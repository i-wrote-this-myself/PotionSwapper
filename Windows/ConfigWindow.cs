using System;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;

using PotionSwapper;

namespace PotionSwapper.Windows;

public sealed class ConfigWindow : Window, IDisposable
{
    private readonly PluginConfiguration configuration;

    private int selectedTab = 0;
    private static readonly string[] TabNames = { "General", "Appearance" };

    private int ddMode;
    private int elixirMode;
    private int elixirPriority;
    private bool enableIconTinting;
    private Vector4 hpTintColorVec;
    private Vector4 ddTintColorVec;
    private Vector4 elixirTintColorVec;

    private static readonly uint DefaultHpTint = 0xFF00C853;
    private static readonly uint DefaultDdTint = 0xFF26A69A;
    private static readonly uint DefaultElixirTint = 0xFF1531CF;

    private static Vector4 UintToVec(uint color)
    {
        var a = ((color >> 24) & 0xFF) / 255f;
        var r = ((color >> 16) & 0xFF) / 255f;
        var g = ((color >> 8) & 0xFF) / 255f;
        var b = (color & 0xFF) / 255f;
        return new Vector4(r, g, b, a);
    }

    private static uint VecToUint(Vector4 v)
    {
        var a = (byte)MathF.Max(0, MathF.Min(255, v.W * 255));
        var r = (byte)MathF.Max(0, MathF.Min(255, v.X * 255));
        var g = (byte)MathF.Max(0, MathF.Min(255, v.Y * 255));
        var b = (byte)MathF.Max(0, MathF.Min(255, v.Z * 255));
        return ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
    }

    private static string ColorToHex(uint color)
    {
        var r = (color >> 16) & 0xFF;
        var g = (color >> 8) & 0xFF;
        var b = color & 0xFF;
        return $"{r:X2}{g:X2}{b:X2}";
    }

    // alpha stays at the front of the uint so the pickers keep a fully opaque color. easy to lose
    private void SyncFromConfig()
    {
        this.ddMode = (int)this.configuration.DeepDungeonMode;
        this.elixirMode = (int)this.configuration.ElixirMode;
        this.elixirPriority = (int)this.configuration.ElixirPriority;
        this.enableIconTinting = this.configuration.EnableIconTinting;
        this.hpTintColorVec = UintToVec(this.configuration.HpPotionTintColor);
        this.ddTintColorVec = UintToVec(this.configuration.DeepDungeonPotionTintColor);
        this.elixirTintColorVec = UintToVec(this.configuration.ElixirTintColor);
    }

    private void SyncToConfig()
    {
        this.configuration.DeepDungeonMode = (DeepDungeonPotionMode)this.ddMode;
        this.configuration.ElixirMode = (ElixirMode)this.elixirMode;
        this.configuration.ElixirPriority = (ElixirPriority)this.elixirPriority;
        this.configuration.EnableIconTinting = this.enableIconTinting;
        this.configuration.HpPotionTintColor = VecToUint(this.hpTintColorVec);
        this.configuration.DeepDungeonPotionTintColor = VecToUint(this.ddTintColorVec);
        this.configuration.ElixirTintColor = VecToUint(this.elixirTintColorVec);
    }

    public ConfigWindow(PluginConfiguration configuration)
        : base("PotionSwapper Configuration###PotionSwapperConfig", ImGuiWindowFlags.None)
    {
        this.configuration = configuration;
        this.SizeCondition = ImGuiCond.FirstUseEver;
        this.Size = new Vector2(440, 420);
        this.SyncFromConfig();
    }

    public override void Draw()
    {
        this.SyncFromConfig();
        DrawHeader();
        this.DrawTabBar();
        ImGui.Separator();

        switch (this.selectedTab)
        {
            case 0:
                this.DrawGeneralTab();
                break;
            case 1:
                this.DrawAppearanceTab();
                break;
        }

        this.SyncToConfig();
    }

