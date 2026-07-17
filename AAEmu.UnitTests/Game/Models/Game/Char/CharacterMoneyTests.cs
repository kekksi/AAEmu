using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.UnitTests.Game.Models.Game.Char;

public class CharacterMoneyTests
{
    [Test]
    public async Task TrySpendMoney_DoesNotAllowOverdraft()
    {
        var character = new Character(new UnitCustomModelParams()) { Money = 100 };

        var result = character.TrySpendMoney(SlotType.Inventory, 101);

        await Assert.That(result).IsFalse();
        await Assert.That(character.Money).IsEqualTo(100);
    }

    [Test]
    public async Task TrySpendMoney_RejectsNegativeAmounts()
    {
        var character = new Character(new UnitCustomModelParams()) { Money = 100 };

        var result = character.TrySpendMoney(SlotType.Inventory, -1);

        await Assert.That(result).IsFalse();
        await Assert.That(character.Money).IsEqualTo(100);
    }

    [Test]
    public async Task TryAddMoney_DoesNotOverflow()
    {
        var character = new Character(new UnitCustomModelParams()) { Money = long.MaxValue };

        var result = character.TryAddMoney(SlotType.Inventory, 1);

        await Assert.That(result).IsFalse();
        await Assert.That(character.Money).IsEqualTo(long.MaxValue);
    }

    [Test]
    public async Task TryTransferMoney_IsAtomicWhenDestinationWouldOverflow()
    {
        var character = new Character(new UnitCustomModelParams())
        {
            Money = 100,
            Money2 = long.MaxValue
        };

        var result = character.TryTransferMoney(SlotType.Inventory, SlotType.Bank, 1);

        await Assert.That(result).IsFalse();
        await Assert.That(character.Money).IsEqualTo(100);
        await Assert.That(character.Money2).IsEqualTo(long.MaxValue);
    }

    [Test]
    public async Task TrySpendMoney_IsAtomicAcrossConcurrentRequests()
    {
        var character = new Character(new UnitCustomModelParams()) { Money = 100 };

        var results = await Task.WhenAll(Enumerable.Range(0, 1000)
            .Select(_ => Task.Run(() => character.TrySpendMoney(SlotType.Inventory, 1))));

        await Assert.That(results.Count(result => result)).IsEqualTo(100);
        await Assert.That(character.Money).IsEqualTo(0);
    }

    [Test]
    public async Task ChangeMoney_HandlesIntMinValueWithoutNegationOverflow()
    {
        var character = new Character(new UnitCustomModelParams()) { Money = 100 };

        var result = character.ChangeMoney(SlotType.Inventory, int.MinValue);

        await Assert.That(result).IsFalse();
        await Assert.That(character.Money).IsEqualTo(100);
    }

    [Test]
    public async Task TryExchangeMoney_UpdatesBothCharactersAtomically()
    {
        var first = new Character(new UnitCustomModelParams()) { Money = 100 };
        var second = new Character(new UnitCustomModelParams()) { Money = 50 };

        var result = Character.TryExchangeMoney(first, second, 70, 20);

        await Assert.That(result).IsTrue();
        await Assert.That(first.Money).IsEqualTo(50);
        await Assert.That(second.Money).IsEqualTo(100);
    }

    [Test]
    public async Task TryExchangeMoney_DoesNotPartiallyUpdateOnOverflow()
    {
        var first = new Character(new UnitCustomModelParams()) { Money = long.MaxValue };
        var second = new Character(new UnitCustomModelParams()) { Money = 1 };

        var result = Character.TryExchangeMoney(first, second, 0, 1);

        await Assert.That(result).IsFalse();
        await Assert.That(first.Money).IsEqualTo(long.MaxValue);
        await Assert.That(second.Money).IsEqualTo(1);
    }
}
