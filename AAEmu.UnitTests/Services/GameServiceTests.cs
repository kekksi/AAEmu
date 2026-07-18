using AAEmu.Game;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Services.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
namespace AAEmu.UnitTests.Services;

/// <summary>
/// Tests for GameService class
/// </summary>
[NotInParallel]
public class GameServiceTests
{
    [Test]
    public async Task StartTime_IsInitializedToUtcNow()
    {
        var fakeTime = new FakeTimeProvider();
        var sp = Mock.Of<IServiceProvider>().Object;
        var orchestrator = new ManagerOrchestrator(sp, new ServiceCollection());
        using var telemetry = new TelemetryService();
        using var service = new GameService(sp, orchestrator, fakeTime, telemetry);

        await Assert.That(GameService.StartTime).IsEqualTo(fakeTime.GetUtcNow().UtcDateTime);
    }

    [Test]
    public async Task TimeSinceStart_ReturnsTimeSpanSinceStart()
    {
        var fakeTime = new FakeTimeProvider();
        var sp = Mock.Of<IServiceProvider>().Object;
        var orchestrator = new ManagerOrchestrator(sp, new ServiceCollection());
        using var telemetry = new TelemetryService();
        using var service = new GameService(sp, orchestrator, fakeTime, telemetry);

        fakeTime.Advance(TimeSpan.FromSeconds(5));

        await Assert.That(GameService.TimeSinceStart).IsEqualTo(TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task GameService_ImplementsIHostedService()
    {
        var sp = Mock.Of<IServiceProvider>().Object;
        var orchestrator = new ManagerOrchestrator(sp, new ServiceCollection());
        using var telemetry = new TelemetryService();
        using var service = new GameService(sp, orchestrator, TimeProvider.System, telemetry);

        await Assert.That(service).IsAssignableTo<IHostedService>();
    }

    [Test]
    public async Task GameService_ImplementsIDisposable()
    {
        var sp = Mock.Of<IServiceProvider>().Object;
        var orchestrator = new ManagerOrchestrator(sp, new ServiceCollection());
        using var telemetry = new TelemetryService();
        using var service = new GameService(sp, orchestrator, TimeProvider.System, telemetry);

        await Assert.That(service).IsAssignableTo<IDisposable>();
    }

    [Test]
    public async Task Dispose_DoesNotThrow()
    {
        var sp = Mock.Of<IServiceProvider>().Object;
        var orchestrator = new ManagerOrchestrator(sp, new ServiceCollection());
        using var telemetry = new TelemetryService();
        using var service = new GameService(sp, orchestrator, TimeProvider.System, telemetry);

        service.Dispose();
        await Task.CompletedTask; // Suppress warning
    }
}
