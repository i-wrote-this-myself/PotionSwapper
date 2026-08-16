using System;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace PotionSwapper.Hotbar;

public sealed class PotionCooldownTracker : IDisposable
{
    // all regular potions share one cd group so a stand-in potion covers em all
    private const uint PotionItemId = 4554; // X-Potion

    public unsafe bool IsPotionReady
    {
        get
        {
            var am = ActionManager.Instance();
            if (am == null)
                return true;
            // GetRecastTime returns seconds left, 0 = off cd. no more guessin at group numbers
            return am->GetRecastTime(ActionType.Item, PotionItemId) <= 0;
        }
    }

    public void Refresh() { }
    public void Reset() { }
    public void Dispose() { }
}
