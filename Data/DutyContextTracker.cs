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
    // the 4 open world eureka zones, eurekan potion only works out here
    // eureka orthos is a deep dungeon so it uses orthos potion instead, kept out on purpose
    internal static readonly HashSet<ushort> EurekaTerritories = new() { 732, 763, 795, 827 };
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
        if (!this.IsInDuty)
            return DutyType.None;

        // deep dungeons are all content type 21, cfc row tells us exactly which one.
        // beats hardcoding every floor territory id, that list was straight garbage
        var cfc = Plugin.DutyState.ContentFinderCondition;
        if (!cfc.IsValid)
            return DutyType.Regular;

        if (cfc.Value.ContentType.RowId == 21)
        {
            var name = cfc.Value.Name.ToString();
            if (name.Contains("Palace of the Dead")) return DutyType.DeepDungeonPoTD;
            if (name.Contains("Heaven")) return DutyType.DeepDungeonHeavenOnHigh;
            if (name.Contains("Orthos")) return DutyType.DeepDungeonEureka;
            if (name.Contains("Pilgrim")) return DutyType.DeepDungeonPilgrimsTraverse;
        }

        return DutyType.Regular;
    }
}
