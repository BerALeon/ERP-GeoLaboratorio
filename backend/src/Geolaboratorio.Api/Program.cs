using System.Text;
using System.Text.Json.Serialization;
using FluentValidation;
using Geolaboratorio.Api;
using Geolaboratorio.Application;
using Geolaboratorio.Infrastructure;
using Geolaboratorio.Infrastructure.Persistence;
using Geolaboratorio.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

var railwayPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(railwayPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{railwayPort}");
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddInfrastructure(cfg);
builder.Services.AddValidatorsFromAssemblyContaining<LoginValidator>();
builder.Services.AddTransient<ValidationFilter>();
builder.Services.AddControllers(o => o.Filters.AddService<ValidationFilter>())
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var jwt = cfg.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
{
    jwt.Key = Environment.GetEnvironmentVariable("JWT_KEY")
        ?? "GeolaboratorioErpSecretKey2026_Topografia Calibracion_32CharsMin!";
    cfg["Jwt:Key"] = jwt.Key;
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
        ValidIssuer = jwt.Issuer, ValidAudience = jwt.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy(Policies.Admin, p => p.RequireRole("Administrador"));
    o.AddPolicy(Policies.AdminOTecnico, p => p.RequireRole("Administrador", "Tecnico"));
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(cfg.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"])
    .AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("Content-Disposition")));

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddFixedWindowLimiter("public", w => { w.PermitLimit = 30; w.Window = TimeSpan.FromMinutes(1); });
    o.AddFixedWindowLimiter("login", w => { w.PermitLimit = 10; w.Window = TimeSpan.FromMinutes(1); });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "GEOLABORATORIO API", Version = "v1", Description = "GestiÃ³n de clientes, equipos y certificados de calibraciÃ³n." });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header,
        Description = "Token JWT obtenido en /api/auth/login"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = []
    });
});

var app = builder.Build();

// Rutas de almacenamiento y logo relativas al contenido de la aplicaciÃ³n.
var appOpt = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AppOptions>>().Value;
appOpt.StoragePath = Path.GetFullPath(appOpt.StoragePath, app.Environment.ContentRootPath);
appOpt.LogoPath ??= Path.Combine(app.Environment.ContentRootPath, "Assets", "logo.png");

app.UseMiddleware<ErrorHandlingMiddleware>();
if (!app.Environment.IsDevelopment()) { app.UseHsts(); }
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapFallbackToFile("index.html");

if (cfg.GetValue("Database:AutoSeed", true))
{
    using var scope = app.Services.CreateScope();
    var seedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbSeeder.SeedAsync(seedDb, cfg);
    if (cfg.GetValue("Seed:Demo", false)) await DbSeeder.SeedDemoAsync(seedDb);
}

app.Run();

public partial class Program;


