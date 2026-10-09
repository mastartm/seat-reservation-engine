using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SeatReservation.Api.Tests.Support;

namespace SeatReservation.Api.Tests;

public class AuthApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "correct-horse-battery";

    private static string NewEmail() => $"{Guid.NewGuid():N}@test.local";

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>());

    [Fact]
    public async Task Register_returns_token_that_authenticates_as_user_role()
    {
        var client = factory.CreateClient();
        var email = NewEmail();

        var response = await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadAsync(response);
        var token = body.GetProperty("token").GetString()!;

        var me = await factory.CreateClientWithToken(token).GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var meBody = await ReadAsync(me);
        Assert.Equal(email, meBody.GetProperty("email").GetString());
        Assert.Equal("User", meBody.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Register_same_email_in_parallel_yields_exactly_one_created_and_rest_conflict_never_500()
    {
        const int contenders = 20;
        var email = NewEmail();
        var client = factory.CreateClient(); // test sunucusu tek seferde, paralellikten önce ayağa kalksın
        var gate = new TaskCompletionSource();

        // Her istek "e-posta var mı?" kontrolünü geçebilsin diye hepsi aynı anda salınır (yarış koşulu).
        var tasks = Enumerable.Range(0, contenders).Select(_ => Task.Run(async () =>
        {
            await gate.Task;
            var response = await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password });
            return response.StatusCode;
        })).ToArray();
        await Task.Delay(100);
        gate.SetResult();
        var statuses = await Task.WhenAll(tasks);

        Assert.Equal(1, statuses.Count(s => s == HttpStatusCode.Created));
        Assert.Equal(contenders - 1, statuses.Count(s => s == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Register_same_email_twice_is_conflict_regardless_of_case()
    {
        var client = factory.CreateClient();
        var email = NewEmail();
        await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password });

        var again = await client.PostAsJsonAsync("/api/auth/register", new { email = email.ToUpperInvariant(), password = Password });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Theory]
    [InlineData("not-an-email", "long-enough-password")]
    [InlineData("ok@test.local", "short")]
    [InlineData("", "long-enough-password")]
    public async Task Register_rejects_invalid_input(string email, string password)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", new { email, password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_correct_password_returns_token()
    {
        var client = factory.CreateClient();
        var email = NewEmail();
        await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password });

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrEmpty((await ReadAsync(response)).GetProperty("token").GetString()));
    }

    [Fact]
    public async Task Login_fails_identically_for_wrong_password_and_unknown_email()
    {
        var client = factory.CreateClient();
        var email = NewEmail();
        await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password });

        var wrongPassword = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "wrong-password-1" });
        var unknownEmail = await client.PostAsJsonAsync("/api/auth/login", new { email = NewEmail(), password = Password });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal(await wrongPassword.Content.ReadAsStringAsync(), await unknownEmail.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Protected_endpoint_rejects_missing_and_tampered_tokens()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/auth/me")).StatusCode);

        var (_, token) = await factory.CreateUserAsync();
        var tampered = token[..^3] + (token.EndsWith("AAA") ? "BBB" : "AAA");
        var response = await factory.CreateClientWithToken(tampered).GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
