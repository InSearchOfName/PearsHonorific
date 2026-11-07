using Xunit;
using System.Numerics;
using System.Text.Json;
using PearHonorific.Core;
using PearHonorific.Pear;

namespace PearHonorific.Tests;

public class FitTests
{
    private static readonly string Long = new string('a', 20) + new string('b', 20); // 40 chars

    [Fact]
    public void ShortTextIsReturnedUnchanged()
    {
        var result = TitleRenderingService.Fit("hello", scroll: true, 5, 2f, out var truncated);
        Assert.Equal("hello", result);
        Assert.False(truncated);
    }

    [Fact]
    public void LongTextIsCutOffWhenNotScrolling()
    {
        var result = TitleRenderingService.Fit(Long, scroll: false, 0, 2f, out var truncated);
        Assert.Equal(Long[..TitleRenderingService.MaxTitleLength], result);
        Assert.True(truncated);
    }

    [Fact]
    public void ScrollingStartsAtTheBeginning()
    {
        var result = TitleRenderingService.Fit(Long, scroll: true, 0, 2f, out var truncated);
        Assert.Equal(Long[..TitleRenderingService.MaxTitleLength], result);
        Assert.False(truncated);
    }

    [Fact]
    public void ScrollingMovesByCharsPerSecond()
    {
        var result = TitleRenderingService.Fit(Long, scroll: true, 2, 2f, out _);
        Assert.Equal(Long.Substring(4, TitleRenderingService.MaxTitleLength), result);
    }

    [Fact]
    public void ScrollingWrapsAroundPastTheEnd()
    {
        // 40 chars + 3 char gap = 43 loop length; offset 40 shows the gap then the start again.
        var result = TitleRenderingService.Fit(Long, scroll: true, 20, 2f, out _);
        Assert.StartsWith("•", result);
        Assert.True(result.Length <= TitleRenderingService.MaxTitleLength);
    }

    [Fact]
    public void ScrollSpeedHasAMinimum()
    {
        // 0 chars/sec is raised to 0.1, so 100s moves 10 characters.
        var result = TitleRenderingService.Fit(Long, scroll: true, 100, 0f, out _);
        Assert.Equal(Long[10..] + " •", result);
    }
}

public class RenderTests
{
    private static SongInfo Song() => new()
    {
        Title = "Song", Artist = "Artist", Album = "Album", SongDuration = 200, Url = "http://x",
    };

    [Fact]
    public void RendersSongAndContextVariables()
    {
        var svc = new TitleRenderingService();
        var text = svc.Render("{{ song.title }} - {{ song.artist }} {{ context.secs_elapsed }}", Song(), 12.7, out var error);
        Assert.Null(error);
        Assert.Equal("Song - Artist 12", text);
    }

    [Fact]
    public void ReparsesWhenTemplateChanges()
    {
        var svc = new TitleRenderingService();
        Assert.Equal("Song", svc.Render("{{ song.title }}", Song(), 0, out _));
        Assert.Equal("Artist", svc.Render("{{ song.artist }}", Song(), 0, out _));
    }

    [Fact]
    public void ReturnsNullAndErrorOnSyntaxError()
    {
        var svc = new TitleRenderingService();
        Assert.Null(svc.Render("{{ if }}", Song(), 0, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void ReturnsNullOnEmptyOutput()
    {
        var svc = new TitleRenderingService();
        Assert.Null(svc.Render("   ", Song(), 0, out var error));
        Assert.Null(error);
    }
}

public class SerializeTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void PlainConfigHasOnlyTitleAndPrefix()
    {
        var root = Parse(TitleRenderingService.Serialize("T", new Configuration { IsPrefix = true }, 0));
        Assert.Equal("T", root.GetProperty("Title").GetString());
        Assert.True(root.GetProperty("IsPrefix").GetBoolean());
        Assert.False(root.TryGetProperty("Color", out _));
        Assert.False(root.TryGetProperty("Glow", out _));
    }

    [Fact]
    public void CustomColorIsIncluded()
    {
        var cfg = new Configuration { UseColor = true, Color = new Vector3(1f, 0f, 0f) };
        var root = Parse(TitleRenderingService.Serialize("T", cfg, 0));
        Assert.True(root.TryGetProperty("Color", out _));
    }

    [Fact]
    public void RainbowOverridesColor()
    {
        var plain = new Configuration { UseColor = true, Color = new Vector3(0f, 0f, 1f) };
        var rainbow = new Configuration { UseColor = true, Color = new Vector3(0f, 0f, 1f), RainbowMode = true };
        Assert.NotEqual(
            TitleRenderingService.Serialize("T", plain, 0),
            TitleRenderingService.Serialize("T", rainbow, 0));
    }

    [Fact]
    public void GlowWithoutGradientHasNoGradientFields()
    {
        var root = Parse(TitleRenderingService.Serialize("T", new Configuration { UseGlow = true }, 0));
        Assert.True(root.TryGetProperty("Glow", out _));
        Assert.False(root.TryGetProperty("Color3", out _));
    }

    [Fact]
    public void GlowGradientIncludesSecondColourAndStyle()
    {
        var cfg = new Configuration { UseGlow = true, UseGlowGradient = true, GlowAnimation = GlowAnimation.Pulse };
        var root = Parse(TitleRenderingService.Serialize("T", cfg, 0));
        Assert.True(root.TryGetProperty("Color3", out _));
        Assert.Equal(-1, root.GetProperty("GradientColourSet").GetInt32());
        Assert.Equal(0, root.GetProperty("GradientAnimationStyle").GetInt32());
    }
}

public class HsvToRgbTests
{
    [Theory]
    [InlineData(0.00f, 1f, 0f, 0f)]
    [InlineData(0.20f, 0.8f, 1f, 0f)]
    [InlineData(0.40f, 0f, 1f, 0.4f)]
    [InlineData(0.55f, 0f, 0.7f, 1f)]
    [InlineData(0.70f, 0.2f, 0f, 1f)]
    [InlineData(0.90f, 1f, 0f, 0.6f)]
    public void HueMapsToExpectedColour(float h, float r, float g, float b)
    {
        var c = TitleRenderingService.HsvToRgb(h, 1f, 1f);
        Assert.Equal(r, c.X, 0.05f);
        Assert.Equal(g, c.Y, 0.05f);
        Assert.Equal(b, c.Z, 0.05f);
    }
}

public class PearModelsTests
{
    [Fact]
    public void DeserializesPlayerInfoCaseInsensitively()
    {
        const string json = """{"Type":"PLAYER_INFO","song":{"title":"t","artist":"a","videoId":"v","songDuration":10},"isPlaying":true,"position":3.5}""";
        var msg = JsonSerializer.Deserialize<PearMessage>(json, PearJson.Options);
        Assert.NotNull(msg);
        Assert.Equal("PLAYER_INFO", msg.Type);
        Assert.Equal("t", msg.Song?.Title);
        Assert.Equal("v", msg.Song?.VideoId);
        Assert.True(msg.IsPlaying);
        Assert.Equal(3.5, msg.Position);
    }

    [Fact]
    public void DeserializesAuthResponse()
    {
        var auth = JsonSerializer.Deserialize<AuthResponse>("""{"accessToken":"abc"}""", PearJson.Options);
        Assert.Equal("abc", auth?.AccessToken);
    }
}
