using System.Net;

namespace SeatReservation.Api.Tests;

public class CorsTests
{
    private static async Task<HttpResponseMessage> GetWithOriginAsync(HttpClient client, string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/events");
        request.Headers.Add("Origin", origin);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Yapilandirilmis_origin_icin_CORS_basligi_doner()
    {
        await using var factory = new Support.ApiFactory();
        using var client = factory.WithWebHostBuilder(b => b.UseSetting("Cors:AllowedOrigins", "https://app.example.com, https://other.example.com"))
            .CreateClient();

        var response = await GetWithOriginAsync(client, "https://app.example.com");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("https://app.example.com", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        // Arayüz geri sayımı sunucu saatine göre düzeltmek için Date başlığını okur.
        Assert.Contains("Date", response.Headers.GetValues("Access-Control-Expose-Headers").Single());
    }

    [Fact]
    public async Task Listede_olmayan_origin_CORS_basligi_almaz()
    {
        await using var factory = new Support.ApiFactory();
        using var client = factory.WithWebHostBuilder(b => b.UseSetting("Cors:AllowedOrigins", "https://app.example.com"))
            .CreateClient();

        var response = await GetWithOriginAsync(client, "https://evil.example.com");

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Ayar_bossa_CORS_kapali()
    {
        await using var factory = new Support.ApiFactory();
        using var client = factory.CreateClient();

        var response = await GetWithOriginAsync(client, "https://app.example.com");

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Preflight_Authorization_basligina_izin_verir()
    {
        await using var factory = new Support.ApiFactory();
        using var client = factory.WithWebHostBuilder(b => b.UseSetting("Cors:AllowedOrigins", "https://app.example.com"))
            .CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Options, "/api/seats/" + Guid.NewGuid() + "/hold");
        request.Headers.Add("Origin", "https://app.example.com");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains("authorization", string.Join(",", response.Headers.GetValues("Access-Control-Allow-Headers")), StringComparison.OrdinalIgnoreCase);
    }
}
