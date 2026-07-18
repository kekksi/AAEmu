using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game.Items.Actions;
using AAEmu.Game.Models.Game.Quests.Templates;
using AAEmu.Game.Models.Game.Units;

#pragma warning disable IDE0130 // Namespace does not match folder structure

namespace AAEmu.Game.Models.Game.Quests.Acts;

public class QuestActObjItemGroupGather(QuestComponentTemplate parentComponent) : QuestActTemplate(parentComponent)
{
    public override bool CountsAsAnObjective => true;
    public uint ItemGroupId { get; set; }
    public bool Cleanup { get; set; }
    public uint HighlightDoodadId { get; set; }
    public int HighlightDoodadPhase { get; set; }
    public bool UseAlias { get; set; }
    public uint QuestActObjAliasId { get; set; }
    public bool DropWhenDestroy { get; set; }
    public bool DestroyWhenDrop { get; set; }

    public override bool RunAct(Quest quest, QuestAct questAct, int currentObjectiveCount)
    {
        Logger.Debug($"{QuestActTemplateName}({DetailId}).RunAct: Quest {quest.TemplateId}, Owner {quest.Owner.Name} ({quest.Owner.Id}), ItemGroupId {ItemGroupId}, Count {currentObjectiveCount}/{Count}");
        SetObjective(quest, GetOwnedGroupItemCount(quest));
        return GetObjective(quest) >= Count;
    }

    public override void InitializeAction(Quest quest, QuestAct questAct)
    {
        base.InitializeAction(quest, questAct);
        SetObjective(quest, GetOwnedGroupItemCount(quest));
        quest.Owner.Events.OnItemGroupGather += questAct.OnItemGroupGather;
    }

    public override void FinalizeAction(Quest quest, QuestAct questAct)
    {
        quest.Owner.Events.OnItemGroupGather -= questAct.OnItemGroupGather;
        base.FinalizeAction(quest, questAct);
    }

    public override void QuestCleanup(Quest quest)
    {
        base.QuestCleanup(quest);
        if (Cleanup)
            ConsumeGroupItems(quest, Math.Min(GetObjective(quest), MaxObjective()));
    }

    public override void QuestDropped(Quest quest)
    {
        base.QuestDropped(quest);
        if (DestroyWhenDrop)
            ConsumeGroupItems(quest, Math.Min(GetObjective(quest), MaxObjective()));
    }

    public override void OnItemGroupGather(QuestAct questAct, object sender, OnItemGroupGatherArgs args)
    {
        if (questAct.Id != ActId || args.ItemGroupId != ItemGroupId)
            return;

        SetObjective(questAct, GetOwnedGroupItemCount(questAct.QuestComponent.Parent.Parent));
    }

    private int GetOwnedGroupItemCount(Quest quest)
    {
        long total = 0;
        foreach (var itemId in QuestManager.Instance.GetGroupItems(ItemGroupId))
        {
            total += quest.Owner.Inventory.GetItemsCount(itemId);
            if (total >= int.MaxValue)
                return int.MaxValue;
        }

        return (int)total;
    }

    private void ConsumeGroupItems(Quest quest, int count)
    {
        var remaining = count;
        foreach (var itemId in QuestManager.Instance.GetGroupItems(ItemGroupId))
        {
            if (remaining <= 0)
                break;

            var available = quest.Owner.Inventory.GetItemsCount(itemId);
            if (available <= 0)
                continue;

            var consumed = quest.Owner.Inventory.ConsumeItem(
                null,
                ItemTaskType.QuestRemoveSupplies,
                itemId,
                Math.Min(available, remaining),
                null);
            remaining -= consumed;
        }
    }
}
