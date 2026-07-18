using AAEmu.Game.Models.Game.DoodadObj;
using AAEmu.Game.Models.Game.DoodadObj.Funcs;

namespace AAEmu.UnitTests.Game.Models.Game.DoodadObj.Funcs;

public class DoodadFuncGatherTests
{
    [Test]
    public async Task OreMine_AdvancesDoodadPhase()
    {
        var doodad = new Doodad();

        new DoodadFuncOreMine().Use(null, doodad, 0);

        await Assert.That(doodad.ToNextPhase).IsTrue();
    }

    [Test]
    public async Task RockMine_AdvancesDoodadPhase()
    {
        var doodad = new Doodad();

        new DoodadFuncRockMine().Use(null, doodad, 0);

        await Assert.That(doodad.ToNextPhase).IsTrue();
    }

    [Test]
    public async Task FruitPick_AdvancesDoodadPhase()
    {
        var doodad = new Doodad();

        new DoodadFuncFruitPick().Use(null, doodad, 0);

        await Assert.That(doodad.ToNextPhase).IsTrue();
    }
}
