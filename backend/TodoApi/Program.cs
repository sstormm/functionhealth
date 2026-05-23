using Microsoft.AspNetCore.Authentication;
using TodoApi.Auth;
using TodoApi.Db;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("Sqlite")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:Sqlite is required in configuration (appsettings.json or environment override).");
builder.Services.AddSingleton<ISqliteConnectionFactory>(new SqliteConnectionFactory(connectionString));
builder.Services.AddSingleton<PasswordService>();

builder.Services
    .AddAuthentication(SessionAuthHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, SessionAuthHandler>(SessionAuthHandler.SchemeName, _ => { });
builder.Services.AddAuthorization();

const string CorsPolicy = "frontend";
// Origins come from Cors:AllowedOrigins in appsettings.json (overridable per
// environment via appsettings.{Environment}.json or env vars like
// Cors__AllowedOrigins__0=https://todo-app.com). Missing config fails loud at
// startup — there's no hardcoded fallback to silently revert to.
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? throw new InvalidOperationException(
        "Cors:AllowedOrigins is required in configuration (appsettings.json or environment override).");
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<ISqliteConnectionFactory>();
    var passwords = scope.ServiceProvider.GetRequiredService<PasswordService>();
    using var conn = factory.OpenConnection();
    Schema.EnsureCreated(conn);
    Seed.EnsureSeeded(conn, passwords);
}

app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;
