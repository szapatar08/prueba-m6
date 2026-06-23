using System.Text;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Minio;
using Prueba.Application.Interfaces;
using Prueba.Infrastructure.Data;
using Prueba.Infrastructure.Services;
using Prueba.Modules.KYC.Jobs;
using Prueba.Modules.Notifications.Handlers;
using Prueba.Modules.Notifications.Jobs;

var builder = WebApplication.CreateBuilder(args);
var isTesting = builder.Environment.IsEnvironment("Testing");

// Database
if (isTesting)
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite("DataSource=:memory:"));
}
else
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
}

// Tenant context (scoped per request)
builder.Services.AddScoped<ICurrentTenant, CurrentTenantService>();

// Unit of Work (AppDbContext implements IUnitOfWork)
builder.Services.AddScoped<IUnitOfWork>(sp =>
    sp.GetRequiredService<AppDbContext>());

// Generic repository
builder.Services.AddScoped<IRepository, Repository>();

// JWT token generator
builder.Services.AddScoped<IJwtTokenGenerator, Prueba.Modules.Identity.Features.Login.JwtTokenGenerator>();

// Domain event dispatcher (Notifications module overrides default)
builder.Services.AddScoped<IDomainEventDispatcher, Prueba.Modules.Notifications.NotificationsDomainEventDispatcher>();

if (!isTesting)
{
    // MinIO client (skip in testing)
    builder.Services.AddSingleton<IMinioClient>(sp =>
    {
        var config = builder.Configuration.GetSection("MinIO");
        var endpoint = config["Endpoint"] ?? "localhost:9000";
        var accessKey = config["AccessKey"] ?? "minioadmin";
        var secretKey = config["SecretKey"] ?? "minioadmin";
        var useSsl = bool.TryParse(config["UseSsl"], out var ssl) && ssl;

        return new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(accessKey, secretKey)
            .WithSSL(useSsl)
            .Build();
    });

    // Object storage service (skip in testing — replaced by NoOpObjectStorage)
    builder.Services.AddScoped<IObjectStorage, MinioStorageService>();

    // Email service (skip in testing — replaced by NoOpEmailService)
    builder.Services.AddScoped<IEmailService, MailKitEmailService>();
}

// KYC background jobs
builder.Services.AddScoped<ProcessKycDocumentJob>();
builder.Services.AddScoped<KycCleanupJob>();

// Notification handlers
builder.Services.AddScoped<BookingConfirmedEventHandler>();
builder.Services.AddScoped<BookingCancelledEventHandler>();
builder.Services.AddScoped<KycCompletedEventHandler>();
builder.Services.AddScoped<BookingCreatedEventHandler>();
builder.Services.AddScoped<SendNotificationJob>();

if (!isTesting)
{
    // Hangfire (skip in testing)
    builder.Services.AddHangfire(config =>
        config.UsePostgreSqlStorage(options =>
            options.UseNpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"))));
    builder.Services.AddHangfireServer();
}

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var jwtKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
    ?? jwtSettings["Key"]
    ?? throw new InvalidOperationException("JWT key not configured. Set JWT_SECRET_KEY environment variable or Jwt:Key in configuration.");
var key = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});

builder.Services.AddAuthorization();

// Controllers
builder.Services.AddControllers();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Prueba API",
        Version = "v1",
        Description = "Multi-tenant property management and booking platform"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Health checks
if (!isTesting)
{
    builder.Services.AddHealthChecks()
        .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!);
}
else
{
    builder.Services.AddHealthChecks();
}

var app = builder.Build();

// Middleware pipeline — order matters
// Exception handling first to catch all downstream exceptions
app.UseMiddleware<Prueba.Api.Middleware.ExceptionHandling>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsEnvironment("Docker"))
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

// Tenant resolution after auth (needs JWT claims)
app.UseMiddleware<Prueba.Api.Middleware.TenantResolution>();

app.MapControllers();
app.MapHealthChecks("/health");

if (!isTesting)
{
    // Hangfire dashboard and recurring jobs (skip in testing)
    app.UseHangfireDashboard("/hangfire");

    RecurringJob.AddOrUpdate<KycCleanupJob>(
        "kyc-cleanup",
        job => job.ExecuteAsync(CancellationToken.None),
        Cron.Daily);
}

app.Run();

// Make Program accessible for integration testing
public partial class Program { }
