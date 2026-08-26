using System.Text;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.RateLimiting;
using System.Security.Cryptography.X509Certificates;
using IdealCreative.Api.Data;
using IdealCreative.Api.Models;
using IdealCreative.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
var dataProtectionPath = builder.Configuration["Security:DataProtectionKeysPath"] ?? "/home/app/.aspnet/DataProtection-Keys";
var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("IdealCreative")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath));
var dataProtectionCertificatePath = builder.Configuration["Security:DataProtectionCertificate:Path"];
if (!string.IsNullOrWhiteSpace(dataProtectionCertificatePath))
{
    var certificate = X509CertificateLoader.LoadPkcs12FromFile(
        dataProtectionCertificatePath,
        builder.Configuration["Security:DataProtectionCertificate:Password"],
        X509KeyStorageFlags.EphemeralKeySet);
    dataProtection.ProtectKeysWithCertificate(certificate);
}
builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 15;
    options.Password.RequiredUniqueChars = 1;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.AllowedForNewUsers = true;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<AppDbContext>()
.AddSignInManager()
.AddPasswordValidator<StrongPasswordValidator<ApplicationUser>>()
.AddDefaultTokenProviders();
builder.Services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromHours(1));
builder.Services.Configure<PasswordHasherOptions>(options =>
{
    options.CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3;
    options.IterationCount = 220_000;
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("authentication", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("password-recovery", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 }));
});

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is required.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "idealcreative.local";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtIssuer,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userId = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                    ?? context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var versionValue = context.Principal?.FindFirst("ic_tv")?.Value;
                if (string.IsNullOrWhiteSpace(userId) || (!string.IsNullOrWhiteSpace(versionValue) && !int.TryParse(versionValue, out _)))
                {
                    context.Fail("Token inválido.");
                    return;
                }

                // Tokens issued before the account-state claim are treated as version 0.
                // Changing the user's version during deletion still revokes those sessions.
                var tokenVersion = int.TryParse(versionValue, out var parsedVersion) ? parsedVersion : 0;

                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var user = await db.Users.AsNoTracking()
                    .Where(row => row.Id == userId)
                    .Select(row => new { row.AccountState, row.TokenVersion })
                    .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
                if (user is null || !string.Equals(user.AccountState, AccountStates.Active, StringComparison.OrdinalIgnoreCase) || user.TokenVersion != tokenVersion)
                    context.Fail("A sessão foi encerrada.");
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<IntegrationSettingsStore>();
builder.Services.AddSingleton<IEmailQueue, EmailQueue>();
builder.Services.AddHostedService<EmailQueueWorker>();
builder.Services.AddScoped<AccountDeletionService>();
builder.Services.AddHostedService<OrderReservationCleanupService>();
builder.Services.AddHostedService<AccountDeletionCleanupService>();
builder.Services.AddSingleton<IStorageClientFactory, StorageClientFactory>();

builder.Services.AddCors(options => options.AddPolicy("frontend", policy =>
{
    var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5289"];
    policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));

var app = builder.Build();

await DatabaseBootstrap.InitializeAsync(app.Services, app.Configuration);

app.UseExceptionHandler();
app.UseForwardedHeaders();
app.UseCors("frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health/ready");
app.MapGet("/health/live", () => Results.Ok(new { status = "ok", service = "idealcreative-api" }));
app.MapControllers();

app.Run();
