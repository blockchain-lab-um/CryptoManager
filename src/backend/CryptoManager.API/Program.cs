using CryptoManager.API.Middleware;
using CryptoManager.Application.Abstractions;
using CryptoManager.Application.UseCases;
using CryptoManager.Infrastructure.Auditing;
using CryptoManager.Infrastructure.Crypto;
using CryptoManager.Infrastructure.HSM.PKCS11;
using CryptoManager.Infrastructure.HSM.SoftHSM;
using CryptoManager.Infrastructure.Persistence.EntityFramework;
using CryptoManager.Infrastructure.Persistence.InMemory;
using CryptoManager.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;

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

builder.Services.Configure<Pkcs11Options>(builder.Configuration.GetSection("Pkcs11"));

builder.Services.AddSingleton<IHsmProvider, SoftHsmProvider>();
builder.Services.AddScoped<IKeyRepository, KeyRepository>();
builder.Services.AddScoped<IAuditSink, AuditSink>();
builder.Services.AddSingleton<IClock, SystemClock>();

builder.Services.AddScoped<CreateKeyUseCase>();
builder.Services.AddScoped<RotateKeyUseCase>();
builder.Services.AddScoped<SignDigestUseCase>();
builder.Services.AddScoped<GetPublicKeyUseCase>();
builder.Services.AddScoped<ListKeysUseCase>();
builder.Services.AddScoped<SignFileUseCase>();

builder.Services.AddScoped<ISignedArtifactBuilder, SignedArtifactBuilder>();
builder.Services.AddScoped<IPadesSigner, PadesSigner>();
builder.Services.AddScoped<IPkcs7AttachedSigner, BouncyCastlePkcs7AttachedSigner>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

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
