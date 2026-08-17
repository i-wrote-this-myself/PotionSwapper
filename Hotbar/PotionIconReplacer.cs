using System;
using System.Collections.Generic;
using System.Linq;

using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;

using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;

using PotionSwapper.Data;

namespace PotionSwapper.Hotbar;

public enum IconColoringMethod { Vibrant = 0, Shade = 1, Glow = 2 }

public readonly struct ComboTint
{
    public readonly uint Color;
    public readonly IconColoringMethod? Method;
    public ComboTint(uint color, IconColoringMethod? method = null) { this.Color = color; this.Method = method; }
}

internal struct SlotTintState
{
    public byte GameR, GameG, GameB;
    public byte WroteR, WroteG, WroteB;
}

internal sealed class PotionIconReplacer : IDisposable
{
    private static readonly string[] HotbarAddonNames =
    [
        .. Enumerable.Range(0, 10).Select(i => i == 0 ? "_ActionBar" : $"_ActionBar{i:D2}"),
        "_ActionCross", "_ActionDoubleCrossL", "_ActionDoubleCrossR",
    ];

    private readonly PluginConfiguration configuration;

    private readonly Dictionary<nint, (RaptureHotbarModule.HotbarSlotType OrigType, uint OrigCmd, uint AppliedCmd)> slotState = new();
    private readonly Dictionary<nint, ComboTint> activeTintBySlot = new();
    private readonly Dictionary<(string addon, int slot), SlotTintState> tintedSlots = new();

    private uint currentFrame = 0;

