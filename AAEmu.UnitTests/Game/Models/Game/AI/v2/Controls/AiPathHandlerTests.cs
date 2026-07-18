using System.Numerics;
using AAEmu.Game.Models.Game.AI.v2.Controls;

namespace AAEmu.UnitTests.Game.Models.Game.AI.v2.Controls;

public class AiPathHandlerTests
{
    [Test]
    public async Task GetMovementTarget_GroundNpc_UsesCurrentTerrainHeight()
    {
        var waypoint = new Vector3(14802.766f, 12095.709f, 209.8933f);
        var ownerPosition = new Vector3(14802.766f, 12095.709f, 180.23604f);

        var result = AiPathHandler.GetMovementTarget(waypoint, ownerPosition, canFly: false);

        await Assert.That(result).IsEqualTo(new Vector3(waypoint.X, waypoint.Y, ownerPosition.Z));
    }

    [Test]
    public async Task GetMovementTarget_FlyingNpc_PreservesWaypointHeight()
    {
        var waypoint = new Vector3(100f, 200f, 300f);
        var ownerPosition = new Vector3(90f, 190f, 10f);

        var result = AiPathHandler.GetMovementTarget(waypoint, ownerPosition, canFly: true);

        await Assert.That(result).IsEqualTo(waypoint);
    }

    [Test]
    public async Task CalculateStepDistance_UsesWholeTickDuration()
    {
        var result = AiPathHandler.CalculateStepDistance(2d, TimeSpan.FromMilliseconds(1250));

        await Assert.That(result).IsEqualTo(2.5f);
    }
}
