using Dalamud.Game.Command;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game;
using Lumina.Excel.Sheets;

using PotionSwapper.Data;
using PotionSwapper.Hotbar;
using PotionSwapper.Windows;

namespace PotionSwapper;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;
    [PluginService] internal static ISigScanner SigScanner { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IDutyState DutyState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string Command = "/pswap";

    private readonly PluginConfiguration configuration;
    private PotionIconReplacer? iconReplacer;
    private PotionCooldownTracker? cooldownTracker;
    private DutyContextTracker? dutyContextTracker;
    private readonly WindowSystem windowSystem;
    private readonly ConfigWindow configWindow;

    public string Name => "PotionSwapper";

    public Plugin()
    {
        this.configuration = PluginInterface.GetPluginConfig() as PluginConfiguration ?? new();

        ServiceContainer.DutyContextTracker = this.dutyContextTracker = new();
        ServiceContainer.CooldownTracker = this.cooldownTracker = new();
        this.iconReplacer = new(this.configuration);

        this.windowSystem = new("PotionSwapper.WindowSystem");
        this.configWindow = new(this.configuration);
        this.windowSystem.AddWindow(this.configWindow);

        PluginInterface.UiBuilder.Draw += this.windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += () => this.configWindow.Toggle();

        CommandManager.AddHandler(Command, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the PotionSwapper configuration window.",
            ShowInHelp = true,
        });

        // if you add a hook here you BETTER unhook it in dispose or the game crashes on reload
        ClientState.Login += OnLogin;
        ClientState.Logout += OnLogout;
        Framework.Update += FrameworkOnUpdate;
        DutyState.DutyStarted += DutyStateOnDutyStarted;
        DutyState.DutyCompleted += DutyStateOnDutyCompleted;
        DutyState.DutyWiped += DutyStateOnDutyCompleted;
        Log.Information("PotionSwapper loaded.");
    }

    public void Dispose()
    {
        Log.Information("PotionSwapper unloading.");
        CommandManager.RemoveHandler(Command);
        PluginInterface.UiBuilder.Draw -= this.windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= () => this.configWindow.Toggle();
        ClientState.Login -= OnLogin;
        ClientState.Logout -= OnLogout;
        Framework.Update -= FrameworkOnUpdate;
        DutyState.DutyStarted -= DutyStateOnDutyStarted;
        DutyState.DutyCompleted -= DutyStateOnDutyCompleted;
        DutyState.DutyWiped -= DutyStateOnDutyCompleted;
        this.iconReplacer?.Dispose();
        this.cooldownTracker?.Dispose();
        this.configWindow.Dispose();
        this.windowSystem.RemoveAllWindows();
        PluginInterface.SavePluginConfig(this.configuration);
    }

    // login resets the cd so we dont swap with a stale cooldown from last session
    private void OnLogin() => this.cooldownTracker?.Reset();
    private void OnLogout(int _, int __) => this.iconReplacer?.OnPlayerLogout();
    private void OnCommand(string _, string __) => this.configWindow.IsOpen = !this.configWindow.IsOpen;

    private void FrameworkOnUpdate(IFramework framework)
    {
        if (!ClientState.IsLoggedIn)
            return;
        this.iconReplacer?.OnFrameworkUpdate(framework);
    }

    // both duty events just nuke the cache, started and completed do the same thing honestly
    private void DutyStateOnDutyStarted(Dalamud.Game.DutyState.IDutyStateEventArgs args)
    {
        this.iconReplacer?.InvalidateCache();
    }

    private void DutyStateOnDutyCompleted(Dalamud.Game.DutyState.IDutyStateEventArgs args)
    {
        this.iconReplacer?.InvalidateCache();
    }
}
