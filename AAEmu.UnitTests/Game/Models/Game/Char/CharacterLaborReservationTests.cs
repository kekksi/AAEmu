using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.UnitTests.Game.Models.Game.Char;

public class CharacterLaborReservationTests
{
    [Test]
    public async Task TryReserveLaborPower_PreventsOversubscription()
    {
        var character = new Character(new UnitCustomModelParams());
        character.InitializeLaborCache(100, DateTime.UtcNow);

        var first = character.TryReserveLaborPower(80);
        var second = character.TryReserveLaborPower(21);

        await Assert.That(first).IsTrue();
        await Assert.That(second).IsFalse();
        await Assert.That(character.AvailableLaborPower).IsEqualTo(20);
        await Assert.That(character.LaborPower).IsEqualTo((short)100);
    }

    [Test]
    public async Task ReleaseLaborPowerReservation_MakesLaborAvailableAgain()
    {
        var character = new Character(new UnitCustomModelParams());
        character.InitializeLaborCache(100, DateTime.UtcNow);
        character.TryReserveLaborPower(80);

        character.ReleaseLaborPowerReservation(80);

        await Assert.That(character.AvailableLaborPower).IsEqualTo(100);
        await Assert.That(character.TryReserveLaborPower(100)).IsTrue();
    }

    [Test]
    public async Task ChangeLabor_DoesNotConsumeLaborReservedByAnActiveSkill()
    {
        var character = new Character(new UnitCustomModelParams());
        character.InitializeLaborCache(100, DateTime.UtcNow);
        character.TryReserveLaborPower(80);

        var changed = character.TryChangeLabor(-30, 0);

        await Assert.That(changed).IsFalse();
        await Assert.That(character.LaborPower).IsEqualTo((short)100);
        await Assert.That(character.AvailableLaborPower).IsEqualTo(20);
    }

    [Test]
    public async Task TryReserveLaborPower_IsAtomicAcrossConcurrentRequests()
    {
        var character = new Character(new UnitCustomModelParams());
        character.InitializeLaborCache(100, DateTime.UtcNow);

        var results = await Task.WhenAll(Enumerable.Range(0, 1000)
            .Select(_ => Task.Run(() => character.TryReserveLaborPower(1))));

        await Assert.That(results.Count(result => result)).IsEqualTo(100);
        await Assert.That(character.AvailableLaborPower).IsEqualTo(0);
        await Assert.That(character.LaborPower).IsEqualTo((short)100);
    }
}
