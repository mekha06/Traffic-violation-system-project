using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TrafficViolationSystem.API.Data;
using TrafficViolationSystem.API.DTOs;
using TrafficViolationSystem.API.Services;

namespace TrafficViolationSystem.Tests;

/// <summary>
/// Integration tests for the API controllers using WebApplicationFactory.
/// Replaces the real database with InMemory and mocks external services.
/// </summary>
public class ControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ControllerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove the real DbContext registration
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<TrafficDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                // Add InMemory database
                services.AddDbContext<TrafficDbContext>(options =>
                    options.UseInMemoryDatabase("IntegrationTestDb_" + Guid.NewGuid()));

                // Mock OCR service
                var mockOcr = new Mock<IOcrService>();
                mockOcr.Setup(s => s.RecognizePlateAsync(It.IsAny<Stream>(), It.IsAny<string>()))
                    .ReturnsAsync(new OcrResultDto
                    {
                        PlateNumber = "TEST1234",
                        Confidence = 0.92
                    });

                // Remove existing registrations
                var ocrDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IOcrService));
                if (ocrDescriptor != null) services.Remove(ocrDescriptor);
                services.AddSingleton(mockOcr.Object);

                // Mock Gemini service
                var mockGemini = new Mock<IGeminiService>();
                mockGemini.Setup(s => s.GenerateSummaryAsync(
                        It.IsAny<string>(), It.IsAny<string>(),
                        It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string?>()))
                    .ReturnsAsync("Test AI summary for the violation case.");

                var geminiDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IGeminiService));
                if (geminiDescriptor != null) services.Remove(geminiDescriptor);
                services.AddSingleton(mockGemini.Object);

                // Ensure the database is created and seeded
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TrafficDbContext>();
                db.Database.EnsureCreated();
            });
        });
    }

    private HttpClient CreateClient() => _factory.CreateClient();

    // ── Junction Tests ──

    [Fact]
    public async Task GetJunctions_ReturnsOk()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/junctions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateJunction_ReturnsCreated()
    {
        var client = CreateClient();
        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new { name = "Test Junction", location = "Test Area" }),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync("/api/junctions", content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ── Vehicle Tests ──

    [Fact]
    public async Task GetVehicles_ReturnsOk()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/vehicles");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateVehicle_ReturnsCreated()
    {
        var client = CreateClient();
        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new
            {
                plateNumber = "INT" + Guid.NewGuid().ToString("N")[..6],
                ownerName = "Test Owner",
                vehicleType = "Car"
            }),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync("/api/vehicles", content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ── Violation Case Tests ──

    [Fact]
    public async Task GetViolationCases_ReturnsOk()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/violationcases");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateViolationCase_WithImage_ReturnsCreated()
    {
        var client = CreateClient();

        // First, create a junction to reference
        var junctionContent = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new { name = "Case Junction " + Guid.NewGuid(), location = "Test" }),
            System.Text.Encoding.UTF8,
            "application/json");
        var junctionResponse = await client.PostAsync("/api/junctions", junctionContent);
        var junctionJson = await junctionResponse.Content.ReadAsStringAsync();
        var junctionDoc = System.Text.Json.JsonDocument.Parse(junctionJson);
        var junctionId = junctionDoc.RootElement.GetProperty("junctionId").GetInt32();

        // Create a fake image
        var imageContent = new ByteArrayContent(CreateFakeJpeg());
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

        // Build multipart form
        var form = new MultipartFormDataContent
        {
            { new StringContent("Red Light"), "ViolationType" },
            { new StringContent("Test violation"), "Description" },
            { new StringContent(junctionId.ToString()), "JunctionId" },
            { imageContent, "image", "test_plate.jpg" }
        };

        var response = await client.PostAsync("/api/violationcases", form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var doc = System.Text.Json.JsonDocument.Parse(body);
        Assert.Equal("Pending", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("TEST1234", doc.RootElement.GetProperty("recognizedPlate").GetString());
    }

    // ── Helper ──

    private static byte[] CreateFakeJpeg()
    {
        // Minimal valid JPEG (1x1 pixel, white)
        return new byte[]
        {
            0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46,
            0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01,
            0x00, 0x01, 0x00, 0x00, 0xFF, 0xDB, 0x00, 0x43,
            0x00, 0x08, 0x06, 0x06, 0x07, 0x06, 0x05, 0x08,
            0x07, 0x07, 0x07, 0x09, 0x09, 0x08, 0x0A, 0x0C,
            0x14, 0x0D, 0x0C, 0x0B, 0x0B, 0x0C, 0x19, 0x12,
            0x13, 0x0F, 0x14, 0x1D, 0x1A, 0x1F, 0x1E, 0x1D,
            0x1A, 0x1C, 0x1C, 0x20, 0x24, 0x2E, 0x27, 0x20,
            0x22, 0x2C, 0x23, 0x1C, 0x1C, 0x28, 0x37, 0x29,
            0x2C, 0x30, 0x31, 0x34, 0x34, 0x34, 0x1F, 0x27,
            0x39, 0x3D, 0x38, 0x32, 0x3C, 0x2E, 0x33, 0x34,
            0x32, 0xFF, 0xC0, 0x00, 0x0B, 0x08, 0x00, 0x01,
            0x00, 0x01, 0x01, 0x01, 0x11, 0x00, 0xFF, 0xC4,
            0x00, 0x1F, 0x00, 0x00, 0x01, 0x05, 0x01, 0x01,
            0x01, 0x01, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x01, 0x02, 0x03, 0x04,
            0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0xFF,
            0xC4, 0x00, 0xB5, 0x10, 0x00, 0x02, 0x01, 0x03,
            0x03, 0x02, 0x04, 0x03, 0x05, 0x05, 0x04, 0x04,
            0x00, 0x00, 0x01, 0x7D, 0x01, 0x02, 0x03, 0x00,
            0x04, 0x11, 0x05, 0x12, 0x21, 0x31, 0x41, 0x06,
            0x13, 0x51, 0x61, 0x07, 0x22, 0x71, 0x14, 0x32,
            0x81, 0x91, 0xA1, 0x08, 0x23, 0x42, 0xB1, 0xC1,
            0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00,
            0x3F, 0x00, 0x7B, 0x94, 0x11, 0x00, 0x00, 0x00,
            0x00, 0xFF, 0xD9
        };
    }
}
