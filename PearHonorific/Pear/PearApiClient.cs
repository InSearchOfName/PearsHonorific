using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PearHonorific.Pear;

public enum PearStatus
{
    Disconnected,
    Connecting,
    WaitingForApproval,
    Connected,
}

/// <summary>
/// Talks to Pear Desktop's built-in api-server plugin: POST /auth/{id} for a token,
/// then a websocket on /api/v1/ws for now-playing events.
/// </summary>
public sealed class PearApiClient : IDisposable
{
    private readonly Configuration config;
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(60) }; // auth waits on a user dialog
    private readonly object stateLock = new();

    private CancellationTokenSource? cts;
    private SongInfo? song;
    private bool isPlaying;
    private double position;
    private DateTime positionStamp = DateTime.UtcNow;

    public PearApiClient(Configuration config)
    {
        this.config = config;
    }

    public PearStatus Status { get; private set; } = PearStatus.Disconnected;
    public string StatusDetail { get; private set; } = string.Empty;

    public SongInfo? CurrentSong
    {
        get { lock (stateLock) return song; }
    }

    public bool IsPlaying
    {
        get { lock (stateLock) return isPlaying && song != null; }
    }

    /// <summary>Estimated elapsed seconds, extrapolated between position events while playing.</summary>
    public double ElapsedSeconds
    {
        get
        {
            lock (stateLock)
                return isPlaying ? position + (DateTime.UtcNow - positionStamp).TotalSeconds : position;
        }
    }

    /// <summary>Last time we heard from Pear while connected (used for the disconnect grace period).</summary>
    public DateTime LastConnectedUtc { get; private set; } = DateTime.MinValue;

    public void Start()
    {
        Stop();
        cts = new CancellationTokenSource();
        var token = cts.Token;
        _ = Task.Run(() => RunAsync(token), token);
    }

    public void Stop()
    {
        try { cts?.Cancel(); } catch { /* already disposed */ }
        cts?.Dispose();
        cts = null;
        Status = PearStatus.Disconnected;
        lock (stateLock) { song = null; isPlaying = false; position = 0; }
    }

    /// <summary>Restart the connection, e.g. after host/port changes or to retry authorization.</summary>
    public void Reconnect() => Start();

    private async Task RunAsync(CancellationToken ct)
    {
        var delay = TimeSpan.FromSeconds(1);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                Status = PearStatus.Connecting;
                StatusDetail = $"Connecting to {config.Host}:{config.Port}...";

                if (string.IsNullOrEmpty(config.AccessToken))
                    await AuthorizeAsync(ct);

                await ListenAsync(ct);
                delay = TimeSpan.FromSeconds(1);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (UnauthorizedAccessException ex)
            {
                // Token rejected or user denied: drop the token and retry slowly.
                config.AccessToken = string.Empty;
                config.Save();
                StatusDetail = ex.Message;
                delay = TimeSpan.FromSeconds(15);
            }
            catch (Exception ex)
            {
                StatusDetail = $"Can't reach Pear ({ex.GetType().Name}). Is the API Server plugin enabled?";
                Plugin.Log.Debug(ex, "Pear connection failed");
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 30));
            }

            Status = PearStatus.Disconnected;
            lock (stateLock) { isPlaying = false; }
            try { await Task.Delay(delay, ct); } catch (OperationCanceledException) { return; }
        }
    }

    private string BaseUrl => $"{config.Host}:{config.Port}";

    private async Task AuthorizeAsync(CancellationToken ct)
    {
        Status = PearStatus.WaitingForApproval;
        StatusDetail = "Waiting for you to allow the request in Pear Desktop...";

        using var req = new HttpRequestMessage(HttpMethod.Post,
            $"http://{BaseUrl}/auth/{Uri.EscapeDataString(config.ClientId)}");
        using var res = await http.SendAsync(req, ct);

        if (res.StatusCode == HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException("Pear denied the authorization request.");
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadAsStringAsync(ct);
        var auth = JsonSerializer.Deserialize<AuthResponse>(body, PearJson.Options);
        if (string.IsNullOrEmpty(auth?.AccessToken))
            throw new InvalidOperationException("Pear returned no access token.");

        config.AccessToken = auth.AccessToken;
        config.Save();
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        using var ws = new ClientWebSocket();
        var uri = new Uri($"ws://{BaseUrl}/api/v1/ws?token={Uri.EscapeDataString(config.AccessToken)}");
        await ws.ConnectAsync(uri, ct);

        Status = PearStatus.Connected;
        StatusDetail = "Connected";
        LastConnectedUtc = DateTime.UtcNow;

        var buffer = new byte[16 * 1024];
        var sb = new StringBuilder();
        while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            sb.Clear();
            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    if (ws.CloseStatus == WebSocketCloseStatus.PolicyViolation) // 1008 Unauthorized
                        throw new UnauthorizedAccessException("Pear rejected our token; re-authorizing.");
                    return;
                }
                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            } while (!result.EndOfMessage);

            LastConnectedUtc = DateTime.UtcNow;
            HandleMessage(sb.ToString());
        }
    }

    private void HandleMessage(string json)
    {
        PearMessage? msg;
        try { msg = JsonSerializer.Deserialize<PearMessage>(json, PearJson.Options); }
        catch (JsonException ex)
        {
            Plugin.Log.Debug(ex, "Bad Pear message");
            return;
        }
        if (msg == null) return;

        lock (stateLock)
        {
            switch (msg.Type)
            {
                case "PLAYER_INFO":
                    song = msg.Song;
                    isPlaying = msg.IsPlaying ?? false;
                    SetPosition(msg.Position ?? msg.Song?.ElapsedSeconds ?? 0);
                    break;
                case "VIDEO_CHANGED":
                    song = msg.Song;
                    isPlaying = msg.Song?.IsPaused != true;
                    SetPosition(msg.Position ?? 0);
                    break;
                case "PLAYER_STATE_CHANGED":
                    isPlaying = msg.IsPlaying ?? false;
                    if (msg.Position != null) SetPosition(msg.Position.Value);
                    break;
                case "POSITION_CHANGED":
                    if (msg.Position != null) SetPosition(msg.Position.Value);
                    break;
            }
        }
    }

    private void SetPosition(double seconds)
    {
        position = seconds;
        positionStamp = DateTime.UtcNow;
    }

    public void Dispose()
    {
        Stop();
        http.Dispose();
    }
}
