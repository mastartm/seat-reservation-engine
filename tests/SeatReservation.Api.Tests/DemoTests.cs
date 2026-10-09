using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SeatReservation.Api.Tests.Support;
using SeatReservation.Infrastructure.Persistence;

namespace SeatReservation.Api.Tests;

public class DemoTests
{
    // Seçenek servis olarak ezilir, "Demo:Enabled" ayarı olarak verilmez: ayar açılışta DatabaseInitializer'ı tetikler,
    // oysa test şeması host kurulduktan sonra (ApiFactory.CreateHost) oluşturulur. Tohumlama testlerde ayrıca elle çağrılır.
    private static HttpClient DemoClient(ApiFactory factory, bool enabled) =>
        factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
            s.Configure<SeatReservation.Application.Demo.DemoOptions>(o => o.Enabled = enabled))).CreateClient();

    [Fact]
    public async Task Demo_kapaliyken_404_doner()
    {
        await using var factory = new ApiFactory();
        using var client = DemoClient(factory, enabled: false);

        var response = await client.PostAsync("/api/auth/demo", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Demo_aciksa_gecerli_token_veren_User_rolunde_misafir_acar()
    {
        await using var factory = new ApiFactory();
        using var client = DemoClient(factory, enabled: true);

        var response = await client.PostAsync("/api/auth/demo", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.EndsWith("@demo.local", body.GetProperty("email").GetString());
        Assert.Equal("User", body.GetProperty("role").GetString());

        var me = await factory.CreateClientWithToken(body.GetProperty("token").GetString()!).GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Her_demo_girisi_ayri_bir_hesap_acar()
    {
        await using var factory = new ApiFactory();
        using var client = DemoClient(factory, enabled: true);

        var first = await (await client.PostAsync("/api/auth/demo", null)).Content.ReadFromJsonAsync<JsonElement>();
        var second = await (await client.PostAsync("/api/auth/demo", null)).Content.ReadFromJsonAsync<JsonElement>();

        Assert.NotEqual(first.GetProperty("userId").GetGuid(), second.GetProperty("userId").GetGuid());
    }

    [Fact]
    public async Task Misafir_hesabina_parolayla_girilemez()
    {
        await using var factory = new ApiFactory();
        using var client = DemoClient(factory, enabled: true);
        var guest = await (await client.PostAsync("/api/auth/demo", null)).Content.ReadFromJsonAsync<JsonElement>();

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { email = guest.GetProperty("email").GetString(), password = "herhangi-bir-parola" });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Tohum_etkinlikleri_ve_satilmis_koltuklari_olusturur()
    {
        await using var factory = new ApiFactory();
        using var scope = factory.Services.CreateScope();

        await DemoDataSeeder.SeedAsync(scope.ServiceProvider);

        var client = factory.CreateClient();
        var events = await client.GetFromJsonAsync<JsonElement>("/api/events");
        Assert.Equal(2, events.GetArrayLength());

        var firstId = events[0].GetProperty("id").GetGuid();
        var seats = await client.GetFromJsonAsync<JsonElement>($"/api/events/{firstId}/seats");
        var statuses = seats.EnumerateArray().Select(s => s.GetProperty("status").GetString()).ToList();
        Assert.Contains("Sold", statuses);
        Assert.Contains("Available", statuses);
    }

    [Fact]
    public async Task Tohumlama_tekrar_calisinca_veriyi_cogaltmaz()
    {
        await using var factory = new ApiFactory();
        using (var scope = factory.Services.CreateScope()) await DemoDataSeeder.SeedAsync(scope.ServiceProvider);
        using (var scope = factory.Services.CreateScope()) await DemoDataSeeder.SeedAsync(scope.ServiceProvider);

        var events = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/events");
        Assert.Equal(2, events.GetArrayLength());
    }

    [Fact]
    public async Task Tohum_verideki_satilmis_koltuga_misafir_hold_alamaz()
    {
        await using var factory = new ApiFactory();
        using (var scope = factory.Services.CreateScope()) await DemoDataSeeder.SeedAsync(scope.ServiceProvider);
        using var client = DemoClient(factory, enabled: true);
        var guest = await (await client.PostAsync("/api/auth/demo", null)).Content.ReadFromJsonAsync<JsonElement>();

        var events = await client.GetFromJsonAsync<JsonElement>("/api/events");
        var seats = await client.GetFromJsonAsync<JsonElement>($"/api/events/{events[0].GetProperty("id").GetGuid()}/seats");
        var sold = seats.EnumerateArray().First(s => s.GetProperty("status").GetString() == "Sold").GetProperty("id").GetGuid();

        var response = await factory.CreateClientWithToken(guest.GetProperty("token").GetString()!)
            .PostAsync($"/api/seats/{sold}/hold", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
