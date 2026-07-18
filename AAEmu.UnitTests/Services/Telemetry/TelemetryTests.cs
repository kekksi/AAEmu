using AAEmu.Game.Services.Telemetry;

namespace AAEmu.UnitTests.Services.Telemetry;

public class TelemetryTests
{
    [Test]
    [Arguments(false, 0, 0, 0, 0, 0, 0, "utility")]
    [Arguments(true, 0, 0, 0, 0, 0, 0, "no_damage_effect")]
    [Arguments(true, 1, 1, 0, 0, 0, 0, "damage")]
    [Arguments(true, 2, 1, 1, 0, 0, 0, "mixed")]
    [Arguments(true, 1, 0, 1, 0, 0, 0, "zero")]
    [Arguments(true, 1, 0, 0, 0, 0, 1, "zero")]
    [Arguments(true, 1, 0, 0, 1, 0, 0, "avoided")]
    [Arguments(true, 1, 0, 0, 0, 1, 0, "immune")]
    public async Task SkillOutcome_IsNormalized(
        bool expectsDamage,
        int damageApplications,
        int positiveHits,
        int zeroHits,
        int avoidedHits,
        int immuneHits,
        int skippedHits,
        string expected)
    {
        var actual = SkillTelemetryState.DetermineOutcome(
            expectsDamage,
            damageApplications,
            positiveHits,
            zeroHits,
            avoidedHits,
            immuneHits,
            skippedHits);

        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    public async Task ExceptionFingerprint_IgnoresMessageButIncludesType()
    {
        var first = Convert.ToHexString(TelemetryEmitter.CreateExceptionFingerprint(CaptureInvalidOperation("player 17")));
        var second = Convert.ToHexString(TelemetryEmitter.CreateExceptionFingerprint(CaptureInvalidOperation("player 42")));
        var differentType = Convert.ToHexString(TelemetryEmitter.CreateExceptionFingerprint(CaptureArgument("player 17")));

        await Assert.That(first).IsEqualTo(second);
        await Assert.That(first).IsNotEqualTo(differentType);
    }

    private static Exception CaptureInvalidOperation(string message)
    {
        try
        {
            throw new InvalidOperationException(message);
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static Exception CaptureArgument(string message)
    {
        try
        {
            throw new ArgumentException(message);
        }
        catch (Exception exception)
        {
            return exception;
        }
    }
}
