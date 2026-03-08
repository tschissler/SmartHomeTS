using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using BMWConnector.Models;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Formatter;
using MQTTnet.Protocol;

namespace BMWConnector.Services;

/// <summary>
/// Connects to BMW's CarData MQTT broker, subscribes to vehicle telemetry,
/// aggregates incoming field updates into a VehicleState, and republishes
/// the state as JSON to the local Mosquitto broker.
/// </summary>
public class BmwCarDataService : BackgroundService
{
    private const string BmwBrokerHost = "customer.streaming-cardata.bmwgroup.com";
    private const int BmwBrokerPort = 9000;

    private readonly VehicleConfig _config;
    private readonly TokenService _tokenService;
    private readonly HealthRegistry _health;
    private readonly ILogger<BmwCarDataService> _log;
    private readonly VehicleState _state;

    private readonly string _localMqttHost;
    private readonly int _localMqttPort;
    private readonly bool _debugRaw;

    private IMqttClient? _localClient;
    private int _consecutiveAuthFailures = 0;

    public BmwCarDataService(VehicleConfig config, TokenService tokenService, HealthRegistry health, ILogger<BmwCarDataService> log)
    {
        _config = config;
        _tokenService = tokenService;
        _health = health;
        _log = log;
        _state = new VehicleState(config.Name);
        _localMqttHost = Environment.GetEnvironmentVariable("MQTT_BROKER") ?? "mosquitto.intern";
        _localMqttPort = int.Parse(Environment.GetEnvironmentVariable("MQTT_PORT") ?? "1883");
        _debugRaw = Environment.GetEnvironmentVariable("BMW_DEBUG_RAW") == "true";
        _health.SetConnected(config.Name, false);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            await _tokenService.LoadAsync(ct);
        }
        catch (Exception ex)
        {
            _log.LogError("[{Vehicle}] Failed to load tokens from Kubernetes Secret: {Message}", _config.Name, ex.Message);
            _log.LogError("[{Vehicle}] Re-run bootstrap: dotnet run -- --bootstrap {Vehicle}", _config.Name, _config.Name);
            return;
        }

