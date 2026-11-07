using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using PearHonorific.Pear;

namespace PearHonorific.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly Configuration config;

    public ConfigWindow(Plugin plugin) : base("Pear Honorific###PearHonorificConfig")
    {
        this.plugin = plugin;
        config = plugin.Configuration;

        Size = new Vector2(480, 440);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var enabled = config.Enabled;
        if (ImGui.Checkbox("Enabled", ref enabled))
            plugin.SetEnabled(enabled);

        DrawConnection();
        ImGui.Separator();
        DrawTitleSettings();
        ImGui.Separator();
        DrawBehaviour();
    }

    private void DrawConnection()
    {
        ImGui.TextUnformatted("Pear Desktop connection");

        var host = config.Host;
        ImGui.SetNextItemWidth(160);
        if (ImGui.InputText("Host", ref host, 64))
        {
            config.Host = host.Trim();
            config.Save();
        }

        // Typed-in port; defaults to the Pear api-server default.
        var port = config.Port;
        ImGui.SetNextItemWidth(100);
        if (ImGui.InputInt("Port", ref port, 0, 0))
            config.Port = Math.Clamp(port, 1, 65535);
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            config.Save();
            plugin.Pear.Reconnect();
        }

        ImGui.SameLine();
        if (ImGui.Button($"Reset to default ({Configuration.DefaultPort})"))
        {
            config.Port = Configuration.DefaultPort;
            config.Save();
            plugin.Pear.Reconnect();
        }

        if (ImGui.Button("Reconnect / re-authorize"))
        {
            config.AccessToken = string.Empty;
            config.Save();
            plugin.Pear.Reconnect();
        }

        var status = plugin.Pear.Status;
        var color = status switch
        {
            PearStatus.Connected => new Vector4(0.4f, 1f, 0.4f, 1f),
            PearStatus.Disconnected => new Vector4(1f, 0.4f, 0.4f, 1f),
            _ => new Vector4(1f, 0.85f, 0.3f, 1f),
        };
        ImGui.TextColored(color, $"{status}: {plugin.Pear.StatusDetail}");

        if (!plugin.HonorificReady)
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), "Honorific isn't loaded - install and enable it.");

        var song = plugin.Pear.CurrentSong;
        ImGui.TextDisabled(song == null
            ? "Nothing playing"
            : $"{(plugin.Pear.IsPlaying ? "Playing" : "Paused")}: {song.Title} - {song.Artist}");
    }

    private void DrawTitleSettings()
    {
        ImGui.TextUnformatted("Title");

        var template = config.TitleTemplate;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputText("##template", ref template, 512))
        {
            config.TitleTemplate = template;
            config.Save();
        }
        ImGui.TextDisabled("Scriban template. Variables: song.title, song.artist, song.album, song.duration, context.secs_elapsed");
        ImGui.TextDisabled("Example: {{ (song.title | string.truncate 20) }} - {{ song.artist }}");

        var preview = plugin.LastTitle ?? "(none)";
        ImGui.TextUnformatted($"Preview ({preview.Length}/{Core.TitleRenderingService.MaxTitleLength}): {preview}");
        if (plugin.LastError != null)
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), plugin.LastError);

        var scroll = config.ScrollLongTitles;
        if (ImGui.Checkbox("Scroll titles longer than 32 characters", ref scroll))
            Change(() => config.ScrollLongTitles = scroll);
        if (config.ScrollLongTitles)
        {
            ImGui.SameLine();
            var speed = config.ScrollSpeed;
            ImGui.SetNextItemWidth(120);
            if (ImGui.SliderFloat("chars/sec", ref speed, 0.5f, 6f, "%.1f"))
                Change(() => config.ScrollSpeed = speed);

            ImGui.TextColored(new Vector4(1f, 0.85f, 0.3f, 1f),
                "Note: Lightless Sync can't update titles as fast as the speed set here,");
            ImGui.TextColored(new Vector4(1f, 0.85f, 0.3f, 1f),
                "so the scrolling may look choppy or odd to other players.");
        }

        var prefix = config.IsPrefix;
        if (ImGui.Checkbox("Show before name (prefix)", ref prefix))
            Change(() => config.IsPrefix = prefix);

        var useColor = config.UseColor;
        if (ImGui.Checkbox("Custom color", ref useColor))
            Change(() => config.UseColor = useColor);
        if (config.UseColor)
        {
            ImGui.SameLine();
            var c = config.Color;
            if (ImGui.ColorEdit3("##color", ref c, ImGuiColorEditFlags.NoInputs))
                Change(() => config.Color = c);
        }

        var useGlow = config.UseGlow;
        if (ImGui.Checkbox("Glow", ref useGlow))
            Change(() => config.UseGlow = useGlow);
        if (config.UseGlow)
        {
            ImGui.SameLine();
            var g = config.Glow;
            if (ImGui.ColorEdit3("##glow", ref g, ImGuiColorEditFlags.NoInputs))
                Change(() => config.Glow = g);

            ImGui.TextColored(new Vector4(1f, 0.85f, 0.3f, 1f),
                "Note: the glow options (especially the gradient) haven't been fully tested and may not look right.");

            ImGui.Indent();
            var gradient = config.UseGlowGradient;
            if (ImGui.Checkbox("Two-color gradient", ref gradient))
                Change(() => config.UseGlowGradient = gradient);
            if (config.UseGlowGradient)
            {
                ImGui.SameLine();
                var g2 = config.GlowColor2;
                if (ImGui.ColorEdit3("##glow2", ref g2, ImGuiColorEditFlags.NoInputs))
                    Change(() => config.GlowColor2 = g2);

                ImGui.SameLine();
                ImGui.SetNextItemWidth(90);
                var style = (int)config.GlowAnimation;
                if (ImGui.Combo("##glowstyle", ref style, "Pulse\0Wave\0"))
                    Change(() => config.GlowAnimation = (GlowAnimation)style);
                ImGui.TextDisabled("Pulse: the whole glow shifts together. Wave: the colors travel across the text.");
            }
            ImGui.Unindent();
        }

        var rainbow = config.RainbowMode;
        if (ImGui.Checkbox("Rainbow mode (overrides color)", ref rainbow))
            Change(() => config.RainbowMode = rainbow);
    }

    private void DrawBehaviour()
    {
        var clearPaused = config.ClearWhenPaused;
        if (ImGui.Checkbox("Clear title when paused", ref clearPaused))
            Change(() => config.ClearWhenPaused = clearPaused);

        var afk = config.PauseWhenAfk;
        if (ImGui.Checkbox("Clear title while AFK", ref afk))
            Change(() => config.PauseWhenAfk = afk);
    }

    private void Change(Action apply)
    {
        apply();
        config.Save();
        plugin.ForceResend();
    }
}
