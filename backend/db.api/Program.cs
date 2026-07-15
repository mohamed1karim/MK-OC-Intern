// This is the startup file for the whole API: it wires together the
// database, the services, the controllers, Swagger, CORS, JWT auth, and our
// custom error-handling middleware, then starts listening for HTTP requests.
using System.Text;
using db.api.Auth;
using db.api.BackgroundServices;
using db.api.Middleware;
using db.Context;
using db.Service.Ai;
using db.Service.Analytics;
using db.Service.Auth;
using db.Service.Orders;
using db.Service.Products;
using db.Service.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

// Loads the repo-root .env (simple KEY=VALUE lines) into process environment
// variables before anything else runs, so secrets like GROQ_API_KEY never
// need to be committed to appsettings.*.json. Walks upward from the working
// directory since db.api's own folder sits two levels below the repo root
// that holds .env. A missing .env (e.g. in a real deployment where the key
// is set as a real environment variable) is not an error — it just no-ops.
static void LoadDotEnv()
{
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir is not null)
    {
        var envPath = Path.Combine(dir.FullName, ".env");
        if (File.Exists(envPath))
        {
            foreach (var line in File.ReadAllLines(envPath))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;

                var separatorIndex = trimmed.IndexOf('=');
                if (separatorIndex <= 0) continue;

                var key = trimmed[..separatorIndex].Trim();
                var value = trimmed[(separatorIndex + 1)..].Trim();

                if (Environment.GetEnvironmentVariable(key) is null)
                {
                    Environment.SetEnvironmentVariable(key, value);
                }
            }
            return;
        }
        dir = dir.Parent;
    }
}

LoadDotEnv();

var builder = WebApplication.CreateBuilder(args);

// Turns on controller support, so classes like UsersController (with
// [ApiController]/[Route] attributes) get automatically discovered.
builder.Services.AddControllers();

// Registers AppDbcontext with dependency injection, configured to talk to
// SQL Server using the connection string from appsettings.Development.json.
// Anything that asks for an AppDbcontext (like UserService) gets one of
// these automatically.
builder.Services.AddDbContext<AppDbcontext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Registers UserService as the implementation whenever something asks for
// IUserService (e.g. UsersController's constructor). "AddScoped" means one
// instance is created per HTTP request.
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IAnalyticsReportService, AnalyticsReportService>();
builder.Services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();

// Typed HttpClients for the two Groq-backed AI features — AddHttpClient
// gives each a pooled/reused HttpMessageHandler instead of new-ing one up
// itself. Separate clients since they use separate API keys.
builder.Services.AddHttpClient<IProductDescriptionEnhancer, GroqProductDescriptionEnhancer>();
builder.Services.AddHttpClient<IReportGenerator, GroqReportGenerator>();

// Runs for the app's whole lifetime, generating a fresh analytics report
// once a week (see the class itself for the actual cadence check).
builder.Services.AddHostedService<WeeklyReportHostedService>();

// JWT bearer authentication — validates the token's signature, issuer,
// audience, and expiry on every request. Registering this alone does
// nothing without AddAuthorization() below and app.UseAuthentication() in
// the pipeline further down.
var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

// Required for any [Authorize]/[Authorize(Roles = ...)] attribute to work —
// without this, the app throws at the first request that hits one.
builder.Services.AddAuthorization();

// Allows the Angular dev server (localhost:4200) to call this API from the
// browser. Without this, the browser blocks every request with a CORS error
// — server-side calls (like SSR) aren't affected, only direct browser calls.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Needed for Swagger to be able to inspect our controllers/endpoints.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Registered first (before auth/controllers) so it wraps everything that
// follows — if any later middleware or controller throws NotFoundException/
// ConflictException/etc., this catches it and returns the right status code.
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Must come before UseAuthorization()/MapControllers() — order matters in
// the middleware pipeline, same reasoning as the exception middleware above.
app.UseCors("AllowAngular");

app.UseHttpsRedirection();

// Authentication must run before authorization — it's what populates
// HttpContext.User from the request's bearer token in the first place.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
