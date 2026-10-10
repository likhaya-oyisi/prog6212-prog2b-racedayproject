using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using RaceDay.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RaceDay API",
        Version = "v1",
        Description = "RESTful API for the RaceDay event management system (PROG6212 Part 2). " +
                      "Register or log in first to receive a session cookie; the cookie carries the " +
                      "user identity and role (Organiser / Participant) on every subsequent request."
    });

    // Include XML comments so every endpoint shows a description the UI
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);

    // Document the session cookie so it is visible/testable from Swagger UI.
    c.AddSecurityDefinition("session", new OpenApiSecurityScheme
    {
        Name = "raceday.session",
        In = ParameterLocation.Cookie,
        Type = SecuritySchemeType.ApiKey,
        Description = "Session cookie issued by POST /api/auth/register or POST /api/auth/login."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "session" } }] = new List<string>()
    });
});

// Session-based authentication using an encrypted HTTP-only cookie.
// The cookie stores the claims (user id, email, role) for all subsequent requests.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "raceday.session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;

        // This is a pure API: never redirect, return proper status codes instead.
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
builder.Services.AddAuthorization();

// EF Core - Code First. Development uses SQLite (no LocalDB required);
// Production / default connection string targets SQL Server as per Part 2.
builder.Services.AddDbContext<RaceDayDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=raceday.db";

    if (connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) &&
        !connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlite(connectionString);
    }
    else
    {
        options.UseSqlServer(connectionString);
    }
});

var app = builder.Build();

// Ensure the database schema exists (LocalDB / first-run convenience).
// Skipped in the Testing environment so the InMemory provider used by tests is not replaced.
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
    db.Database.EnsureCreated();
}

// ---------------------------------------------------------
// Pipeline
// ---------------------------------------------------------
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "RaceDay API v1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "Swagger — supported by Likhaya Oyisi";
    // CSS-only rebrand (no MutationObserver — that freezes Swagger UI into a blank page).
    c.HeadContent = """
        <style>
          .swagger-ui .topbar { background-color: #1b1b1b; }
          .swagger-ui .topbar .topbar-wrapper .link {
            display: flex;
            align-items: center;
            max-width: none !important;
          }
          .swagger-ui .topbar .topbar-wrapper .link svg,
          .swagger-ui .topbar .topbar-wrapper .link img {
            display: none !important;
          }
          .swagger-ui .topbar .topbar-wrapper .link::after {
            content: "Swagger  supported by Likhaya Oyisi";
            color: #ffffff;
            font-size: 16px;
            font-weight: 700;
            white-space: nowrap;
          }
        </style>
        """;
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Make the implicit Program class public so the integration tests can use
// WebApplicationFactory<Program>.
public partial class Program { }
