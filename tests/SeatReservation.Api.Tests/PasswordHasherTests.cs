using SeatReservation.Infrastructure.Security;

namespace SeatReservation.Api.Tests;

public class PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Verify_accepts_correct_password_and_rejects_wrong_one()
    {
        var hash = _hasher.Hash("s3cret-pass");

        Assert.True(_hasher.Verify("s3cret-pass", hash));
        Assert.False(_hasher.Verify("s3cret-pasS", hash));
    }

    [Fact]
    public void Same_password_yields_different_hashes_because_of_salt()
    {
        Assert.NotEqual(_hasher.Hash("same"), _hasher.Hash("same"));
    }

    [Fact]
    public void Verify_returns_false_for_malformed_hash()
    {
        Assert.False(_hasher.Verify("x", "garbage"));
    }
}
