using Dalamud.Configuration;
using System;
using System.Numerics;

namespace PearHonorific;

/// <summary>Values match Honorific's GradientAnimationStyle enum.</summary>
public enum GlowAnimation
{
    Pulse = 0, // whole title changes colour together
    Wave = 1,  // colour travels across the characters
}

[Serializable]
public class Configuration : IPluginConfiguration
{
    public const int DefaultPort = 26538; // Pear Desktop api-server default
    public const string DefaultHost = "127.0.0.1";

    public int Version { get; set; } = 0;

    public bool Enabled { get; set; } = true;

    // Pear Desktop api-server connection
    public string Host { get; set; } = DefaultHost;
    public int Port { get; set; } = DefaultPort;
    public string ClientId { get; set; } = "pear-honorific";
    public string AccessToken { get; set; } = string.Empty;

    // Title appearance
    public string TitleTemplate { get; set; } = "♪ {{ song.title }} - {{ song.artist }}";
    public bool IsPrefix { get; set; } = false;
    public bool UseColor { get; set; } = false;
    public Vector3 Color { get; set; } = new(1f, 1f, 1f);
    public bool UseGlow { get; set; } = false;
    public Vector3 Glow { get; set; } = new(1f, 1f, 1f);
    // Honorific two-colour gradient on the glow: Glow is the first colour, GlowColor2 the second
    public bool UseGlowGradient { get; set; } = false;
    public Vector3 GlowColor2 { get; set; } = new(0f, 0.6f, 1f);
    public GlowAnimation GlowAnimation { get; set; } = GlowAnimation.Wave;
    public bool RainbowMode { get; set; } = false;

    // Scroll titles longer than the 32 character limit like a train station board
    public bool ScrollLongTitles { get; set; } = false;
    public float ScrollSpeed { get; set; } = 2f; // characters per second

    // Behaviour
    public bool ClearWhenPaused { get; set; } = true;
    public bool PauseWhenAfk { get; set; } = true;

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
