using d2c_launcher.Models;
using Xunit;

namespace d2c_launcher.Tests;

public class SecretBanPolicyTests
{
    [Theory]
    [InlineData(0.0, false)]
    [InlineData(1.0, false)]
    [InlineData(2.0, true)]
    [InlineData(4.0, true)]
    [InlineData(7.0, true)]
    [InlineData(99.0, false)]
    [InlineData(null, false)]
    public void CooldownsKeepQueueBlockedWithoutSelectingSecret(double? reason, bool eligible)
    {
        var member = new PartyMemberView("1", "Player", null, isBanned: true, banReason: reason);
        Assert.True(member.IsBanned);
        Assert.Equal(eligible, member.IsSecretEligible);
    }

    [Fact]
    public void ExpiredBanDoesNotGrantSecret()
    {
        var member = new PartyMemberView("1", "Player", null, isBanned: false, banReason: 7);
        Assert.False(member.IsSecretEligible);
    }
}
