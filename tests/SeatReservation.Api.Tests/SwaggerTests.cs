using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SeatReservation.Api.Tests.Support;

namespace SeatReservation.Api.Tests;

public class SwaggerTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Swagger_document_lists_endpoints_and_bearer_scheme()
    {
        var response = await factory.CreateClient().GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(doc.GetProperty("paths").TryGetProperty("/api/seats/{seatId}/hold", out _));
        Assert.True(doc.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out _));
    }

    [Fact]
    public async Task Swagger_ui_is_served()
    {
        var response = await factory.CreateClient().GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