        try
        {
            _localClient = await ConnectLocalBrokerAsync(ct);
        }
        catch (Exception ex)
        {
            _log.LogError("[{Vehicle}] Failed to connect to local MQTT broker ({Host}:{Port}): {Message}",
                _config.Name, _localMqttHost, _localMqttPort, ex.Message);
            return;
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RunBmwConnectionAsync(ct);
                _consecutiveAuthFailures = 0; // successful cycle — reset counter
            }
            catch (OperationCanceledException) { break; }
            catch (MQTTnet.Adapter.MqttConnectingFailedException ex)
            {
                _consecutiveAuthFailures++;
                _log.LogError("[{Vehicle}] BMW broker rejected the connection ({Count}x): {Reason}",
                    _config.Name, _consecutiveAuthFailures, ex.ResultCode);

                // On first rejection, try a token refresh — the id_token may simply have expired
                if (_consecutiveAuthFailures == 1)
                {
                    _log.LogInformation("[{Vehicle}] Attempting token refresh before retrying...", _config.Name);
                    try
                    {
                        await _tokenService.RefreshAsync(ct);
                        _log.LogInformation("[{Vehicle}] Token refreshed, retrying connection immediately...", _config.Name);
                        continue;
                    }
                    catch (BmwAuthExpiredException)
                    {
                        // refresh token also expired — fall through to re-bootstrap message
                    }
                    catch (Exception refreshEx)
                    {
                        _log.LogError("[{Vehicle}] Token refresh also failed: {Message}", _config.Name, refreshEx.Message);
                    }
                }

                LogRebootstrapInstructions();
                await Task.Delay(TimeSpan.FromMinutes(5), ct);
            }
            catch (BmwAuthExpiredException)
            {
                // Refresh token expired during a running session (proactive refresh after 50 min)
                LogRebootstrapInstructions();
                await Task.Delay(TimeSpan.FromMinutes(5), ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "[{Vehicle}] BMW connection error, retrying in 30s...", _config.Name);
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            }
        }
    }

    private async Task RunBmwConnectionAsync(CancellationToken ct)
    {
        if (_tokenService.NeedsRefresh())
            await _tokenService.RefreshAsync(ct);

        var factory = new MqttFactory();
        using var bmwClient = factory.CreateMqttClient();

        bmwClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(BmwBrokerHost, BmwBrokerPort)
            .WithTlsOptions(o => o.WithSslProtocols(SslProtocols.Tls12 | SslProtocols.Tls13))
            .WithProtocolVersion(MqttProtocolVersion.V500)
            .WithCredentials(_config.Gcid, _tokenService.IdToken)
            .WithClientId($"smarthome-{_config.Name.ToLower()}-{Guid.NewGuid():N}")
            .WithCleanSession()
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(30))
            .Build();

        _log.LogInformation("[{Vehicle}] Connecting to BMW CarData broker...", _config.Name);
        await bmwClient.ConnectAsync(options, ct);
        _log.LogInformation("[{Vehicle}] Connected. Subscribing to {Gcid}/+", _config.Name, _config.Gcid);

        await bmwClient.SubscribeAsync($"{_config.Gcid}/+", MqttQualityOfServiceLevel.AtMostOnce, ct);
        _log.LogInformation("[{Vehicle}] Subscribed to {Gcid}/+ — waiting for data...", _config.Name, _config.Gcid);
        _health.SetConnected(_config.Name, true);

        // Run until disconnected or token needs refresh
        var tokenRefreshTimer = new PeriodicTimer(TimeSpan.FromMinutes(50));
        while (!ct.IsCancellationRequested && bmwClient.IsConnected)
        {
            if (await tokenRefreshTimer.WaitForNextTickAsync(ct))
            {
                _log.LogInformation("[{Vehicle}] Proactive token refresh, reconnecting...", _config.Name);
                break;
            }
        }

        _health.SetConnected(_config.Name, false);

        if (bmwClient.IsConnected)
            await bmwClient.DisconnectAsync(cancellationToken: ct);

        if (!ct.IsCancellationRequested)
        {
            try
            {
                await _tokenService.RefreshAsync(ct);
            }
            catch (BmwAuthExpiredException)
            {
                throw; // propagate so ExecuteAsync shows the re-bootstrap message
            }
            catch (Exception ex)
            {
                _log.LogWarning("[{Vehicle}] Post-session token refresh failed, will retry with existing token: {Message}",
                    _config.Name, ex.Message);
            }
        }
    }

    private void LogRebootstrapInstructions()
    {
        _log.LogError("[{Vehicle}] ══════════════════════════════════════════════════", _config.Name);
        _log.LogError("[{Vehicle}] Authentication failed — refresh token may have expired.", _config.Name);
        _log.LogError("[{Vehicle}] Re-authenticate from your local machine:", _config.Name);
        _log.LogError("[{Vehicle}]   cd BMWConnector && dotnet run -- --bootstrap {Vehicle}", _config.Name, _config.Name);
        _log.LogError("[{Vehicle}] Then restart the pod:", _config.Name);
        _log.LogError("[{Vehicle}]   kubectl -n smarthome rollout restart deployment/bmwconnector", _config.Name);
        _log.LogError("[{Vehicle}] Will retry in 5 minutes.", _config.Name);
        _log.LogError("[{Vehicle}] ══════════════════════════════════════════════════", _config.Name);
    }

    private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            string json = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

            if (_debugRaw)
                await PublishRawAsync(json);

            var payload = JsonSerializer.Deserialize<BmwPayload>(json);
            if (payload?.Data == null) return;

            foreach (var (field, dataPoint) in payload.Data)
            {
                bool recognized = _state.Apply(field, dataPoint);
                if (recognized)
                    _log.LogDebug("[{Vehicle}] {Field} = {Value}", _config.Name, field, dataPoint.Value.ToString());
                else
                    _log.LogInformation("[{Vehicle}] Unknown field (not mapped): {Field} ({Kind}) = {Value}", _config.Name, field, dataPoint.Value.ValueKind, dataPoint.Value.ToString());
            }

            await PublishStateAsync();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[{Vehicle}] Error processing BMW message", _config.Name);
        }
    }

    private async Task PublishRawAsync(string json)
    {
        if (_localClient == null || !_localClient.IsConnected) return;

        var message = new MqttApplicationMessageBuilder()
            .WithTopic($"debug/{_config.Name.ToLower()}/raw")
            .WithPayload(json)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
            .Build();

        await _localClient.PublishAsync(message);
    }

    private async Task PublishStateAsync()
    {
        if (_localClient == null || !_localClient.IsConnected)
        {
            _log.LogWarning("[{Vehicle}] Local broker not connected, skipping publish", _config.Name);
            return;
        }

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(_config.OutputTopic)
            .WithPayload(_state.ToJson())
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(true)
            .Build();

        await _localClient.PublishAsync(message);
        _log.LogInformation("[{Vehicle}] Published to {Topic}", _config.Name, _config.OutputTopic);
    }

    private async Task<IMqttClient> ConnectLocalBrokerAsync(CancellationToken ct)
    {
        var factory = new MqttFactory();
        var client = factory.CreateMqttClient();

        client.DisconnectedAsync += async _ =>
        {
            _log.LogWarning("[{Vehicle}] Local broker disconnected, reconnecting in 5s...", _config.Name);
            await Task.Delay(5000, ct);
            try { await client.ReconnectAsync(ct); }
            catch (Exception ex) { _log.LogError(ex, "[{Vehicle}] Local broker reconnect failed", _config.Name); }
        };

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(_localMqttHost, _localMqttPort)
            .WithProtocolVersion(MqttProtocolVersion.V500)
            .WithClientId($"bmw-connector-{_config.Name.ToLower()}")
            .WithCleanSession()
            .Build();

        await client.ConnectAsync(options, ct);
        _log.LogInformation("[{Vehicle}] Connected to local broker {Host}:{Port}",
            _config.Name, _localMqttHost, _localMqttPort);

        return client;
    }
}