    private static void DrawHeader()
    {
        ImGui.TextColored(new Vector4(1f, 0.84f, 0f, 1f), "PotionSwapper");
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.8f, 1f), "\u2014 Smart potion icon swapping");
        ImGui.Separator();
    }

    private void DrawTabBar()
    {
        var activeColor = new Vector4(1f, 0.84f, 0f, 0.3f);
        var buttonSize = new Vector2(110, 0);

        for (int i = 0; i < TabNames.Length; i++)
        {
            var isActive = this.selectedTab == i;
            if (isActive)
                ImGui.PushStyleColor(ImGuiCol.Button, activeColor);

            if (ImGui.Button(TabNames[i], buttonSize))
                this.selectedTab = i;

            if (isActive)
                ImGui.PopStyleColor();

            if (i < TabNames.Length - 1)
                ImGui.SameLine();
        }

        ImGui.Spacing();
    }

    private void DrawGeneralTab()
    {
        ImGui.TextColored(new Vector4(0.9f, 0.9f, 0.9f, 1f), "Deep Dungeon Potions");
        ImGui.TextWrapped("Control how Deep Dungeon potions (Sustaining, Empyrean, Orthos, Pilgrim's) are handled.");
        ImGuiComponents.HelpMarker("These potions are restricted to their respective deep dungeon areas.");

        ImGui.Spacing();
        DrawDDToggle();

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(new Vector4(0.9f, 0.9f, 0.9f, 1f), "Elixirs");
        ImGui.TextWrapped("Elixirs restore both HP and MP. Control how they factor into potion swapping.");
        ImGuiComponents.HelpMarker("Elixirs, Hi-Elixirs, and Onyx Tears.");

        ImGui.Spacing();
        DrawElixirToggle();

        ImGui.Spacing();
        DrawElixirPriority();
    }

    private void DrawDDToggle()
    {
        var modes = new[]
        {
            ("Enable", (int)DeepDungeonPotionMode.Enable, "DD potions swap alongside regular HP potions on the same hotbar slot."),
            ("Separate", (int)DeepDungeonPotionMode.Separate, "DD potions get their own dedicated hotbar slot, separate from regular potions."),
            ("Disable", (int)DeepDungeonPotionMode.Disable, "DD potions are excluded from swapping entirely."),
        };

        foreach (var (label, value, desc) in modes)
        {
            var isActive = this.ddMode == value;
            if (ImGui.RadioButton($"##DDMode{value}", isActive))
                this.ddMode = value;
            ImGui.SameLine();
            ImGui.Text(label);
            ImGuiComponents.HelpMarker(desc);
            ImGui.SameLine();
        }
    }

    private void DrawElixirToggle()
    {
        var modes = new[]
        {
            ("Enable", (int)ElixirMode.Enable, "Elixirs swap alongside regular HP potions on the same hotbar slot."),
            ("Separate", (int)ElixirMode.Separate, "Elixirs get their own dedicated hotbar slot, separate from regular potions."),
            ("Disable", (int)ElixirMode.Disable, "Elixirs are excluded from swapping entirely."),
        };

        foreach (var (label, value, desc) in modes)
        {
            var isActive = this.elixirMode == value;
            if (ImGui.RadioButton($"##ElixirMode{value}", isActive))
                this.elixirMode = value;
            ImGui.SameLine();
            ImGui.Text(label);
            ImGuiComponents.HelpMarker(desc);
            ImGui.SameLine();
        }
    }

    private void DrawElixirPriority()
    {
        var elixirsEnabled = this.elixirMode == (int)ElixirMode.Enable;

        if (!elixirsEnabled)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.5f);
            ImGui.BeginDisabled();
        }

        ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.8f, 1f), "Elixir Priority");
        ImGui.TextWrapped("When Elixirs are in the global pool, control how they compete with regular potions.");
        ImGuiComponents.HelpMarker("Only applies when Elixirs are set to 'Enable'.");

        ImGui.Spacing();

        var priorities = new[]
        {
            ("Smart", (int)ElixirPriority.Smart, "Best potion wins regardless of type. An Elixir may be chosen over a weaker potion."),
            ("Last", (int)ElixirPriority.Last, "Elixirs are used only when no regular potions remain. Conserves them as a last resort."),
        };

        foreach (var (label, value, desc) in priorities)
        {
            var isActive = this.elixirPriority == value;
            if (ImGui.RadioButton($"##ElixirPriority{value}", isActive))
                this.elixirPriority = value;
            ImGui.SameLine();
            ImGui.Text(label);
            ImGuiComponents.HelpMarker(desc);
            ImGui.SameLine();
        }

        if (!elixirsEnabled)
        {
            ImGui.EndDisabled();
            ImGui.PopStyleVar();
        }
    }

    private void DrawAppearanceTab()
    {
        ImGui.Checkbox("Enable icon tinting", ref this.enableIconTinting);
        ImGuiComponents.HelpMarker("Tints swapped potion icons so you can tell them from your original hotbar setup.");

        ImGui.Spacing();
        ImGui.Spacing();

        if (!this.enableIconTinting)
        {
            ImGui.BeginDisabled();
        }

        ImGui.TextColored(new Vector4(0.9f, 0.9f, 0.9f, 1f), "HP Potion Tint");
        ImGuiComponents.HelpMarker($"Default: #{ColorToHex(DefaultHpTint)}");

        ImGui.Spacing();
        ImGui.PushItemWidth(150);
        ImGui.ColorEdit4("##HpTint", ref this.hpTintColorVec, ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.NoInputs);
        ImGui.PopItemWidth();
        ImGui.SameLine();
        if (ImGui.Button("Reset##HpTint"))
            this.hpTintColorVec = UintToVec(DefaultHpTint);

        ImGui.Spacing();
        ImGui.Spacing();

        ImGui.TextColored(new Vector4(0.9f, 0.9f, 0.9f, 1f), "Deep Dungeon Potion Tint");
        ImGuiComponents.HelpMarker($"Default: #{ColorToHex(DefaultDdTint)}");

        ImGui.Spacing();
        ImGui.PushItemWidth(150);
        ImGui.ColorEdit4("##DdTint", ref this.ddTintColorVec, ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.NoInputs);
        ImGui.PopItemWidth();
        ImGui.SameLine();
        if (ImGui.Button("Reset##DdTint"))
            this.ddTintColorVec = UintToVec(DefaultDdTint);

        ImGui.Spacing();
        ImGui.Spacing();

        // the three tint blocks are copy pasted. could refactor but im not touching working code
        ImGui.TextColored(new Vector4(0.9f, 0.9f, 0.9f, 1f), "Elixir Tint");
        ImGuiComponents.HelpMarker($"Default: #{ColorToHex(DefaultElixirTint)}");

        ImGui.Spacing();
        ImGui.PushItemWidth(150);
        ImGui.ColorEdit4("##ElixirTint", ref this.elixirTintColorVec, ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.NoInputs);
        ImGui.PopItemWidth();
        ImGui.SameLine();
        if (ImGui.Button("Reset##ElixirTint"))
            this.elixirTintColorVec = UintToVec(DefaultElixirTint);

        ImGui.Spacing();
        ImGui.Spacing();

        // Reset All button
        if (ImGui.Button("Reset All Colors"))
        {
            this.hpTintColorVec = UintToVec(DefaultHpTint);
            this.ddTintColorVec = UintToVec(DefaultDdTint);
            this.elixirTintColorVec = UintToVec(DefaultElixirTint);
        }

        if (!this.enableIconTinting)
        {
            ImGui.EndDisabled();
        }
    }

    public void Dispose() { }
}
