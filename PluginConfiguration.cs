using System;
using Dalamud.Configuration;

namespace PotionSwapper;

public enum DeepDungeonPotionMode
{
    Enable,
    Separate,
    Disable,
}

public enum ElixirMode
{
    Enable,
    Separate,
    Disable,
}

public enum ElixirPriority
{
    Smart,
    Last,
}

[Serializable]
public class PluginConfiguration : IPluginConfiguration
{
    // these enum ints have to line up with the radio buttons in the config window. do NOT reorder
    public int Version { get; set; } = 1;
    public DeepDungeonPotionMode DeepDungeonMode { get; set; } = DeepDungeonPotionMode.Separate;
    public ElixirMode ElixirMode { get; set; } = ElixirMode.Enable;
    public ElixirPriority ElixirPriority { get; set; } = ElixirPriority.Smart;
    public bool EnableIconTinting { get; set; } = false;

    // default colors took forever to tune. green/teal/blue so theyre actually distinguishable
    public uint HpPotionTintColor { get; set; } = 0xFF00C853;
    public uint DeepDungeonPotionTintColor { get; set; } = 0xFF26A69A;
    public uint ElixirTintColor { get; set; } = 0xFF1531CF;
    public System.Collections.Generic.HashSet<uint> ExcludedItemIds { get; set; } = new();
}
