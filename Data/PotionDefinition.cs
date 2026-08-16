using System;

namespace PotionSwapper.Data;

public enum PotionCategory
{
    HpRecovery,
    MpRecovery,
    HpMpRecovery,
    DeepDungeonHp,
    EurekaStandard,
}

public enum DeepDungeonType
{
    None,
    PalaceOfTheDead,
    HeavenOnHigh,
    Eureka,
    PilgrimsTraverse,
}

public readonly struct PotionInfo
{
    public const uint HqOffset = 1_000_000;

    public readonly uint ItemId;
    public readonly string Name;
    public readonly PotionCategory Category;
    public readonly DeepDungeonType DeepDungeon;
    public readonly float HpPercent;
    public readonly uint HpCap;
    public readonly float MpPercent;
    public readonly uint MpCap;

    public uint GetHealForHp(uint maxHp)
    {
        var heal = (uint)(maxHp * this.HpPercent);
        return this.HpCap > 0 ? Math.Min(heal, this.HpCap) : heal;
    }

    public uint GetHealForMp(uint maxMp)
    {
        var heal = (uint)(maxMp * this.MpPercent);
        return this.MpCap > 0 ? Math.Min(heal, this.MpCap) : heal;
    }

    public bool IsDeepDungeonOnly => this.DeepDungeon != DeepDungeonType.None;
    public bool IsDualRecovery => this.MpPercent > 0;
    public bool IsEurekaOnly => this.Category == PotionCategory.EurekaStandard;

    // the percent/cap numbers came from a random spreadsheet i found, some might be slightly off
    public PotionInfo(uint itemId, string name, PotionCategory category, DeepDungeonType deepDungeon, float hpPercent, uint hpCap = 0)
        : this(itemId, name, category, deepDungeon, hpPercent, hpCap, 0f, 0) { }

    // two ctors because elixirs need mp params and everything else doesnt. lazy but whatever
    public PotionInfo(uint itemId, string name, PotionCategory category, DeepDungeonType deepDungeon,
        float hpPercent, uint hpCap, float mpPercent, uint mpCap)
    {
        this.ItemId = itemId;
        this.Name = name;
        this.Category = category;
        this.DeepDungeon = deepDungeon;
        this.HpPercent = hpPercent;
        this.HpCap = hpCap;
        this.MpPercent = mpPercent;
        this.MpCap = mpCap;
    }
}
