using System;
using System.Collections.Generic;
using System.Numerics;
using Newtonsoft.Json;
using PearHonorific.Pear;
using Scriban;
using Scriban.Runtime;

namespace PearHonorific.Core;

public sealed class TitleRenderingService
{
    public const int MaxTitleLength = 32; // Honorific's hard limit
    private const float RainbowHueSpeed = 0.5f;

    private Template? template;
    private string? templateSource;

    private const string ScrollGap = " • ";

    /// <summary>
    /// Fits <paramref name="text"/> into <see cref="MaxTitleLength"/>. Short text is returned as is;
    /// long text is either cut off or, when scrolling, a window that moves left one character at a time and wraps around.
    /// </summary>
    public static string Fit(string text, bool scroll, double scrollSecs, float charsPerSecond, out bool truncated)
    {
        truncated = false;
        if (text.Length <= MaxTitleLength) return text;

        if (!scroll)
        {
            truncated = true;
            return text[..MaxTitleLength].TrimEnd();
        }

        var loop = text + ScrollGap;
        var offset = (int)(scrollSecs * Math.Max(charsPerSecond, 0.1f)) % loop.Length;
        var doubled = loop + loop;
        return doubled.Substring(offset, MaxTitleLength).Trim();
    }

    /// <summary>Renders the template; returns null on empty output. Sets <paramref name="error"/> on template errors. Not length limited, see <see cref="Fit"/>.</summary>
    public string? Render(string source, SongInfo song, double elapsedSeconds, out string? error)
    {
        error = null;

        if (template == null || templateSource != source)
        {
            template = Template.Parse(source);
            templateSource = source;
        }

        if (template.HasErrors)
        {
            error = string.Join("; ", template.Messages);
            return null;
        }

        var model = new ScriptObject();
        model.Import(new
        {
            song = new
            {
                title = song.Title,
                artist = song.Artist,
                album = song.Album ?? string.Empty,
                duration = (int)song.SongDuration,
                url = song.Url ?? string.Empty,
            },
            context = new { secs_elapsed = (int)elapsedSeconds },
        }, renamer: m => m.Name);

        var ctx = new TemplateContext { MemberRenamer = m => m.Name };
        ctx.PushGlobal(model);

        string text;
        try { text = template.Render(ctx).Trim(); }
        catch (Exception ex)
        {
            error = ex.Message;
            return null;
        }

        return text.Length == 0 ? null : text;
    }

    /// <summary>JSON payload for Honorific.SetCharacterTitle. Colors are 0-1 floats.</summary>
    public static string Serialize(string title, Configuration config, double secs)
    {
        var data = new Dictionary<string, object?>
        {
            ["Title"] = title,
            ["IsPrefix"] = config.IsPrefix,
        };

        if (config.RainbowMode)
            data["Color"] = HsvToRgb((float)(secs * RainbowHueSpeed) % 1f, 1f, 1f);
        else if (config.UseColor)
            data["Color"] = config.Color;

        if (config.UseGlow)
        {
            data["Glow"] = config.Glow;
            if (config.UseGlowGradient)
            {
                // -1 = custom two colour gradient between Glow and Color3
                data["Color3"] = config.GlowColor2;
                data["GradientColourSet"] = -1;
                data["GradientAnimationStyle"] = (int)config.GlowAnimation;
            }
        }

        return JsonConvert.SerializeObject(data, Formatting.None);
    }

    public static Vector3 HsvToRgb(float h, float s, float v)
    {
        var i = (int)(h * 6);
        var f = h * 6 - i;
        var p = v * (1 - s);
        var q = v * (1 - f * s);
        var t = v * (1 - (1 - f) * s);
        return (i % 6) switch
        {
            0 => new Vector3(v, t, p),
            1 => new Vector3(q, v, p),
            2 => new Vector3(p, v, t),
            3 => new Vector3(p, q, v),
            4 => new Vector3(t, p, v),
            _ => new Vector3(v, p, q),
        };
    }
}
