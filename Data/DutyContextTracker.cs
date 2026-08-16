using System.Collections.Generic;

namespace PotionSwapper.Data;

public enum DutyType
{
    None,
    Regular,
    Savage,
    Ultimate,
    DeepDungeonPoTD,
    DeepDungeonHeavenOnHigh,
    DeepDungeonEureka,
    DeepDungeonPilgrimsTraverse,
}

internal static class TerritoryIds
{
    // eureka territories where the eurekan potion works (these are NOT deep dungeons)
    internal static readonly HashSet<ushort> EurekaTerritories = new() { 789, 953 };

    internal static readonly Dictionary<ushort, DutyType> TerritoryToDutyType = new()
    {
        { 463, DutyType.DeepDungeonPoTD },
        { 464, DutyType.DeepDungeonPoTD },
        { 843, DutyType.DeepDungeonHeavenOnHigh },
        { 844, DutyType.DeepDungeonHeavenOnHigh },
        { 789, DutyType.DeepDungeonEureka },
        { 1073, DutyType.DeepDungeonPilgrimsTraverse },
    };
}

public sealed class DutyContextTracker
{
    public DutyType CurrentDutyType => this.GetDutyType();
    public bool IsInDeepDungeon => this.CurrentDutyType is >= DutyType.DeepDungeonPoTD and <= DutyType.DeepDungeonPilgrimsTraverse;
    public bool IsInDuty => Plugin.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty];
    public bool IsInEureka => TerritoryIds.EurekaTerritories.Contains((ushort)Plugin.ClientState.TerritoryType);

    public DeepDungeonType CurrentDeepDungeon => this.CurrentDutyType switch
    {
        DutyType.DeepDungeonPoTD => DeepDungeonType.PalaceOfTheDead,
        DutyType.DeepDungeonHeavenOnHigh => DeepDungeonType.HeavenOnHigh,
        DutyType.DeepDungeonEureka => DeepDungeonType.Eureka,
        DutyType.DeepDungeonPilgrimsTraverse => DeepDungeonType.PilgrimsTraverse,
        _ => DeepDungeonType.None,
    };

    private DutyType GetDutyType()
    {
        var territory = (ushort)Plugin.ClientState.TerritoryType;
        if (TerritoryIds.TerritoryToDutyType.TryGetValue(territory, out var dutyType))
            return dutyType;
        if (!this.IsInDuty)
            return DutyType.None;

        // territory ids are hardcoded from xivapi, if SE adds a new dungeon this silently breaks
        // 4=savage 5=ultimate in the cfc table, took me way too long to dig out
        var cfc = Plugin.DutyState.ContentFinderCondition;
        if (cfc.IsValid)
        {
            var row = (dynamic)(object)cfc;
            return row.Type switch { 4 => DutyType.Savage, 5 => DutyType.Ultimate, _ => DutyType.Regular };
        }
        return DutyType.Regular;
    }
}
