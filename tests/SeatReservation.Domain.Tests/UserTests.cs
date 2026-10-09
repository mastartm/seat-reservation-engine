using SeatReservation.Domain.Common;
using SeatReservation.Domain.Entities;
using SeatReservation.Domain.Enums;

namespace SeatReservation.Domain.Tests;

public class UserTests
{
    [Fact]
    public void Create_normalizes_email_to_lowercase()
    {
        var user = User.Create("  Ali@Example.COM ", "hash", UserRole.User, DateTimeOffset.UnixEpoch);

        Assert.Equal("ali@example.com", user.Email);
    }

    [Theory]
    [InlineData("", "hash")]
    [InlineData("no-at-sign", "hash")]
    [InlineData("a@b.c", "")]
    public void Create_rejects_invalid_input(string email, string hash)
    {
        Assert.Throws<DomainValidationException>(() => User.Create(email, hash, UserRole.User, DateTimeOffset.UnixEpoch));
    }
}
