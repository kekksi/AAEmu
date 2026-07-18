using System.Collections.Concurrent;

using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Quests.Static;
using AAEmu.Game.Models.Game.Quests.Templates;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Game.Quests.Acts;

public class QuestActCheckGuard(QuestComponentTemplate parentComponent) : QuestActTemplate(parentComponent)
{
    private sealed record GuardSubscription(Unit Guard, EventHandler<OnDeathArgs> DeathHandler);

    private readonly ConcurrentDictionary<QuestAct, GuardSubscription> _guardSubscriptions = [];

    public uint NpcId { get; set; }

    public override bool RunAct(Quest quest, QuestAct questAct, int currentObjectiveCount)
    {
        var guard = TryBindGuard(quest, questAct);
        if (guard == null)
        {
            Logger.Warn($"{QuestActTemplateName}({DetailId}).RunAct: Quest {quest.TemplateId}, Owner {quest.Owner.Name} ({quest.Owner.Id}), guard NPC {NpcId} is not active");
            return false;
        }

        return !guard.IsDead;
    }

    public override void InitializeQuest(Quest quest, QuestAct questAct)
    {
        base.InitializeQuest(quest, questAct);
        quest.Owner.Events.OnQuestStepChanged += questAct.OnQuestStepChanged;
        TryBindGuard(quest, questAct);
    }

    public override void InitializeAction(Quest quest, QuestAct questAct)
    {
        base.InitializeAction(quest, questAct);
        TryBindGuard(quest, questAct);
    }

    public override void FinalizeAction(Quest quest, QuestAct questAct)
    {
        // Guard checks can be declared in an earlier component but protect the NPC
        // until the quest reaches a terminal state, so the death hook stays active.
        base.FinalizeAction(quest, questAct);
    }

    public override void FinalizeQuest(Quest quest, QuestAct questAct)
    {
        CleanupLifecycle(quest, questAct);
        base.FinalizeQuest(quest, questAct);
    }

    public override void OnQuestStepChanged(QuestAct questAct, object sender, OnQuestStepChangedArgs args)
    {
        var quest = questAct.QuestComponent.Parent.Parent;
        if (args.QuestId != quest.TemplateId)
            return;

        if (args.Step is QuestComponentKind.Fail or QuestComponentKind.Ready or QuestComponentKind.Drop or QuestComponentKind.Reward)
            CleanupLifecycle(quest, questAct);
    }

    private Unit TryBindGuard(Quest quest, QuestAct questAct)
    {
        if (_guardSubscriptions.TryGetValue(questAct, out var existing))
            return existing.Guard;

        if (quest.Owner is not Character owner || owner.ParentWorld == null)
            return null;

        var guard = owner.ParentWorld.GetNpcByTemplateId(NpcId);
        if (guard == null)
            return null;

        EventHandler<OnDeathArgs> deathHandler = (_, args) => OnGuardDeath(questAct, args);
        var subscription = new GuardSubscription(guard, deathHandler);
        if (_guardSubscriptions.TryAdd(questAct, subscription))
        {
            guard.Events.OnDeath += deathHandler;
            return guard;
        }

        return _guardSubscriptions.TryGetValue(questAct, out existing) ? existing.Guard : null;
    }

    private void OnGuardDeath(QuestAct questAct, OnDeathArgs args)
    {
        if (!_guardSubscriptions.TryGetValue(questAct, out var subscription) ||
            !ReferenceEquals(args.Victim, subscription.Guard))
            return;

        var quest = questAct.QuestComponent.Parent.Parent;
        if (!quest.Owner.Quests.ActiveQuests.TryGetValue(quest.TemplateId, out var activeQuest) ||
            !ReferenceEquals(activeQuest, quest))
            return;

        Logger.Warn($"{QuestActTemplateName}({DetailId}): guard NPC {NpcId} died; failing Quest {quest.TemplateId} for {quest.Owner.Name} ({quest.Owner.Id})");
        QuestManager.Instance.FailQuest(quest.Owner, quest.TemplateId);
    }

    private void CleanupLifecycle(Quest quest, QuestAct questAct)
    {
        quest.Owner.Events.OnQuestStepChanged -= questAct.OnQuestStepChanged;
        if (_guardSubscriptions.TryRemove(questAct, out var subscription))
            subscription.Guard.Events.OnDeath -= subscription.DeathHandler;
    }
}
