using FluentAssertions;
using Kable.Codecs;
using Xunit;

namespace Kable.Tests.Cases.Codecs;

public sealed class AsciiLineRoutingTests
{
    [Theory]
    [InlineData("$EVT:1", true)]
    [InlineData("#ALARM:OVERHEAT", true)]
    [InlineData("!ALERT", true)]
    [InlineData("*NOTIFY", true)]
    [InlineData("OK", false)]
    [InlineData("READY", false)]
    [InlineData("!OK", true)] // Default legacy behavior
    public void AsciiLineCodec_DefaultBehavior_MatchesLegacyAutonomousPrefixes(string message, bool expectedAutonomous)
    {
        var codec = new AsciiLineCodec();
        bool isAutonomous = codec.IsAutonomousMessage(message);
        isAutonomous.Should().Be(expectedAutonomous);
    }

    [Fact]
    public void AsciiLineCodec_CustomPredicate_AllowsExclamationOkAsCommandResponse()
    {
        // Equipment uses "!OK" as normal command acknowledgment, and only "$" for unsolicited alarms
        var codec = new AsciiLineCodec(isAutonomousPredicate: msg => msg.StartsWith('$'));

        // "!OK" must NOT be classified as autonomous event
        codec.IsAutonomousMessage("!OK").Should().BeFalse();
        codec.IsAutonomousMessage("#100").Should().BeFalse();

        // "$" is classified as autonomous event
        codec.IsAutonomousMessage("$ALARM:DOOR_OPEN").Should().BeTrue();
    }

    [Fact]
    public void AsciiLineCodec_CustomPredicate_NullOrEmpty_ReturnsFalse()
    {
        var codec = new AsciiLineCodec(isAutonomousPredicate: _ => true);
        codec.IsAutonomousMessage("").Should().BeFalse();
        codec.IsAutonomousMessage(null!).Should().BeFalse();
    }
}
