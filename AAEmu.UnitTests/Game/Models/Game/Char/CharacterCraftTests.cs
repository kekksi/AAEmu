using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Crafts;
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

    [Test]
    public async Task TryStartCraft_AllowsOnlyOneConcurrentProductPath()
    {
        var character = new Character(new UnitCustomModelParams());
        using var requestsReady = new Barrier(3);
        using var productPathEntered = new ManualResetEventSlim();
        using var releaseProductPath = new ManualResetEventSlim();
        var productPathRuns = 0;
        var craft = new CharacterCraft(character, (_, _, _) =>
        {
            Interlocked.Increment(ref productPathRuns);
            productPathEntered.Set();
            releaseProductPath.Wait();
        });

        Task<bool> SendRequest(uint craftId) => Task.Run(() =>
        {
            requestsReady.SignalAndWait();
            return craft.TryStartCraft(new Craft { Id = craftId }, 1, craftId);
        });

        var firstRequest = SendRequest(100);
        var secondRequest = SendRequest(200);
        requestsReady.SignalAndWait();
        productPathEntered.Wait();
        releaseProductPath.Set();

        var results = await Task.WhenAll(firstRequest, secondRequest);

        await Assert.That(results.Count(result => result)).IsEqualTo(1);
        await Assert.That(productPathRuns).IsEqualTo(1);
    }

    [Test]
    public async Task ReleaseCraftGuardOnDisconnect_AllowsNextRequest()
    {
        var character = new Character(new UnitCustomModelParams());
        var craftStarts = 0;
        var craft = new CharacterCraft(character, (_, _, _) => Interlocked.Increment(ref craftStarts));

        var first = craft.TryStartCraft(new Craft { Id = 100 }, 1, 100);
        craft.ReleaseCraftGuardOnDisconnect();
        var afterDisconnect = craft.TryStartCraft(new Craft { Id = 200 }, 1, 200);

        await Assert.That(first).IsTrue();
        await Assert.That(afterDisconnect).IsTrue();
        await Assert.That(craftStarts).IsEqualTo(2);
    }
}