    public PotionIconReplacer(PluginConfiguration configuration)
    {
        this.configuration = configuration;

        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, HotbarAddonNames, this.OnActionBarPostDraw);
        Plugin.Framework.Update += OnFrameworkUpdate;
        Plugin.Log.Information("PotionSwapper started (CommandId swap mode).");
    }

    public unsafe void OnFrameworkUpdate(IFramework framework)
    {
        this.currentFrame++;
        ServiceContainer.CooldownTracker.Refresh();

        if (!Plugin.ClientState.IsLoggedIn || Plugin.ObjectTable.LocalPlayer is not IPlayerCharacter player)
            return;

        var hotbarModule = RaptureHotbarModule.Instance();
        if (hotbarModule == null) return;

        var hpPercent = player.CurrentHp / (float)player.MaxHp * 100f;
        var mpPercent = player.CurrentMp / (float)player.MaxMp * 100f;
        //Plugin.Log.Debug($"frame {this.currentFrame} hp {hpPercent:F1} mp {mpPercent:F1}");

        // standard bars have 12 slots, cross bars have 16. i keep getting this backwards
        for (uint hb = 0; hb < 18; hb++)
        {
            uint slotCount = hb < 10 ? 12u : 16u;
            for (uint si = 0; si < slotCount; si++)
            {
                var slot = hotbarModule->GetSlotById(hb, si);
                if (slot == null) continue;

                var slotAddr = (nint)slot;
                if (slot->CommandId == 0)
                {
                    this.slotState.Remove(slotAddr);
                    this.activeTintBySlot.Remove(slotAddr);
                    continue;
                }

                var curType = slot->CommandType;
                var curCmd = slot->CommandId;

                // the game works out the icon/tooltip/count from CommandId every frame, so rewriting
                // that one field updates everything at once. i tried editing the icon node directly first, nightmare
                bool isPotion = curType == RaptureHotbarModule.HotbarSlotType.Action
                    ? PotionDatabase.TryGetPotionByActionId(curCmd, out _)
                    : curType == RaptureHotbarModule.HotbarSlotType.Item
                        ? PotionDatabase.TryGetPotion(curCmd, out _)
                        : false;

                if (!isPotion)
                {
                    // dont restore here, if the player dragged a cordial over it restoring would stomp their item
                    this.slotState.Remove(slotAddr);
                    this.activeTintBySlot.Remove(slotAddr);
                    continue;
                }

                if (!this.slotState.TryGetValue(slotAddr, out var st))
                {
                    st = (curType, curCmd, curCmd);
                }
                else if (curCmd != st.OrigCmd && curCmd != st.AppliedCmd)
                {
                    st = (curType, curCmd, curCmd);
                }

                bool origIsPotion;
                PotionInfo originalPotion = default;
                if (st.OrigType == RaptureHotbarModule.HotbarSlotType.Action)
                    origIsPotion = PotionDatabase.TryGetPotionByActionId(st.OrigCmd, out originalPotion);
                else if (st.OrigType == RaptureHotbarModule.HotbarSlotType.Item)
                    origIsPotion = PotionDatabase.TryGetPotion(st.OrigCmd, out originalPotion);
                else
                    origIsPotion = false;

                if (!origIsPotion || this.configuration.ExcludedItemIds.Contains(originalPotion.ItemId))
                {
                    this.RestoreIfNeeded(slot, ref st, slotAddr);
                    this.slotState[slotAddr] = st;
                    continue;
                }

                var bestItemId = this.FindBestPotion(originalPotion, player.MaxHp, player.MaxMp, hpPercent, mpPercent);

                // 0 means nothing usable, usually because the whole potion cd is rolling.
                // keep whatever potion is on the slot so the cd overlay shows, dont rip it back
                // to the host potion. reverting after using one was driving me insane
                if (bestItemId == 0)
                    continue;

                if (bestItemId == originalPotion.ItemId)
                {
                    this.RestoreIfNeeded(slot, ref st, slotAddr);
                    this.slotState[slotAddr] = st;
                    continue;
                }

                var commandItemId = this.PickOwnedVariant(bestItemId);
                bool ddTarget = PotionDatabase.TryGetPotion(bestItemId, out var targetInfo) && targetInfo.IsDeepDungeonOnly;

                // dd potions are held in the restricted inventory, an item slot makes the game
                // show count 0 and refuse to use em. lay the action down instead like dragging it on
                var applyCmd = ddTarget ? PotionDatabase.GetActionIdForItem(bestItemId) : commandItemId;
                var changed = st.AppliedCmd != applyCmd;
                if (ddTarget)
                    ApplySwapAction(slot, applyCmd, changed);
                else
                    ApplySwap(slot, commandItemId, changed);

                if (changed)
                {
                    var hadSwap = st.AppliedCmd != st.OrigCmd;
                    var fromName = hadSwap && PotionDatabase.TryGetPotion(st.AppliedCmd, out var fromP)
                        ? fromP.Name : originalPotion.Name;
                    var toName = PotionDatabase.TryGetPotion(bestItemId, out var toP)
                        ? toP.Name : $"item {commandItemId}";
                    Plugin.Log.Information($"PotionSwapper: hotbar {hb} slot {si}: {fromName} -> {toName}");
                }

                st.AppliedCmd = applyCmd;
                this.slotState[slotAddr] = st;

                if (PotionDatabase.TryGetPotion(bestItemId, out var bestInfo))
                {
                    ComboTint? tint = bestInfo.Category switch
                    {
                        PotionCategory.HpRecovery or PotionCategory.EurekaStandard => new ComboTint(this.configuration.HpPotionTintColor),
                        PotionCategory.HpMpRecovery => new ComboTint(this.configuration.ElixirTintColor),
                        PotionCategory.DeepDungeonHp => new ComboTint(this.configuration.DeepDungeonPotionTintColor),
                        _ => null,
                    };
                    if (tint.HasValue)
                        this.activeTintBySlot[slotAddr] = tint.Value;
                    else
                        this.activeTintBySlot.Remove(slotAddr);
                }
            }
        }
    }

    private static unsafe void ApplySwap(RaptureHotbarModule.HotbarSlot* slot, uint itemId, bool changed)
    {
        // Set() is the same call the game runs when you drag an item onto the bar, on a real change
        // it keeps the hq shimmer and hover tooltip looking right. manual SetString looked like garbage
        if (changed)
        {
            slot->Set(RaptureHotbarModule.HotbarSlotType.Item, itemId);
        }
        else
        {
            slot->CommandType = RaptureHotbarModule.HotbarSlotType.Item;
            slot->CommandId = itemId;
            slot->ApparentActionId = itemId;
            slot->ApparentSlotType = RaptureHotbarModule.HotbarSlotType.Item;
        }
    }

    // deep dungeon potions have to be dropped as an action slot or the game cant count or use em
    private static unsafe void ApplySwapAction(RaptureHotbarModule.HotbarSlot* slot, uint actionId, bool changed)
    {
        if (changed)
        {
            slot->Set(RaptureHotbarModule.HotbarSlotType.Action, actionId);
        }
        else
        {
            slot->CommandType = RaptureHotbarModule.HotbarSlotType.Action;
            slot->CommandId = actionId;
            slot->ApparentActionId = actionId;
            slot->ApparentSlotType = RaptureHotbarModule.HotbarSlotType.Action;
        }
    }

    private unsafe void RestoreIfNeeded(RaptureHotbarModule.HotbarSlot* slot,
        ref (RaptureHotbarModule.HotbarSlotType OrigType, uint OrigCmd, uint AppliedCmd) st, nint slotAddr)
    {
        if (st.AppliedCmd != st.OrigCmd || slot->CommandType != st.OrigType)
        {
            RestoreSlotCommand(slot, st.OrigType, st.OrigCmd);
        }
        st.AppliedCmd = st.OrigCmd;
        this.activeTintBySlot.Remove(slotAddr);
    }

    private static unsafe void RestoreSlotCommand(RaptureHotbarModule.HotbarSlot* slot,
        RaptureHotbarModule.HotbarSlotType type, uint cmd)
    {
        slot->Set(type, cmd);
    }

    private unsafe uint PickOwnedVariant(uint baseItemId)
    {
        var nqId = baseItemId >= PotionInfo.HqOffset ? baseItemId - PotionInfo.HqOffset : baseItemId;
        if (nqId < 1_000_000 && this.GetItemCount(nqId + PotionInfo.HqOffset) > 0)
            return nqId + PotionInfo.HqOffset;
        return nqId;
    }

    public void OnPlayerLogin() { }
    public unsafe void OnPlayerLogout()
    {
        this.RestoreAllColors();
        this.slotState.Clear();
        this.activeTintBySlot.Clear();
    }
    public void InvalidateCache() { }

    private unsafe void RestoreAllSlots()
    {
        foreach (var (addr, st) in this.slotState)
        {
            if (st.AppliedCmd != st.OrigCmd)
            {
                var slot = (RaptureHotbarModule.HotbarSlot*)addr;
                RestoreSlotCommand(slot, st.OrigType, st.OrigCmd);
            }
        }
        this.slotState.Clear();
        this.activeTintBySlot.Clear();
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkUpdate;
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostDraw, this.OnActionBarPostDraw);
        this.RestoreAllColors();
        this.RestoreAllSlots();
        Plugin.Log.Information("PotionSwapper stopped.");
    }

    private uint FindBestPotion(PotionInfo originalPotion, uint maxHp, uint maxMp, float hpPercent, float mpPercent)
    {
        var candidates = this.BuildCandidatePool(originalPotion, maxHp, maxMp, hpPercent, mpPercent);
        if (candidates.Count == 0)
            return 0;

        candidates.Sort((a, b) => b.effectiveness.CompareTo(a.effectiveness));
        uint bestItemId = 0;

        foreach (var candidate in candidates)
        {
            var count = this.GetItemCount(candidate.potion.ItemId);
            if (count <= 0)
                continue;
            // dd potions have their own cd, regular ones all share the same cd
            if (!candidate.potion.IsDeepDungeonOnly && !ServiceContainer.CooldownTracker.IsPotionReady)
                continue;
            bestItemId = candidate.potion.ItemId;
            break;
        }

        return bestItemId;
    }

    private List<(PotionInfo potion, uint effectiveness)> BuildCandidatePool(
        PotionInfo originalPotion, uint maxHp, uint maxMp, float hpPercent, float mpPercent)
    {
        var result = new List<(PotionInfo, uint)>();
        var dutyContext = ServiceContainer.DutyContextTracker;

        bool isHpSlot = originalPotion.Category is PotionCategory.HpRecovery or PotionCategory.EurekaStandard;
        bool isElixirSlot = originalPotion.Category == PotionCategory.HpMpRecovery;
        bool isDdSlot = originalPotion.Category == PotionCategory.DeepDungeonHp;

        bool ddSlotIncludesHpPool = !dutyContext.IsInDeepDungeon
            || this.configuration.DeepDungeonMode == DeepDungeonPotionMode.Enable;

        if (isHpSlot || (isDdSlot && ddSlotIncludesHpPool))
        {
            foreach (var p in PotionDatabase.GetStandardHpPotions())
                result.Add((p, p.GetHealForHp(maxHp)));

            if (dutyContext.IsInEureka && !dutyContext.IsInDeepDungeon)
            {
                var eureka = PotionDatabase.GetEurekaPotion();
                if (eureka.HasValue) result.Add((eureka.Value, eureka.Value.GetHealForHp(maxHp)));
            }

            if (this.configuration.ElixirMode == ElixirMode.Enable)
            {
                foreach (var p in PotionDatabase.GetElixirPotions())
                {
                    var eff = p.GetHealForHp(maxHp);
                    if (this.configuration.ElixirPriority == ElixirPriority.Last)
                        eff = Math.Max(1, eff / 10);
                    result.Add((p, eff));
                }
            }
        }

        if (isElixirSlot && this.configuration.ElixirMode == ElixirMode.Separate)
        {
            foreach (var p in PotionDatabase.GetElixirPotions())
                result.Add((p, p.GetHealForHp(maxHp)));
        }

        // enable mode folds the dd potion into the global pool, so we need it in here even when
        // the slot started as a normal hp potion. AddDeepDungeonCandidates handles the mode checks
        if (dutyContext.IsInDeepDungeon)
        {
            this.AddDeepDungeonCandidates(result, originalPotion, maxHp, dutyContext);
        }

        return result;
    }

    private void AddDeepDungeonCandidates(List<(PotionInfo, uint)> result, PotionInfo originalPotion, uint maxHp, DutyContextTracker dutyContext)
    {
        if (this.configuration.DeepDungeonMode == DeepDungeonPotionMode.Disable)
            return;
        if (!dutyContext.IsInDeepDungeon)
            return;

        var ddPotion = PotionDatabase.GetPotionForDeepDungeon(dutyContext.CurrentDeepDungeon);
        if (!ddPotion.HasValue || this.IsDeepDungeonBuffActive())
            return;

        var potion = ddPotion.Value;

        if (this.configuration.DeepDungeonMode == DeepDungeonPotionMode.Enable)
        {
            if (originalPotion.Category is PotionCategory.HpRecovery or PotionCategory.EurekaStandard or PotionCategory.DeepDungeonHp)
                result.Add((potion, potion.GetHealForHp(maxHp)));
        }
        else if (this.configuration.DeepDungeonMode == DeepDungeonPotionMode.Separate)
        {
            if (originalPotion.IsDeepDungeonOnly)
                result.Add((potion, potion.GetHealForHp(maxHp)));
        }
    }

    // the regen buff check is still a stub, never got the status ids wired up. always returns false
    private bool IsDeepDungeonBuffActive()
    {
        return false;
    }

    private unsafe int GetItemCount(uint itemId)
    {
        var invManager = FFXIVClientStructs.FFXIV.Client.Game.InventoryManager.Instance();
        if (invManager == null)
            return 0;

        uint nqId = itemId >= PotionInfo.HqOffset ? itemId - PotionInfo.HqOffset : itemId;
        uint hqId = nqId + PotionInfo.HqOffset;

        int count = (int)invManager->GetInventoryItemCount(nqId);
        if (nqId < 1_000_000)
            count += (int)invManager->GetInventoryItemCount(hqId);
        return count;
    }

    private unsafe void OnActionBarPostDraw(AddonEvent eventType, AddonArgs args)
    {
        var player = Plugin.ObjectTable.LocalPlayer;
        if (player == null) return;

        try
        {
            var addon = (AddonActionBarBase*)args.Addon.Address;
            if (this.configuration.EnableIconTinting)
                this.ApplyColorsToAddon(addon, args.AddonName);
            else if (this.tintedSlots.Count > 0)
                this.RestoreAllColors();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Icon update failed");
        }
    }

    private unsafe void ApplyColorsToAddon(AddonActionBarBase* addon, string addonName)
    {
        var hotbarModule = RaptureHotbarModule.Instance();
        if (hotbarModule == null) return;

        var hotbarId = addon->RaptureHotbarId;
        var slotCount = addon->SlotCount;
        if (slotCount == 0) return;

        for (int si = 0; si < slotCount; si++)
        {
            var imageNode = GetIconImageNode(addon, si);
            if (imageNode == null) continue;

            var slotKey = (addonName, si);
            var hotbarSlot = hotbarModule->GetSlotById(hotbarId, (uint)si);

            if (hotbarSlot != null &&
                this.activeTintBySlot.TryGetValue((nint)hotbarSlot, out var tint))
            {
                var curR = imageNode->MultiplyRed;
                var curG = imageNode->MultiplyGreen;
                var curB = imageNode->MultiplyBlue;

                SlotTintState st;
                if (!this.tintedSlots.TryGetValue(slotKey, out st) ||
                    curR != st.WroteR || curG != st.WroteG || curB != st.WroteB)
                {
                    st.GameR = curR;
                    st.GameG = curG;
                    st.GameB = curB;
                }

                // lerp so the original art shows through, a flat recolor looked like garbage.
                // 0.45 means the target color only pulls 45% of the way in
                const float t = 0.45f;

                byte targetR = (byte)((tint.Color >> 16) & 0xFF);
                byte targetG = (byte)((tint.Color >> 8) & 0xFF);
                byte targetB = (byte)(tint.Color & 0xFF);

                byte mulR = (byte)Math.Clamp((int)Math.Round(st.GameR * (1f - t)), 0, 255);
                byte mulG = (byte)Math.Clamp((int)Math.Round(st.GameG * (1f - t)), 0, 255);
                byte mulB = (byte)Math.Clamp((int)Math.Round(st.GameB * (1f - t)), 0, 255);

                short addR = (short)Math.Clamp((int)Math.Round(targetR * t), 0, 255);
                short addG = (short)Math.Clamp((int)Math.Round(targetG * t), 0, 255);
                short addB = (short)Math.Clamp((int)Math.Round(targetB * t), 0, 255);

                imageNode->MultiplyRed = mulR;
                imageNode->MultiplyGreen = mulG;
                imageNode->MultiplyBlue = mulB;
                imageNode->AddRed = addR;
                imageNode->AddGreen = addG;
                imageNode->AddBlue = addB;

                st.WroteR = mulR;
                st.WroteG = mulG;
                st.WroteB = mulB;
                this.tintedSlots[slotKey] = st;
            }
            else if (this.tintedSlots.TryGetValue(slotKey, out var st))
            {
                imageNode->MultiplyRed = st.GameR;
                imageNode->MultiplyGreen = st.GameG;
                imageNode->MultiplyBlue = st.GameB;
                imageNode->AddRed = 0;
                imageNode->AddGreen = 0;
                imageNode->AddBlue = 0;
                this.tintedSlots.Remove(slotKey);
            }
        }
    }

    private static unsafe AtkImageNode* GetIconImageNode(AddonActionBarBase* addon, int si)
    {
        var dragDrop = addon->ActionBarSlotVector[si].ComponentDragDrop;
        if (dragDrop == null) return null;
        var iconComponent = dragDrop->AtkComponentIcon;
        if (iconComponent == null) return null;
        return iconComponent->IconImage;
    }

    private unsafe void RestoreAllColors()
    {
        foreach (var ((addonName, si), st) in this.tintedSlots)
        {
            var addonWrapper = Plugin.GameGui.GetAddonByName(addonName);
            if (addonWrapper.IsNull) continue;

            var addon = (AddonActionBarBase*)addonWrapper.Address;
            if (si >= addon->SlotCount) continue;

            var imageNode = GetIconImageNode(addon, si);
            if (imageNode == null) continue;

            imageNode->MultiplyRed = st.GameR;
            imageNode->MultiplyGreen = st.GameG;
            imageNode->MultiplyBlue = st.GameB;
            imageNode->AddRed = 0;
            imageNode->AddGreen = 0;
            imageNode->AddBlue = 0;
        }

        this.tintedSlots.Clear();
        this.activeTintBySlot.Clear();
    }
}
