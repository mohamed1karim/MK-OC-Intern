// This is the startup file for the whole API: it wires together the
// database, the services, the controllers, Swagger, CORS, and our custom
// error-handling middleware, then starts listening for HTTP requests.
using db.api.Middleware;
using db.Context;
using db.Service.Orders;
using db.Service.Products;
using db.Service.Users;
using Microsoft.EntityFrameworkCore;

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

app.UseAuthorization();

app.MapControllers();

app.Run();
