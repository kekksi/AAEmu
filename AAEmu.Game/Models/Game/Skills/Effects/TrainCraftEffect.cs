using AAEmu.Game.Core.Packets;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Items.Actions;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Game.Skills.Effects;

public class TrainCraftEffect : EffectTemplate
{
    public uint CraftId { get; set; }

    public override bool OnActionTime => false;

    public override void Apply(BaseUnit caster, SkillCaster casterObj, BaseUnit target, SkillCastTarget targetObj,
        CastAction castObj, EffectSource source, SkillObject skillObject, DateTime time,
        CompressedGamePackets packetBuilder = null)
    {
        if (caster is not Character character)
            return;

        var craftId = CraftId;
        var recipeItem = casterObj as SkillItem;
        if (recipeItem != null &&
            CraftManager.Instance.TryGetCraftIdByRecipeItem(recipeItem.ItemTemplateId, out var recipeCraftId))
            craftId = recipeCraftId;

        if (!CraftManager.Instance.TryGetCraftById(craftId, out _))
        {
            CancelSkill(source);
            character.SendErrorMessage(ErrorMessageType.CraftInvalidCraftType);
            Logger.Warn("Character {0} attempted to learn missing craft {1} from item {2}", character.Id,
                craftId, recipeItem?.ItemTemplateId ?? 0);
            return;
        }

        if (character.Craft.LearnedCraft(craftId))
        {
            CancelSkill(source);
            character.SendErrorMessage(ErrorMessageType.CraftAlreadyLearned);
            return;
        }

        if (recipeItem != null &&
            (recipeItem.SkillSourceItem == null ||
             character.Inventory.Bag.ConsumeItem(ItemTaskType.ConsumeSkillSource, recipeItem.ItemTemplateId, 1,
                 recipeItem.SkillSourceItem) != 1))
        {
            CancelSkill(source);
            character.SendErrorMessage(ErrorMessageType.NotEnoughRequiredItem);
            return;
        }

        if (!character.Craft.TryLearnCraft(craftId))
        {
            CancelSkill(source);
            character.SendErrorMessage(ErrorMessageType.CraftAlreadyLearned);
            return;
        }

        character.SendPacket(new SCCraftItemUnlockPacket(craftId));
        Logger.Debug("Character {0} learned craft {1} from item {2}", character.Id, craftId,
            recipeItem?.ItemTemplateId ?? 0);
    }

    private static void CancelSkill(EffectSource source)
    {
        if (source?.Skill != null)
            source.Skill.Cancelled = true;
    }
}
