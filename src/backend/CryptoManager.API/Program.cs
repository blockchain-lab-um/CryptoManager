using CryptoManager.API.Middleware;
using CryptoManager.Application.Abstractions;
using CryptoManager.Application.UseCases;
using CryptoManager.Infrastructure.Auditing;
using CryptoManager.Infrastructure.HSM.PKCS11;
using CryptoManager.Infrastructure.HSM.SoftHSM;
using CryptoManager.Infrastructure.Persistence.InMemory;
using CryptoManager.Infrastructure.Time;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
//builder.Services.AddOpenApi();

builder.Services.AddSingleton(new Pkcs11Options
{
    LibraryPath = builder.Configuration["Pkcs11:LibraryPath"]!,
    TokenLabel = builder.Configuration["Pkcs11:TokenLabel"]!,
    UserPin = builder.Configuration["Pkcs11:UserPin"]!,
    RsaKeySizeBits = 2048
});

builder.Services.AddSingleton<IHsmProvider, YubiHsmPkcs11Provider>();
builder.Services.AddSingleton<IKeyRepository, InMemoryKeyRepository>();
builder.Services.AddSingleton<IAuditSink, InMemoryAuditSink>();
builder.Services.AddSingleton<IClock, SystemClock>();

builder.Services.AddScoped<CreateKeyUseCase>();
builder.Services.AddScoped<RotateKeyUseCase>();
builder.Services.AddScoped<SignDigestUseCase>();
builder.Services.AddScoped<GetPublicKeyUseCase>();
builder.Services.AddScoped<ListKeysUseCase>();
var app = builder.Build();

app.UseMiddleware<ErrorHandlingMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    //app.MapOpenApi();
}

app.UseCors();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.UseSwagger();
app.UseSwaggerUI();

app.Run();
