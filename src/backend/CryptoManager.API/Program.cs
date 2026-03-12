using System.Runtime.InteropServices;
using CryptoManager.API.Middleware;
using CryptoManager.Application.Abstractions;
using CryptoManager.Application.UseCases;
using CryptoManager.Infrastructure.Auditing;
using CryptoManager.Infrastructure.Crypto;
using CryptoManager.Infrastructure.HSM;
using CryptoManager.Infrastructure.HSM.PKCS11;
using CryptoManager.Infrastructure.HSM.SoftHSM;
using CryptoManager.Infrastructure.Persistence.EntityFramework;
using CryptoManager.Infrastructure.Persistence.InMemory;
using CryptoManager.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Net.Pkcs11Interop.HighLevelAPI;

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
        "SoftHsm" => new SoftHsmProvider(cfg.Id),
        "Pkcs11" => new Pkcs11HsmProvider(cfg.Id,
            cfg.Pkcs11 ?? throw new InvalidOperationException($"HsmProvider '{cfg.Id}' of type Pkcs11 requires a Pkcs11 config block.")),
        _ => throw new InvalidOperationException($"Unknown HsmProvider type '{cfg.Type}' for provider '{cfg.Id}'.")
    };

    providers[cfg.Id] = provider;
}

builder.Services.AddSingleton<IHsmProviderRegistry>(
    new HsmProviderRegistry(providers, defaultProviderId: defaults[0].Id));
builder.Services.AddScoped<IKeyRepository, KeyRepository>();
builder.Services.AddScoped<IAuditSink, AuditSink>();
builder.Services.AddSingleton<IClock, SystemClock>();

builder.Services.AddScoped<CreateKeyUseCase>();
builder.Services.AddScoped<RotateKeyUseCase>();
builder.Services.AddScoped<SignDigestUseCase>();
builder.Services.AddScoped<GetPublicKeyUseCase>();
builder.Services.AddScoped<ListKeysUseCase>();
builder.Services.AddScoped<SignFileUseCase>();
builder.Services.AddScoped<DeleteKeyUseCase>();

builder.Services.AddScoped<ISignedArtifactBuilder, SignedArtifactBuilder>();
builder.Services.AddScoped<IPadesSigner, PadesSigner>();
builder.Services.AddScoped<IPkcs7AttachedSigner, BouncyCastlePkcs7AttachedSigner>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseMiddleware<ErrorHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.UseSwagger();
app.UseSwaggerUI();

app.Run();
