using System;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using PearHonorific.Core;
using PearHonorific.Pear;
using PearHonorific.Windows;

namespace PearHonorific;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/pearhonorific";
    private const uint AfkOnlineStatusId = 17;
    private const double TextRenderIntervalSecs = 0.5;
    private const double RainbowRenderIntervalSecs = 0.1;
    private const double AfkGraceSecs = 10;
    private const double DisconnectGraceSecs = 60;

    public Configuration Configuration { get; init; }
    public PearApiClient Pear { get; init; }
    public string? LastTitle { get; private set; }
    public string? LastError { get; private set; }
    public bool HonorificReady { get; private set; } = true;

    public readonly WindowSystem WindowSystem = new("PearHonorific");
    private ConfigWindow ConfigWindow { get; init; }

    private readonly HonorificIpc honorific;
    private readonly TitleRenderingService renderer = new();

    private string? lastSentJson;
    private DateTime lastRenderUtc = DateTime.MinValue;
    private DateTime notAfkSinceUtc = DateTime.UtcNow;
    private bool warnedTruncate;
    private DateTime scrollStartUtc = DateTime.UtcNow;
    private string? lastSongId;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        honorific = new HonorificIpc(PluginInterface);
        Pear = new PearApiClient(Configuration);

        ConfigWindow = new ConfigWindow(this);
        WindowSystem.AddWindow(ConfigWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the Pear Honorific settings. Use '/pearhonorific toggle' to enable/disable.",
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleConfigUi;

        Framework.Update += OnFrameworkUpdate;
        ClientState.TerritoryChanged += OnTerritoryChanged;

        if (Configuration.Enabled) Pear.Start();
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        ClientState.TerritoryChanged -= OnTerritoryChanged;
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleConfigUi;

        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        CommandManager.RemoveHandler(CommandName);

        ClearTitle();
        Pear.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        if (args.Trim().Equals("toggle", StringComparison.OrdinalIgnoreCase))
        {
            SetEnabled(!Configuration.Enabled);
            ChatGui.Print($"[Pear Honorific] {(Configuration.Enabled ? "Enabled" : "Disabled")}.");
            return;
        }
        ConfigWindow.Toggle();
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();

    public void SetEnabled(bool enabled)
    {
        Configuration.Enabled = enabled;
        Configuration.Save();
        if (enabled) Pear.Start();
        else
        {
            Pear.Stop();
            ClearTitle();
        }
    }

    /// <summary>Call after settings that affect the rendered payload change.</summary>
    public void ForceResend() => lastSentJson = null;

    private void OnTerritoryChanged(uint territory) => ForceResend();

    private bool IsAfk() => ObjectTable.LocalPlayer?.OnlineStatus.RowId == AfkOnlineStatusId;

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!Configuration.Enabled) return;

        var now = DateTime.UtcNow;
        var afk = IsAfk();
        if (!afk) notAfkSinceUtc = now;

        var song = Pear.CurrentSong;
        var connected = Pear.Status == PearStatus.Connected;
        var disconnectedTooLong = !connected && (now - Pear.LastConnectedUtc).TotalSeconds > DisconnectGraceSecs;

        var shouldClear = song == null
            || disconnectedTooLong
            || (Configuration.ClearWhenPaused && !Pear.IsPlaying)
            || (Configuration.PauseWhenAfk && afk && (now - notAfkSinceUtc).TotalSeconds > AfkGraceSecs);

        if (shouldClear || song == null)
        {
            ClearTitle();
            return;
        }

        if (song.VideoId != lastSongId)
        {
            lastSongId = song.VideoId;
            warnedTruncate = false;
            scrollStartUtc = now;
        }

        var interval = Configuration.RainbowMode ? RainbowRenderIntervalSecs : TextRenderIntervalSecs;
        if (Configuration.ScrollLongTitles)
            interval = Math.Min(interval, 1.0 / Math.Max(Configuration.ScrollSpeed, 0.1f));
        if ((now - lastRenderUtc).TotalSeconds < interval) return;
        lastRenderUtc = now;

        var elapsed = Pear.ElapsedSeconds;
        var full = renderer.Render(Configuration.TitleTemplate, song, elapsed, out var error);
        LastError = error;
        if (full == null)
        {
            ClearTitle();
            return;
        }

        var scrollSecs = (now - scrollStartUtc).TotalSeconds;
        var title = TitleRenderingService.Fit(full, Configuration.ScrollLongTitles, scrollSecs, Configuration.ScrollSpeed, out var truncated);
        if (truncated && !warnedTruncate)
        {
            warnedTruncate = true;
            Log.Information($"Title truncated to {TitleRenderingService.MaxTitleLength} characters: {title}");
        }

        var json = TitleRenderingService.Serialize(title, Configuration, elapsed);
        if (json == lastSentJson) return;

        HonorificReady = honorific.Set(json);
        if (HonorificReady)
        {
            lastSentJson = json;
            LastTitle = title;
        }
    }

    private void ClearTitle()
    {
        if (lastSentJson == null && LastTitle == null) return;
        honorific.Clear();
        lastSentJson = null;
        LastTitle = null;
    }
}
