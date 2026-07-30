using System.Reflection;

using AAEmu.Commons.IO;
using AAEmu.Commons.Utils;
using AAEmu.Commons.Utils.DB;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Id;
using AAEmu.Game.Core.Managers.Stream;
using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.GameData.Framework;
using AAEmu.Game.Models;
using AAEmu.Game.Models.Game.World;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NLog;
using NLog.Config;

namespace AAEmu.Zone;

/// <summary>
/// B2.2 - Zone-local DI subset + in-process WorldInstance bootstrap.
///
/// The Zone process builds its OWN DI container (a subset of AAEmu.Game.Program),
/// sets <see cref="SingletonContainer.ServiceProvider"/> itself, loads the read-only
/// game data and creates exactly ONE WorldInstance (main_world) in-process.
///
/// It does NOT tick gameplay, does NOT open any client network and does NOT register
/// into the live gameplay. This is purely: DI subset -> load data -> create 1 world.
/// </summary>
public static class ZoneWorldBootstrap
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    // All read-only game data (Config.json, Configurations/, Data/, compact.sqlite3,
    // heightmaps, ClientData) lives in the Game publish dir. FileManager.AppPath keys
    // every data lookup, so we point it there via reflection before anything reads it.
    private const string GameBin = "/root/AAEmu/AAEmu.Game/bin/Release/net10.0";

    public static void Run()
    {
        // Load NLog config from the Game publish dir so manager logs are visible.
        try
        {
            LogManager.ThrowConfigExceptions = false;
            LogManager.Configuration = new XmlLoggingConfiguration(
                Path.Combine(GameBin, "NLog.config"));
        }
        catch { /* keep NLog default */ }

        Logger.Info("[B2.2] Zone world bootstrap starting...");

        // --- 1. Point FileManager.AppPath at the Game publish dir -------------------
        var appPath = GameBin + Path.DirectorySeparatorChar;
        var appPathField = typeof(FileManager).GetField("_appPath",
            BindingFlags.NonPublic | BindingFlags.Static);
        appPathField!.SetValue(null, appPath);
        Logger.Info($"[B2.2] FileManager.AppPath = {FileManager.AppPath}");

        // --- 2. Build configuration from the Game publish dir -----------------------
        var cfgBuilder = new ConfigurationBuilder();
        cfgBuilder.AddJsonFile(Path.Combine(GameBin, "Config.json"), optional: true);
        var cfgDir = Path.Combine(GameBin, "Configurations");
        if (Directory.Exists(cfgDir))
            foreach (var f in Directory.GetFiles(cfgDir, "*.json", SearchOption.AllDirectories).Order())
                cfgBuilder.AddJsonFile(f, optional: true);
        var localCfg = Path.Combine(GameBin, "Config.Local.json");
        if (File.Exists(localCfg))
            cfgBuilder.AddJsonFile(localCfg, optional: true);
        var configurationRoot = cfgBuilder.Build();

        // --- 3. Build the zone-local DI subset --------------------------------------
        IServiceCollection services = new ServiceCollection();
        services.AddOptions();
        services.Configure<AppConfiguration>(configurationRoot);
        services.AddSingleton(TimeProvider.System);

        // ---- BEGIN copied manager registrations (subset of AAEmu.Game.Program) -----
        // NOTE: hosted services (Telemetry/GameService/WebApi/Discord) are intentionally
        // NOT copied. The full manager graph is registered so DI can RESOLVE the world
        // creation closure without the Singleton<T> reflection fallback. Which managers
        // are actually LOADED / TOUCHED is the coupling finding (see report), not which
        // are merely registered.
                services.AddSingleton<AccessLevelManager>();
                services.AddSingleton<IAccessLevelManager>(sp => sp.GetRequiredService<AccessLevelManager>());

                services.AddSingleton<AccountManager>();
                services.AddSingleton<IAccountManager>(sp => sp.GetRequiredService<AccountManager>());

                services.AddSingleton<AIManager>();
                services.AddSingleton<IAIManager>(sp => sp.GetRequiredService<AIManager>());

                services.AddSingleton<AiPathsManager>();
                services.AddSingleton<IAiPathsManager>(sp => sp.GetRequiredService<AiPathsManager>());

                services.AddSingleton<AnimationManager>();
                services.AddSingleton<IAnimationManager>(sp => sp.GetRequiredService<AnimationManager>());

                services.AddSingleton<AuctionManager>();
                services.AddSingleton<IAuctionManager>(sp => sp.GetRequiredService<AuctionManager>());

                services.AddSingleton<CashShopManager>();
                services.AddSingleton<ICashShopManager>(sp => sp.GetRequiredService<CashShopManager>());

                services.AddSingleton<ChatManager>();
                services.AddSingleton<IChatManager>(sp => sp.GetRequiredService<ChatManager>());

                services.AddSingleton<CommandManager>();
                services.AddSingleton<ICommandManager>(sp => sp.GetRequiredService<CommandManager>());

                services.AddSingleton<CraftManager>();
                services.AddSingleton<ICraftManager>(sp => sp.GetRequiredService<CraftManager>());

                services.AddSingleton<CrimeManager>();
                services.AddSingleton<ICrimeManager>(sp => sp.GetRequiredService<CrimeManager>());

                services.AddSingleton<DuelManager>();
                services.AddSingleton<IDuelManager>(sp => sp.GetRequiredService<DuelManager>());

                services.AddSingleton<EffectTaskManager>();
                services.AddSingleton<IEffectTaskManager>(sp => sp.GetRequiredService<EffectTaskManager>());

                services.AddSingleton<ExpeditionManager>();
                services.AddSingleton<IExpeditionManager>(sp => sp.GetRequiredService<ExpeditionManager>());

                services.AddSingleton<ExperienceManager>();
                services.AddSingleton<IExperienceManager>(sp => sp.GetRequiredService<ExperienceManager>());

                services.AddSingleton<ExpressTextManager>();
                services.AddSingleton<IExpressTextManager>(sp => sp.GetRequiredService<ExpressTextManager>());

                services.AddSingleton<FamilyManager>();
                services.AddSingleton<IFamilyManager>(sp => sp.GetRequiredService<FamilyManager>());

                services.AddSingleton<FeaturesManager>();
                services.AddSingleton<IFeaturesManager>(sp => sp.GetRequiredService<FeaturesManager>());

                services.AddSingleton<FishSchoolManager>();
                services.AddSingleton<IFishSchoolManager>(sp => sp.GetRequiredService<FishSchoolManager>());

                services.AddSingleton<FormulaManager>();
                services.AddSingleton<IFormulaManager>(sp => sp.GetRequiredService<FormulaManager>());

                services.AddSingleton<FriendMananger>();
                services.AddSingleton<IFriendManager>(sp => sp.GetRequiredService<FriendMananger>());

                services.AddSingleton<GameScheduleManager>();
                services.AddSingleton<IGameScheduleManager>(sp => sp.GetRequiredService<GameScheduleManager>());

                services.AddSingleton<HousingManager>();
                services.AddSingleton<IHousingManager>(sp => sp.GetRequiredService<HousingManager>());

                services.AddSingleton<IndunManager>();
                services.AddSingleton<IIndunManager>(sp => sp.GetRequiredService<IndunManager>());

                services.AddSingleton<InstantGameManager>();
                services.AddSingleton<IInstantGameManager>(sp => sp.GetRequiredService<InstantGameManager>());

                services.AddSingleton<ItemManager>();
                services.AddSingleton<IItemManager>(sp => sp.GetRequiredService<ItemManager>());

                services.AddSingleton<LocalizationManager>();
                services.AddSingleton<ILocalizationManager>(sp => sp.GetRequiredService<LocalizationManager>());

                services.AddSingleton<MailManager>();
                services.AddSingleton<IMailManager>(sp => sp.GetRequiredService<MailManager>());

                services.AddSingleton<ManaRegenManager>();
                services.AddSingleton<IManaRegenManager>(sp => sp.GetRequiredService<ManaRegenManager>());

                services.AddSingleton<ModelManager>();
                services.AddSingleton<IModelManager>(sp => sp.GetRequiredService<ModelManager>());

                services.AddSingleton<MusicManager>();
                services.AddSingleton<IMusicManager>(sp => sp.GetRequiredService<MusicManager>());

                services.AddSingleton<NameManager>();
                services.AddSingleton<INameManager>(sp => sp.GetRequiredService<NameManager>());

                services.AddSingleton<PlotManager>();
                services.AddSingleton<IPlotManager>(sp => sp.GetRequiredService<PlotManager>());

                services.AddSingleton<PortalManager>();
                services.AddSingleton<IPortalManager>(sp => sp.GetRequiredService<PortalManager>());

                services.AddSingleton<PublicFarmManager>();
                services.AddSingleton<IPublicFarmManager>(sp => sp.GetRequiredService<PublicFarmManager>());

                services.AddSingleton<QuestManager>();
                services.AddSingleton<IQuestManager>(sp => sp.GetRequiredService<QuestManager>());

                services.AddSingleton<RadarManager>();
                services.AddSingleton<IRadarManager>(sp => sp.GetRequiredService<RadarManager>());

                services.AddSingleton<SaveManager>();
                services.AddSingleton<ISaveManager>(sp => sp.GetRequiredService<SaveManager>());

                services.AddSingleton<ShipyardManager>();
                services.AddSingleton<IShipyardManager>(sp => sp.GetRequiredService<ShipyardManager>());

                services.AddSingleton<SkillManager>();
                services.AddSingleton<ISkillManager>(sp => sp.GetRequiredService<SkillManager>());

                services.AddSingleton<SubZoneManager>();
                services.AddSingleton<ISubZoneManager>(sp => sp.GetRequiredService<SubZoneManager>());

                services.AddSingleton<SusManager>();
                services.AddSingleton<ISusManager>(sp => sp.GetRequiredService<SusManager>());

                services.AddSingleton<TaskManager>();
                services.AddSingleton<ITaskManager>(sp => sp.GetRequiredService<TaskManager>());

                services.AddSingleton<TaxationsManager>();
                services.AddSingleton<ITaxationsManager>(sp => sp.GetRequiredService<TaxationsManager>());

                services.AddSingleton<TeamManager>();
                services.AddSingleton<ITeamManager>(sp => sp.GetRequiredService<TeamManager>());

                services.AddSingleton<TickManager>();
                services.AddSingleton<ITickManager>(sp => sp.GetRequiredService<TickManager>());

                services.AddSingleton<TimedRewardsManager>();
                services.AddSingleton<ITimedRewardsManager>(sp => sp.GetRequiredService<TimedRewardsManager>());

                services.AddSingleton<TimeManager>();
                services.AddSingleton<ITimeManager>(sp => sp.GetRequiredService<TimeManager>());

                services.AddSingleton<TradeManager>();
                services.AddSingleton<ITradeManager>(sp => sp.GetRequiredService<TradeManager>());

                // -- Singleton<T>-based managers (AAEmu.Game.Core.Managers.UnitManagers) --
                services.AddSingleton<CharacterManager>();
                services.AddSingleton<ICharacterManager>(sp => sp.GetRequiredService<CharacterManager>());

                services.AddSingleton<DoodadManager>();
                services.AddSingleton<IDoodadManager>(sp => sp.GetRequiredService<DoodadManager>());

                services.AddSingleton<NpcManager>();
                services.AddSingleton<INpcManager>(sp => sp.GetRequiredService<NpcManager>());

                services.AddSingleton<TrialManager>();
                services.AddSingleton<ITrialManager>(sp => sp.GetRequiredService<TrialManager>());

                services.AddSingleton<CrimeManager>();
                services.AddSingleton<ICrimeManager>(sp => sp.GetRequiredService<CrimeManager>());

                // -- Singleton<T>-based managers (AAEmu.Game.Core.Managers.World) --
                services.AddSingleton<AreaTriggerManager>();
                services.AddSingleton<IAreaTriggerManager>(sp => sp.GetRequiredService<AreaTriggerManager>());

                services.AddSingleton<EnterWorldManager>();
                services.AddSingleton<IEnterWorldManager>(sp => sp.GetRequiredService<EnterWorldManager>());

                services.AddSingleton<FactionManager>();
                services.AddSingleton<IFactionManager>(sp => sp.GetRequiredService<FactionManager>());

                services.AddSingleton<SpecialtyManager>();
                services.AddSingleton<ISpecialtyManager>(sp => sp.GetRequiredService<SpecialtyManager>());

                services.AddSingleton<StreamManager>();
                services.AddSingleton<IStreamManager>(sp => sp.GetRequiredService<StreamManager>());

                services.AddSingleton<WorldManager>();
                services.AddSingleton<IWorldManager>(sp => sp.GetRequiredService<WorldManager>());

                services.AddSingleton<ZoneManager>();
                services.AddSingleton<IZoneManager>(sp => sp.GetRequiredService<ZoneManager>());

                // -- Singleton<T>-based managers (AAEmu.Game.Core.Managers.Stream) --
                services.AddSingleton<UccManager>();
                services.AddSingleton<IUccManager>(sp => sp.GetRequiredService<UccManager>());

                // -- Singleton<T>-based managers (AAEmu.Game.GameData.Framework) --
                services.AddSingleton<GameDataManager>();
                services.AddSingleton<IGameDataManager>(sp => sp.GetRequiredService<GameDataManager>());

                // -- IdManagers (own static Instance, factory pattern) --
                services.AddSingleton<AuctionIdManager>();
                services.AddSingleton<IAuctionIdManager>(sp => sp.GetRequiredService<AuctionIdManager>());

                services.AddSingleton<ChatIdManager>();
                services.AddSingleton<IChatIdManager>(sp => sp.GetRequiredService<ChatIdManager>());

                services.AddSingleton<CharacterIdManager>();
                services.AddSingleton<ICharacterIdManager>(sp => sp.GetRequiredService<CharacterIdManager>());

                services.AddSingleton<ContainerIdManager>();
                services.AddSingleton<IContainerIdManager>(sp => sp.GetRequiredService<ContainerIdManager>());

                services.AddSingleton<DoodadIdManager>();
                services.AddSingleton<IDoodadIdManager>(sp => sp.GetRequiredService<DoodadIdManager>());

                services.AddSingleton<CrimeIdManager>();
                services.AddSingleton<ICrimeIdManager>(sp => sp.GetRequiredService<CrimeIdManager>());

                services.AddSingleton<TrialIdManager>();
                services.AddSingleton<ITrialIdManager>(sp => sp.GetRequiredService<ITrialIdManager>());

                services.AddSingleton<ExpeditionIdManager>();
                services.AddSingleton<IExpeditionIdManager>(sp => sp.GetRequiredService<ExpeditionIdManager>());

                services.AddSingleton<FamilyIdManager>();
                services.AddSingleton<IFamilyIdManager>(sp => sp.GetRequiredService<FamilyIdManager>());

                services.AddSingleton<FriendIdManager>();
                services.AddSingleton<IFriendIdManager>(sp => sp.GetRequiredService<FriendIdManager>());

                services.AddSingleton<GimmickIdManager>();
                services.AddSingleton<IGimmickIdManager>(sp => sp.GetRequiredService<GimmickIdManager>());

                services.AddSingleton<HousingIdManager>();
                services.AddSingleton<IHousingIdManager>(sp => sp.GetRequiredService<HousingIdManager>());

                services.AddSingleton<HousingTldManager>();
                services.AddSingleton<IHousingTldManager>(sp => sp.GetRequiredService<HousingTldManager>());

                services.AddSingleton<ItemIdManager>();
                services.AddSingleton<IItemIdManager>(sp => sp.GetRequiredService<ItemIdManager>());

                services.AddSingleton<MailIdManager>();
                services.AddSingleton<IMailIdManager>(sp => sp.GetRequiredService<MailIdManager>());

                services.AddSingleton<MateIdManager>();
                services.AddSingleton<IMateIdManager>(sp => sp.GetRequiredService<MateIdManager>());

                services.AddSingleton<MusicIdManager>();
                services.AddSingleton<IMusicIdManager>(sp => sp.GetRequiredService<MusicIdManager>());

                services.AddSingleton<ObjectIdManager>();
                services.AddSingleton<IObjectIdManager>(sp => sp.GetRequiredService<ObjectIdManager>());

                services.AddSingleton<PrivateBookIdManager>();
                services.AddSingleton<IPrivateBookIdManager>(sp => sp.GetRequiredService<PrivateBookIdManager>());

                services.AddSingleton<QuestIdManager>();
                services.AddSingleton<IQuestIdManager>(sp => sp.GetRequiredService<QuestIdManager>());

                services.AddSingleton<ShipyardIdManager>();
                services.AddSingleton<IShipyardIdManager>(sp => sp.GetRequiredService<ShipyardIdManager>());

                services.AddSingleton<TaskIdManager>();
                services.AddSingleton<ITaskIdManager>(sp => sp.GetRequiredService<TaskIdManager>());

                services.AddSingleton<TeamIdManager>();
                services.AddSingleton<ITeamIdManager>(sp => sp.GetRequiredService<TeamIdManager>());

                services.AddSingleton<TlIdManager>();
                services.AddSingleton<ITlIdManager>(sp => sp.GetRequiredService<TlIdManager>());

                services.AddSingleton<TradeIdManager>();
                services.AddSingleton<ITradeIdManager>(sp => sp.GetRequiredService<TradeIdManager>());

                services.AddSingleton<UccIdManager>();
                services.AddSingleton<IUccIdManager>(sp => sp.GetRequiredService<UccIdManager>());

                services.AddSingleton<VisitedSubZoneIdManager>();
                services.AddSingleton<IVisitedSubZoneIdManager>(sp => sp.GetRequiredService<VisitedSubZoneIdManager>());

                services.AddSingleton<WorldIdManager>();
                services.AddSingleton<IWorldIdManager>(sp => sp.GetRequiredService<WorldIdManager>());

                // -- Lazy<T> wrappers for circular-dep break patterns --
                // MS DI does NOT auto-wrap registered types in Lazy<T>; they must be registered explicitly.
                // WorldManager takes these three as Lazy<T> to break the circular dependency.
                services.AddSingleton(sp => new Lazy<IZoneManager>(sp.GetRequiredService<IZoneManager>));
                services.AddSingleton(sp => new Lazy<IIndunManager>(sp.GetRequiredService<IIndunManager>));
                services.AddSingleton(sp => new Lazy<IFamilyManager>(sp.GetRequiredService<IFamilyManager>));
                // NameManager ↔ CharacterManager circular dep: NameManager takes Lazy<ICharacterManager>.
                services.AddSingleton(sp => new Lazy<ICharacterManager>(sp.GetRequiredService<ICharacterManager>));
                // HousingManager ↔ MailManager circular dep: MailManager takes Lazy<IHousingManager>.
                services.AddSingleton(sp => new Lazy<IHousingManager>(sp.GetRequiredService<IHousingManager>));

                // -- Orchestrator --
                // Expose the ServiceCollection itself so ManagerOrchestrator can inspect registrations.
                services.AddSingleton(_ => services);
                services.AddSingleton<ManagerOrchestrator>();
        // ---- END copied manager registrations --------------------------------------

        var provider = services.BuildServiceProvider();
        SingletonContainer.ServiceProvider = provider;
        Logger.Info("[B2.2] SingletonContainer.ServiceProvider set to zone-local subset.");

        // --- 4. DB configuration ----------------------------------------------------
        MySQL.SetConfiguration(AppConfiguration.Instance.Connections.MySQLProvider);

        // --- 5. Client data sources -------------------------------------------------
        AAEmu.Game.IO.ClientFileManager.Initialize();
        Logger.Info($"[B2.2] ClientData sources loaded: {AAEmu.Game.IO.ClientFileManager.Sources.Count}");
        if (AAEmu.Game.IO.ClientFileManager.Sources.Count == 0)
            throw new InvalidOperationException("No client data sources - cannot load world data.");

        // --- 6. Read-only data load (exception-swept) -------------------------------
        // CreateWorldInstance(main_world) -> SpawnManager.Load touches the whole
        // read-only data tier: NpcManager/DoodadManager (.Exist on every spawner),
        // the *GameData tables (Npc/Doodad/Transfer/Gimmick/Slave) and, for main_world,
        // HousingManager (LoadPlayerHousing/SpawnPersistentDoodads - read-only because
        // World.UsePersistentHouseDoodads is false, so no ReconcileBoundDoodads writes).
        // We therefore run the same dependency-ordered load the monolith uses, minus
        // all network / hosted-service / tick startup. Every Load() is a pure data read.
        Logger.Info("[B2.2] Loading FormulaManager + ItemManager...");
        FormulaManager.Instance.Load();
        ItemManager.Instance.Load();

        Logger.Info("[B2.2] Running ManagerOrchestrator.RunLoadAsync() (read-only data tier)...");
        var orchestrator = provider.GetRequiredService<ManagerOrchestrator>();
        orchestrator.RunLoadAsync().GetAwaiter().GetResult();
        Logger.Info("[B2.2] Data load complete. PostLoadGameData()...");
        GameDataManager.Instance.PostLoadGameData();

        // --- 7. Create exactly ONE WorldInstance (main_world) -----------------------
        var tmpl = WorldManager.Instance.GetWorldTemplateByName("main_world");
        if (tmpl == null)
            throw new InvalidOperationException("main_world template not found after WorldManager.Load().");
        Logger.Info($"[B2.2] Creating WorldInstance for main_world (templateId={tmpl.Id})...");

        var world = WorldManager.Instance.CreateWorldInstance(tmpl, 0, true, 0);

        // --- 8. Report ---------------------------------------------------------------
        var regionsX = world.Regions?.GetLength(0) ?? 0;
        var regionsY = world.Regions?.GetLength(1) ?? 0;
        var npcSpawners = world.SpawnManager?.GetAllSpawners()?.Values.Sum(l => l.Count) ?? 0;
        int CountPrivate(string field)
        {
            var fi = typeof(AAEmu.Game.Core.Managers.World.SpawnManager).GetField(field,
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (fi?.GetValue(world.SpawnManager) is System.Collections.ICollection c) return c.Count;
            return -1;
        }
        var doodadSpawners = CountPrivate("<DoodadSpawners>k__BackingField");
        var transferSpawners = CountPrivate("<TransferSpawners>k__BackingField");
        var gimmickSpawners = CountPrivate("<GimmickSpawners>k__BackingField");
        var slaveSpawners = CountPrivate("<SlaveSpawners>k__BackingField");

        Logger.Info($"[B2.2] WorldInstance main_world created OK: Id={world.Id} " +
                    $"regions={regionsX}x{regionsY} physics={(world.Physics != null)} " +
                    $"npcSpawners={npcSpawners} doodadSpawners={doodadSpawners} " +
                    $"transferSpawners={transferSpawners} gimmickSpawners={gimmickSpawners} " +
                    $"slaveSpawners={slaveSpawners}");
        Logger.Info("[B2.2] Zone world bootstrap DONE (no tick, no client network).");
    }
}
