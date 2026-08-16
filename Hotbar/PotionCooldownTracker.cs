using System;

namespace PotionSwapper.Hotbar;

public sealed class PotionCooldownTracker : IDisposable
{
    private const int PotionCooldownGroup = 8;
    private const int PaddingMs = 25;

    private long cooldownExpiryTicks = 0;

    public unsafe bool IsPotionReady
    {
        get
        {
            if (this.cooldownExpiryTicks <= 0)
                return true;
            // fast path so we dont hammer the native call every frame
            if (Environment.TickCount64 < this.cooldownExpiryTicks)
                return false;
            this.Refresh();
            return this.cooldownExpiryTicks <= 0;
        }
    }

    public unsafe void Refresh()
    {
        var am = FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance();
        if (am == null)
        {
            this.cooldownExpiryTicks = 0;
            return;
        }

        // recast group 8 is potions. took forever to find which group it actually was
        var remainingMs = (long)am->GetRecastTimeForGroup(PotionCooldownGroup);
        this.cooldownExpiryTicks = remainingMs <= 0 ? 0 : Environment.TickCount64 + remainingMs + PaddingMs;
    }

    public void Reset() => this.cooldownExpiryTicks = 0;
    public void Dispose() { }
}
