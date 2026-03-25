using System.IdentityModel.Tokens.Jwt;
using System.Runtime.InteropServices;
using System.Security.Claims;
using CryptoManager.API.Middleware;
using CryptoManager.Application.Abstractions;
using CryptoManager.Application.UseCases;
using CryptoManager.Infrastructure.Auditing;
using CryptoManager.Infrastructure.Crypto;
using CryptoManager.Infrastructure.HSM;
using CryptoManager.Infrastructure.HSM.PKCS11;
using CryptoManager.Infrastructure.HSM.SoftHSM;
using CryptoManager.Infrastructure.Identity;
using CryptoManager.Infrastructure.Persistence.EntityFramework;
using CryptoManager.Infrastructure.Persistence.EntityFramework.Repositories;
using CryptoManager.Infrastructure.Time;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Net.Pkcs11Interop.HighLevelAPI;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("X-Audit-Event-Id", "X-Key-Id", "X-Key-Version", "X-Mechanism",
                "X-Signed-Format", "X-File-Name"));
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Identity ──────────────────────────────────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// Suppress Identity's cookie redirect for API requests — return 401/403 instead.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

// ── JWT Authentication ─────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret is not configured. Use dotnet user-secrets.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "CryptoManager";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "CryptoManager";

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtRegisteredClaimNames.Name,
            RoleClaimType = ClaimTypes.Role
        };
    });

// ── Authorization ──────────────────────────────────────────────────────────
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("CanOperate", policy => policy.RequireRole("Admin", "Operator"));
});

// ── HTTP context / current user ────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IUserLookup, UserLookup>();
builder.Services.AddScoped<TokenService>();

// ── HSM providers (config-driven) ─────────────────────────────────────────
var pkcs11InteropAssembly = typeof(Pkcs11InteropFactories).Assembly;

NativeLibrary.SetDllImportResolver(
    pkcs11InteropAssembly,
    (libraryName, assembly, searchPath) =>
    {
        if (libraryName == "libdl")
            return NativeLibrary.Load("libdl.so.2", assembly, searchPath);

        return IntPtr.Zero;
    });

var hsmConfigs = builder.Configuration
    .GetSection("HsmProviders")
    .Get<HsmProviderConfig[]>()
    ?? throw new InvalidOperationException("HsmProviders section is missing from configuration.");

var defaults = hsmConfigs.Where(c => c.IsDefault).ToList();
if (defaults.Count != 1)
    throw new InvalidOperationException($"Exactly one HsmProvider must have IsDefault=true, found {defaults.Count}.");

var providers = new Dictionary<string, IHsmProvider>();
foreach (var cfg in hsmConfigs)
{
    if (string.IsNullOrWhiteSpace(cfg.Id))
        throw new InvalidOperationException("Each HsmProvider entry must have a non-empty Id.");

    IHsmProvider provider = cfg.Type switch
    {
        "SoftHsm" => new SoftHsmProvider(cfg.Id, cfg.SoftHsm?.FilePath),
        "Pkcs11" => new Pkcs11HsmProvider(cfg.Id,
            cfg.Pkcs11 ?? throw new InvalidOperationException($"HsmProvider '{cfg.Id}' of type Pkcs11 requires a Pkcs11 config block.")),
        _ => throw new InvalidOperationException($"Unknown HsmProvider type '{cfg.Type}' for provider '{cfg.Id}'.")
    };

    providers[cfg.Id] = provider;
}

builder.Services.AddSingleton<IHsmProviderRegistry>(
    new HsmProviderRegistry(providers, defaultProviderId: defaults[0].Id));
builder.Services.AddScoped<IKeyRepository, KeyRepository>();
builder.Services.AddScoped<ICertificateRepository, CertificateRepository>();
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();
builder.Services.AddScoped<IAuditSink, AuditSink>();
builder.Services.AddScoped<IAuditRepository, AuditRepository>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<ICertificateAuthority, SoftSelfSignedCertificateAuthority>();
builder.Services.AddScoped<ICsrBuilder, BouncyCastleCsrBuilder>();
builder.Services.AddScoped<ICertificateValidator, BouncyCastleCertificateValidator>();

builder.Services.AddScoped<CreateKeyUseCase>();
builder.Services.AddScoped<RotateKeyUseCase>();
builder.Services.AddScoped<SignDigestUseCase>();
builder.Services.AddScoped<GetPublicKeyUseCase>();
builder.Services.AddScoped<ListKeysUseCase>();
builder.Services.AddScoped<SignFileUseCase>();
builder.Services.AddScoped<DeleteKeyUseCase>();
builder.Services.AddScoped<GetAuditLogsUseCase>();

builder.Services.AddScoped<ISignedArtifactBuilder, SignedArtifactBuilder>();
builder.Services.AddScoped<IPadesSigner, PadesSigner>();
builder.Services.AddScoped<IPkcs7AttachedSigner, BouncyCastlePkcs7AttachedSigner>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ── Startup: migrate DB + seed roles/admin ─────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;

    var db = sp.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    var roleManager = sp.GetRequiredService<RoleManager<ApplicationRole>>();
    foreach (var roleName in new[] { "Admin", "Operator" })
    {
        if (!await roleManager.RoleExistsAsync(roleName))
            await roleManager.CreateAsync(new ApplicationRole(roleName));
    }

    var adminUserName = builder.Configuration["DefaultAdmin:UserName"] ?? "admin";
    var adminEmail = builder.Configuration["DefaultAdmin:Email"] ?? "admin@cryptomanager.local";
    var adminPassword = builder.Configuration["DefaultAdmin:Password"]
        ?? throw new InvalidOperationException("DefaultAdmin:Password is not configured. Use dotnet user-secrets.");

    var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
    if (await userManager.FindByNameAsync(adminUserName) is null)
    {
        var adminUser = new ApplicationUser { UserName = adminUserName, Email = adminEmail };
        var result = await userManager.CreateAsync(adminUser, adminPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Failed to create default admin: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        await userManager.AddToRoleAsync(adminUser, "Admin");
    }
}

app.UseMiddleware<ErrorHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();