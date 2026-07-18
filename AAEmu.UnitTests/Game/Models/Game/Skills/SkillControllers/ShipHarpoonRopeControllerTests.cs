using AAEmu.Game.Models.Game.Skills.SkillControllers;
using AAEmu.Game.Models.Game.Skills.Templates;

namespace AAEmu.UnitTests.Game.Models.Game.Skills.SkillControllers;

public class ShipHarpoonRopeControllerTests
{
    [Test]
    public async Task ClassifyController_RopeWithLifetime_IsLaunch()
    {
        var controller = CreateController(kindId: 5, value1: 180_000, value2: 100_000);

        var result = ShipHarpoonRopeController.ClassifyController(controller);

        await Assert.That(result).IsEqualTo(ShipHarpoonRopeController.RopeControllerAction.Launch);
    }

    [Test]
    public async Task ClassifyController_RopeWithoutLifetime_IsCut()
    {
        var controller = CreateController(kindId: 5, value1: 0, value2: 0);

        var result = ShipHarpoonRopeController.ClassifyController(controller);

        await Assert.That(result).IsEqualTo(ShipHarpoonRopeController.RopeControllerAction.Cut);
    }

    [Test]
    public async Task ClassifyController_NonRope_IsIgnored()
    {
        var controller = CreateController(kindId: 6, value1: 3, value2: 30_000);

        var result = ShipHarpoonRopeController.ClassifyController(controller);

        await Assert.That(result).IsEqualTo(ShipHarpoonRopeController.RopeControllerAction.None);
    }

    private static SkillControllerTemplate CreateController(uint kindId, int value1, int value2)
    {
        var controller = new SkillControllerTemplate { KindId = kindId };
        controller.Value[0] = value1;
        controller.Value[1] = value2;
        return controller;
    }
}
