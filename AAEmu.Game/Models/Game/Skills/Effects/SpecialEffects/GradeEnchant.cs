using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Formulas;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.Items.Actions;
using AAEmu.Game.Models.Game.Items.Templates;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Game.Skills.Effects.SpecialEffects;

public class GradeEnchant : SpecialEffectAction
{
    protected override SpecialType SpecialEffectActionType => SpecialType.GradeEnchant;

    private enum GradeEnchantResult
    {
        Break = 0,
        Downgrade = 1,
        Fail = 2,
        Success = 3,
        GreatSuccess = 4
    }

    public override void Execute(BaseUnit caster,
        SkillCaster casterObj,
        BaseUnit target,
        SkillCastTarget targetObj,
        CastAction castObj,
        Skill skill,
        SkillObject skillObject,
        DateTime time,
        int value1,
        int value2,
        int value3,
        int value4)
    {
        if (caster is Character) { Logger.Debug("Special effects: GradeEnchant value1 {0}, value2 {1}, value3 {2}, value4 {3}", value1, value2, value3, value4); }

        // Get Player
        if (caster is not Character character || character is null)
        {
            return;
        }

        // Get Regrade Scroll Item
        if (casterObj is not SkillItem scroll || scroll is null)
        {
            Reject(character, skill, ErrorMessageType.InternalError, "missing regrade scroll caster");
            return;
        }

        // Get Item to regrade
        if (targetObj is not SkillCastItemTarget itemTarget || itemTarget is null)
        {
            Reject(character, skill, ErrorMessageType.NotEnoughRequiredItem, "missing regrade target");
            return;
        }

        // Check Charm
        var useCharm = false;
        SkillObjectItemGradeEnchantingSupport charm = null;
        if (skillObject is SkillObjectItemGradeEnchantingSupport)
        {
            charm = (SkillObjectItemGradeEnchantingSupport)skillObject;
            if (charm != null && charm.SupportItemId != 0)
            {
                useCharm = true;
            }
        }

        var isLucky = value1 != 0;
        var item = character.Inventory.GetItemById(itemTarget.Id);
        if (item == null)
        {
            Reject(character, skill, ErrorMessageType.NotEnoughRequiredItem, $"target item {itemTarget.Id} was not found");
            return;
        }
        var initialGrade = item.Grade;
        var gradeTemplate = ItemManager.Instance.GetGradeTemplate(item.Grade);
        if (gradeTemplate == null)
        {
            Reject(character, skill, ErrorMessageType.InternalError, $"item {item.Id} has unknown grade {item.Grade}");
            return;
        }

        var tasks = new List<ItemTask>();

        var cost = GoldCost(gradeTemplate, item, value3);
        if (cost == -1)
        {
            Reject(character, skill, ErrorMessageType.InternalError, $"item {item.Id} has no regrade cost data");
            return;
        }

        if (!character.Inventory.CheckItems(SlotType.Inventory, scroll.ItemTemplateId, 1))
        {
            Reject(character, skill, ErrorMessageType.NotEnoughRequiredItem, $"scroll {scroll.ItemTemplateId} is not in inventory");
            return;
        }

        ItemGradeEnchantingSupport charmInfo = null;
        Item charmItem = null;
        if (useCharm)
        {
            charmItem = character.Inventory.GetItemById(charm.SupportItemId);
            if (charmItem == null)
            {
                Reject(character, skill, ErrorMessageType.NotEnoughRequiredItem, $"support item {charm.SupportItemId} was not found");
                return;
            }

            charmInfo = ItemManager.Instance.GetItemGradEnchantingSupportByItemId(charmItem.TemplateId);
            if (charmInfo == null)
            {
                Reject(character, skill, ErrorMessageType.NotEnoughRequiredItem, $"support item {charmItem.TemplateId} has no regrade support data");
                return;
            }

            if (charmInfo.RequireGradeMin != -1 && item.Grade < charmInfo.RequireGradeMin)
            {
                Reject(character, skill, ErrorMessageType.NotEnoughRequiredItem, $"support item {charmItem.TemplateId} requires grade {charmInfo.RequireGradeMin}+");
                return;
            }

            if (charmInfo.RequireGradeMax != -1 && item.Grade > charmInfo.RequireGradeMax)
            {
                Reject(character, skill, ErrorMessageType.GradeEnchantMax, $"support item {charmItem.TemplateId} supports up to grade {charmInfo.RequireGradeMax}");
                return;
            }

            // tasksRemove.Add(InventoryHelper.GetTaskAndRemoveItem(character, charmItem, 1));
        }

        if (!character.TrySpendMoney(SlotType.Inventory, cost, ItemTaskType.GradeEnchant))
        {
            Reject(character, skill, ErrorMessageType.NotEnoughMoney, $"needs {cost} copper, has {character.Money}");
            return;
        }

        // All seems to be in order, roll item, consume items and send the results
        var result = RollRegrade(gradeTemplate, item, isLucky, useCharm, charmInfo);
        if (result == GradeEnchantResult.Break)
        {
            // Poof
            item._holdingContainer.RemoveItem(ItemTaskType.GradeEnchant, item, true);
        }
        else
        {
            // No Poof
            character.SendPacket(new SCItemTaskSuccessPacket(ItemTaskType.GradeEnchant, [new ItemGradeChange(item, item.Grade)], []));
        }

        // Consume
        // TODO: Handled by skill already, do more tests
        // character.Inventory.PlayerInventory.ConsumeItem(ItemTaskType.GradeEnchant, scroll.ItemTemplateId, 1, character.Inventory.GetItemById(scroll.ItemId));
        if (useCharm)
            character.Inventory.Bag.ConsumeItem(ItemTaskType.GradeEnchant, charmItem.TemplateId, 1, charmItem);

        character.SendPacket(new SCGradeEnchantResultPacket((byte)result, item, initialGrade, item.Grade));
        character.BroadcastPacket(new SCSkillEndedPacket(skill.TlId), true);

        // Let the world know if we got lucky enough
        if (item.Grade >= 8 && (result == GradeEnchantResult.Success || result == GradeEnchantResult.GreatSuccess))
        {
            WorldManager.Instance.BroadcastPacketToServer(
                new SCGradeEnchantBroadcastPacket(character.Name, (byte)result, item, initialGrade, item.Grade));
        }
    }

