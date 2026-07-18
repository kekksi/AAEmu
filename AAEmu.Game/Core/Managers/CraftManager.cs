using AAEmu.Commons.Utils;
using AAEmu.Game.Models.Game.Crafts;
using AAEmu.Game.Utils.DB;
using NLog;

namespace AAEmu.Game.Core.Managers;

public class CraftManager : Singleton<CraftManager>, ICraftManager
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

    private Dictionary<uint, Craft> _crafts;
    private Dictionary<uint, uint> _recipeCraftsByItem;
    private HashSet<uint> _learnableCrafts;

    public void Load()
    {
        _crafts = [];
        _recipeCraftsByItem = [];
        _learnableCrafts = [];
        Logger.Info("Loading crafts...");

        using (var connection = SQLite.CreateConnection())
        {
            /* Crafts */
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT * FROM crafts";
                command.Prepare();
                using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                {
                    while (reader.Read())
                    {
                        var template = new Craft
                        {
                            Id = reader.GetUInt32("id"), CastDelay = reader.GetInt32("cast_delay"), ToolId = reader.GetUInt32("tool_id", 0),
                            SkillId = reader.GetUInt32("skill_id", 0),
                            WiId = reader.GetUInt32("wi_id"),
                            MilestoneId = reader.GetUInt32("milestone_id", 0),
                            ReqDoodadId = reader.GetUInt32("req_doodad_id", 0),
                            NeedBind = reader.GetBoolean("need_bind"),
                            AcId = reader.GetUInt32("ac_id", 0),
                            ActabilityLimit = reader.GetInt32("actability_limit"),
                            ShowUpperCraft = reader.GetBoolean("show_upper_crafts"),
                            RecommendLevel = reader.GetInt32("recommend_level"),
                            VisibleOrder = reader.GetInt32("visible_order")
                        };
                        _crafts.Add(template.Id, template);
                    }
                }
            }

            /* Craft products (item you get at the end) */
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT * FROM craft_products";
                command.Prepare();
                using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                {
                    while (reader.Read())
                    {
                        var craftId = reader.GetUInt32("craft_id");
                        if (!_crafts.ContainsKey(craftId))
                            continue;

                        var template = new CraftProduct
                        {
                            Id = reader.GetUInt32("id"), CraftId = reader.GetUInt32("craft_id"), ItemId = reader.GetUInt32("item_id"),
                            Amount = reader.GetInt32("amount", 1), //We always want to produce at least 1 item ?
                            Rate = reader.GetInt32("rate"),
                            ShowLowerCrafts = reader.GetBoolean("show_lower_crafts"),
                            UseGrade = reader.GetBoolean("use_grade"),
                            ItemGradeId = reader.GetUInt32("item_grade_id")
                        };

                        _crafts[template.CraftId].CraftProducts.Add(template);
                    }
                }
            }

            /* Craft products (item you get at the end) */
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT * FROM craft_materials";
                command.Prepare();
                using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                {
                    while (reader.Read())
                    {
                        var craftId = reader.GetUInt32("craft_id");
                        if (!_crafts.ContainsKey(craftId))
                            continue;

                        var template = new CraftMaterial
                        {
                            Id = reader.GetUInt32("id"), CraftId = craftId, ItemId = reader.GetUInt32("item_id"), Amount = reader.GetInt32("amount", 1), //We always want to cost at least 1 item ?
                            MainGrade = reader.GetBoolean("main_grade")
                        };

                        _crafts[craftId].CraftMaterials.Add(template);
                    }
                }
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT * FROM craft_pack_crafts";
                command.Prepare();
                using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                {
                    while (reader.Read())
                    {
                        var craftId = reader.GetUInt32("craft_id");
                        if (!_crafts.TryGetValue(craftId, out var craft))
                            continue;
                        craft.IsPack = true;
                    }
                }
            }

            var invalidRecipeCount = 0;
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT DISTINCT ir.item_id, ir.craft_id " +
                    "FROM item_recipes ir " +
                    "JOIN items i ON i.id = ir.item_id " +
                    "JOIN skill_effects se ON se.skill_id = i.use_skill_id " +
                    "JOIN effects e ON e.id = se.effect_id " +
                    "WHERE e.actual_type = 'TrainCraftEffect'";
                command.Prepare();
                using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                {
                    while (reader.Read())
                    {
                        var itemId = reader.GetUInt32("item_id");
                        var craftId = reader.GetUInt32("craft_id");
                        if (!_crafts.ContainsKey(craftId))
                        {
                            invalidRecipeCount++;
                            continue;
                        }

                        _recipeCraftsByItem[itemId] = craftId;
                        _learnableCrafts.Add(craftId);
                    }
                }
            }

            // Older training skills identify the craft directly instead of through a recipe item.
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT craft_id FROM train_craft_effects";
                command.Prepare();
                using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                {
                    while (reader.Read())
                    {
                        var craftId = reader.GetUInt32("craft_id");
                        if (_crafts.ContainsKey(craftId))
                            _learnableCrafts.Add(craftId);
                    }
                }
            }

            if (invalidRecipeCount > 0)
                Logger.Warn("Ignored {0} item recipe references to missing crafts", invalidRecipeCount);
        }

        Logger.Info("Loaded {0} crafts, {1} recipe items and {2} learnable crafts", _crafts.Count,
            _recipeCraftsByItem.Count, _learnableCrafts.Count);
    }

    public Craft GetCraftById(uint craftId)
    {
        return _crafts[craftId];
    }

    public bool TryGetCraftById(uint craftId, out Craft craft)
    {
        return _crafts.TryGetValue(craftId, out craft);
    }

    public bool TryGetCraftIdByRecipeItem(uint itemId, out uint craftId)
    {
        return _recipeCraftsByItem.TryGetValue(itemId, out craftId);
    }

    public bool IsLearnableCraft(uint craftId)
    {
        return _learnableCrafts.Contains(craftId);
    }
}
