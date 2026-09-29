using Microsoft.EntityFrameworkCore;
using TrafficViolationSystem.API.Data;
using TrafficViolationSystem.API.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──
builder.Services.AddDbContext<TrafficDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── HTTP Clients for external services ──

// Python OCR Vision Service
builder.Services.AddHttpClient<IOcrService, OcrService>(client =>
{
    var baseUrl = builder.Configuration["VisionService:BaseUrl"] ?? "http://localhost:8000";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Gemini AI Service
builder.Services.AddHttpClient<IGeminiService, GeminiService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});

// ── Swagger / OpenAPI ──
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Traffic Violation System API",
        Version = "v1",
        Description = "API for managing traffic violation cases with OCR plate recognition and AI-generated summaries."
    });
});

// ── CORS (for development) ──
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// ── Middleware Pipeline ──
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Traffic Violation System v1");
        c.RoutePrefix = string.Empty; // Swagger at root URL
    });
}

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// ── Auto-apply migrations and seed on startup (Development only) ──
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TrafficDbContext>();
    try
    {
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Could not apply migrations automatically. Please run 'dotnet ef database update' manually.");
    }
}

app.Run();

// Make the implicit Program class public for integration testing
public partial class Program { }
