using PotionSwapper.Data;
using PotionSwapper.Hotbar;

namespace PotionSwapper;

// just a dumb holder so the replacer and the tracker can find each other without
// threading them all through constructors. static is ugly but it works for a plugin
internal static class ServiceContainer
{
    internal static DutyContextTracker? DutyContextTracker { get; set; }
    internal static PotionCooldownTracker? CooldownTracker { get; set; }
}
