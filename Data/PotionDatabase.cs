using System.Collections.Generic;
using System.Linq;

namespace PotionSwapper.Data;

public static class PotionDatabase
{
    private static readonly Dictionary<uint, PotionInfo> PotionByActionId = new();

    private static readonly PotionInfo[] HpRecoveryPotions =
    [
        new(4551, "Potion",             PotionCategory.HpRecovery, DeepDungeonType.None, 0.05f, 250),
        new(4552, "Hi-Potion",          PotionCategory.HpRecovery, DeepDungeonType.None, 0.10f, 500),
        new(4553, "Mega-Potion",        PotionCategory.HpRecovery, DeepDungeonType.None, 0.20f, 1_000),
        new(4554, "X-Potion",           PotionCategory.HpRecovery, DeepDungeonType.None, 0.30f, 2_000),
        new(13637, "Max-Potion",        PotionCategory.HpRecovery, DeepDungeonType.None, 0.40f, 4_000),
        new(23167, "Super-Potion",      PotionCategory.HpRecovery, DeepDungeonType.None, 0.50f, 8_000),
        new(38956, "Hyper-Potion",      PotionCategory.HpRecovery, DeepDungeonType.None, 0.60f, 12_000),
        new(47701, "Ultra-Potion",      PotionCategory.HpRecovery, DeepDungeonType.None, 0.70f, 16_000),
        new(4561, "Dusken Draught",    PotionCategory.HpRecovery, DeepDungeonType.None, 0.50f, 5_000),
    ];

    private static readonly PotionInfo[] ElixirPotions =
    [
        new(4559, "Elixir",     PotionCategory.HpMpRecovery, DeepDungeonType.None, 0.05f, 250,  0.10f, 500),
        new(4560, "Hi-Elixir",  PotionCategory.HpMpRecovery, DeepDungeonType.None, 0.10f, 500,  0.20f, 1_000),
        new(4563, "Onyx Tears", PotionCategory.HpMpRecovery, DeepDungeonType.None, 0.60f, 12_000, 0.30f, 6_000),
    ];

    private static readonly PotionInfo[] DeepDungeonPotions =
    [
        new(20309, "Sustaining Potion", PotionCategory.DeepDungeonHp, DeepDungeonType.PalaceOfTheDead,  0.30f),
        new(23163, "Empyrean Potion",   PotionCategory.DeepDungeonHp, DeepDungeonType.HeavenOnHigh,     0.30f),
        new(38944, "Orthos Potion",     PotionCategory.DeepDungeonHp, DeepDungeonType.Eureka,           0.30f),
        new(47102, "Pilgrim's Potion",  PotionCategory.DeepDungeonHp, DeepDungeonType.PilgrimsTraverse, 0.30f),
    ];

    private static readonly PotionInfo EurekaPotion =
        new(22306, "Eurekan Potion", PotionCategory.EurekaStandard, DeepDungeonType.None, 0.30f, 0);

    private static readonly List<PotionInfo> AllPotions;
    private static readonly Dictionary<uint, PotionInfo> PotionById;

    static PotionDatabase()
    {
        AllPotions = new List<PotionInfo>(30);
        AllPotions.AddRange(HpRecoveryPotions);
        AllPotions.AddRange(ElixirPotions);
        AllPotions.AddRange(DeepDungeonPotions);
        AllPotions.Add(EurekaPotion);

        // hq is nq + 1000000. i kept screwing this up for like 3 hours straight
        var dict = new Dictionary<uint, PotionInfo>();
        foreach (var p in AllPotions)
        {
            dict[p.ItemId] = p;
            var hqId = p.ItemId + PotionInfo.HqOffset;
            if (hqId > 1_000_000 && hqId < 2_000_000)
                dict[hqId] = p;
        }
        PotionById = dict;

        // action ids are NOT item ids, the hotbar stores actions. that one bit me hard
        var actionMappings = new (uint actionId, uint itemId)[]
        {
            (28, 4551), (146, 4552), (528, 4553), (887, 4554),
            (1462, 13637), (1618, 23167), (2500, 38956), (2887, 47701),
            (30, 4559), (148, 4560), (2501, 4563),
            (1617, 4561),
            (1390, 20309), (1615, 23163), (2499, 38944), (2886, 47102),
            (1616, 22306),
        };
        foreach (var (actionId, itemId) in actionMappings)
        {
            if (dict.TryGetValue(itemId, out var info))
                PotionByActionId[actionId] = info;
        }
    }

    public static IEnumerable<PotionInfo> All => AllPotions;

    public static bool TryGetPotion(uint itemId, out PotionInfo potion)
        => PotionById.TryGetValue(itemId, out potion);

    public static bool TryGetPotionByActionId(uint actionId, out PotionInfo potion)
        => PotionByActionId.TryGetValue(actionId, out potion);

    public static IEnumerable<PotionInfo> GetStandardHpPotions()
        => AllPotions.Where(p => p.Category == PotionCategory.HpRecovery
            && !p.IsDeepDungeonOnly && !p.IsEurekaOnly).OrderByDescending(p => p.HpPercent);

    public static IEnumerable<PotionInfo> GetElixirPotions()
        => AllPotions.Where(p => p.Category == PotionCategory.HpMpRecovery).OrderByDescending(p => p.HpPercent);

    public static PotionInfo? GetPotionForDeepDungeon(DeepDungeonType dungeon)
        => AllPotions.FirstOrDefault(p => p.DeepDungeon == dungeon);

    public static PotionInfo? GetEurekaPotion() => EurekaPotion;
}