    private static GradeEnchantResult RollRegrade(GradeTemplate gradeTemplate, Item item, bool isLucky, bool useCharm,
        ItemGradeEnchantingSupport charmInfo)
    {
        var successRoll = Random.Shared.Next(0, 10000);
        var breakRoll = Random.Shared.Next(0, 10000);
        var downgradeRoll = Random.Shared.Next(0, 10000);
        var greatSuccessRoll = Random.Shared.Next(0, 10000);

        // TODO : Refactor
        var successChance = useCharm
            ? GetCharmChance(gradeTemplate.EnchantSuccessRatio, charmInfo.AddSuccessRatio, charmInfo.AddSuccessMul)
            : gradeTemplate.EnchantSuccessRatio;
        var greatSuccessChance = useCharm
            ? GetCharmChance(gradeTemplate.EnchantGreatSuccessRatio, charmInfo.AddGreatSuccessRatio,
                charmInfo.AddGreatSuccessMul)
            : gradeTemplate.EnchantGreatSuccessRatio;
        var breakChance = useCharm
            ? GetCharmChance(gradeTemplate.EnchantBreakRatio, charmInfo.AddBreakRatio, charmInfo.AddBreakMul)
            : gradeTemplate.EnchantBreakRatio;
        var downgradeChance = useCharm
            ? GetCharmChance(gradeTemplate.EnchantDowngradeRatio, charmInfo.AddDowngradeRatio,
                charmInfo.AddDowngradeMul)
            : gradeTemplate.EnchantDowngradeRatio;

        if (successRoll < successChance)
        {
            if (isLucky && greatSuccessRoll < greatSuccessChance)
            {
                // TODO : Refactor
                var increase = useCharm ? 2 + charmInfo.AddGreatSuccessGrade : 2;
                item.Grade = (byte)GetNextGrade(gradeTemplate, increase).Grade;
                return GradeEnchantResult.GreatSuccess;
            }

            item.Grade = (byte)GetNextGrade(gradeTemplate, 1).Grade;
            return GradeEnchantResult.Success;
        }

        if (breakRoll < breakChance)
        {
            return GradeEnchantResult.Break;
        }

        if (downgradeRoll < downgradeChance)
        {
            var newGrade = (byte)Random.Shared.Next(gradeTemplate.EnchantDowngradeMin, gradeTemplate.EnchantDowngradeMax);
            if (newGrade < 0)
            {
                return GradeEnchantResult.Fail;
            }

            item.Grade = newGrade;
            return GradeEnchantResult.Downgrade;
        }

        return GradeEnchantResult.Fail;
    }

    private static int GoldCost(GradeTemplate gradeTemplate, Item item, int ItemType)
    {
        uint slotTypeId = 0;
        switch (ItemType)
        {
            case 1:
                if (item.Template is not WeaponTemplate weaponTemplate)
                    return -1;
                slotTypeId = weaponTemplate.HoldableTemplate.SlotTypeId;
                break;
            case 2:
                if (item.Template is not ArmorTemplate armorTemplate)
                    return -1;
                slotTypeId = armorTemplate.SlotTemplate.SlotTypeId;
                break;
            case 24:
                if (item.Template is not AccessoryTemplate accessoryTemplate)
                    return -1;
                slotTypeId = accessoryTemplate.SlotTemplate.SlotTypeId;
                break;
        }

        if (slotTypeId == 0)
        {
            return -1;
        }

        var enchantingCost = ItemManager.Instance.GetEquipSlotEnchantingCost(slotTypeId);
        if (enchantingCost == null)
            return -1;

        var itemGrade = gradeTemplate.EnchantCost;
        var itemLevel = item.Template.Level;
        var equipSlotEnchantCost = enchantingCost.Cost;

        var parameters = new Dictionary<string, double>
        {
            { "item_grade", itemGrade },
            { "item_level", itemLevel },
            { "equip_slot_enchant_cost", equipSlotEnchantCost }
        };
        var formula = FormulaManager.Instance.GetFormula((uint)FormulaKind.GradeEnchantCost);

        var cost = (int)formula.Evaluate(parameters);

        return cost;
    }

    private static void Reject(Character character, Skill skill, ErrorMessageType error, string reason)
    {
        Logger.Warn($"GradeEnchant rejected for {character.Name} ({character.Id}): {reason}");
        character.SendErrorMessage(error);
        character.BroadcastPacket(new SCSkillEndedPacket(skill.TlId), true);
    }

    private static GradeTemplate GetNextGrade(GradeTemplate currentGrade, int gradeChange)
    {
        return ItemManager.Instance.GetGradeTemplateByOrder(currentGrade.GradeOrder + gradeChange);
    }

    private static int GetCharmChance(int baseChance, int charmRatio, int charmMul)
    {
        return baseChance + charmRatio + (int)(baseChance * (charmMul / 100.0));
    }
}
