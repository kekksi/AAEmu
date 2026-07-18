using System.Reflection;
using System.Runtime.CompilerServices;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Id;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.Items.Actions;
using AAEmu.Game.Models.Game.Items.Containers;
using AAEmu.Game.Models.Game.Items.Templates;
using AAEmu.UnitTests.Utils;
using AAEmu.UnitTests.Utils.Mocks;

namespace AAEmu.UnitTests.Game.Models.Game.Items.Containers;

[NotInParallel]
public class InventoryTests
{
    [Test]
    public async Task InventoryAddsItem()
    {
        // ItemIdManager.Instance.Initialize();

        var mockCharacter = new CharacterMock();
        var container = new ItemContainer(mockCharacter.Id, SlotType.Inventory, false, mockCharacter);
        var item = InventoryTestUtils.MockItem(1, 1);

        await Assert.That(container.AddOrMoveExistingItem(ItemTaskType.Gm, item, 1)).IsTrue();

        var i = container.Items.SingleOrDefault(it => it.TemplateId == 1);

        await Assert.That(i).IsNotNull();
    }

    [Test]
    public async Task SplitAssignsTargetOwnerToNewStack()
    {
        var template = new ItemTemplate
        {
            Id = 100,
            MaxCount = 100,
            BindType = ItemBindType.Normal
        };
        var sourceItem = new Item(99, template, 10);
        var itemManager = CreateItemManager(template, sourceItem);
        var singletonField = typeof(ItemManager).BaseType!.GetField("s_instance", BindingFlags.NonPublic | BindingFlags.Static)!;
        var previousItemManager = singletonField.GetValue(null);

        try
        {
            singletonField.SetValue(null, itemManager);
            var container = new ItemContainer(0, SlotType.Inventory, false, null)
            {
                ContainerSize = 20
            };
            await Assert.That(container.AddOrMoveExistingItem(ItemTaskType.Invalid, sourceItem, 0)).IsTrue();
            SetPrivateField(container, "_ownerId", 42u);
            var inventory = (Inventory)RuntimeHelpers.GetUninitializedObject(typeof(Inventory));

            var result = inventory.SplitOrMoveItemEx(ItemTaskType.Invalid, container, container,
                sourceItem.Id, SlotType.Inventory, 0, 0, SlotType.Inventory, 1, 4);

            var splitItem = container.GetItemBySlot(1);
            await Assert.That(result).IsTrue();
            await Assert.That(sourceItem.Count).IsEqualTo(6);
            await Assert.That(splitItem).IsNotNull();
            await Assert.That(splitItem.Count).IsEqualTo(4);
            await Assert.That(splitItem.OwnerId).IsEqualTo(42ul);
        }
        finally
        {
            singletonField.SetValue(null, previousItemManager);
        }
    }

    private static ItemManager CreateItemManager(ItemTemplate template, Item sourceItem)
    {
        var itemIdManager = Mock.Of<IItemIdManager>();
        itemIdManager.GetNextId().Returns(100u);
        var manager = new ItemManager(
            Mock.Of<ISkillManager>().Object,
            itemIdManager.Object,
            Mock.Of<IContainerIdManager>().Object,
            Mock.Of<ILocalizationManager>().Object,
            Mock.Of<ITaskManager>().Object,
            Mock.Of<AAEmu.Game.Core.Managers.World.IWorldManager>().Object);

        SetPrivateField(manager, "_templates", new Dictionary<uint, ItemTemplate> { [template.Id] = template });
        SetPrivateField(manager, "_allItems", new Dictionary<ulong, Item> { [sourceItem.Id] = sourceItem });
        SetPrivateField(manager, "_removedItems", new List<ulong>());
        return manager;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(target, value);
    }
}
