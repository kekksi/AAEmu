using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.UnitTests.Game.Models.Game.Char;

public class CharacterCraftTests
{
    [Test]
    public async Task TryLearnCraft_RejectsDuplicate()
    {
        var character = new Character(new UnitCustomModelParams());
        var craft = new CharacterCraft(character);

        var first = craft.TryLearnCraft(2792);
        var duplicate = craft.TryLearnCraft(2792);

        await Assert.That(first).IsTrue();
        await Assert.That(duplicate).IsFalse();
        await Assert.That(craft.LearnedCraft(2792)).IsTrue();
    }

    [Test]
    public async Task TryLearnCraft_IsAtomicAcrossConcurrentUses()
    {
        var character = new Character(new UnitCustomModelParams());
        var craft = new CharacterCraft(character);

        var results = await Task.WhenAll(Enumerable.Range(0, 100)
            .Select(_ => Task.Run(() => craft.TryLearnCraft(2792))));

        await Assert.That(results.Count(result => result)).IsEqualTo(1);
    }
}
