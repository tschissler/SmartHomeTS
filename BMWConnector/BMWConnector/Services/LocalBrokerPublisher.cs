using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Formatter;
using MQTTnet.Protocol;

namespace BMWConnector.Services;

/// <summary>
/// A connection to the local Mosquitto of its own, used by the service heartbeat.
///
/// It does not borrow the client of <see cref="BmwCarDataService"/>: there is one of those per
/// vehicle, both may be down, and the heartbeat has to keep going precisely then - a connector
/// that publishes nothing is what it is supposed to make visible.
/// </summary>
public sealed class LocalBrokerPublisher : IAsyncDisposable
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _clientId;
    private readonly ILogger _log;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private IMqttClient? _client;

    public LocalBrokerPublisher(string host, int port, string clientId, ILogger log)
    {
        _host = host;
        _port = port;
        _clientId = clientId;
        _log = log;
    }

    public async Task PublishRetainedAsync(string topic, string payload, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_client is null || !_client.IsConnected)
                await ConnectAsync(ct);

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .WithRetainFlag(true)
                .Build();

            await _client!.PublishAsync(message, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task ConnectAsync(CancellationToken ct)
    {
        _client?.Dispose();
        _client = new MqttFactory().CreateMqttClient();

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(_host, _port)
            .WithProtocolVersion(MqttProtocolVersion.V500)
            .WithClientId(_clientId)
            .WithCleanSession()
            .Build();

        await _client.ConnectAsync(options, ct);
        _log.LogInformation("Heartbeat connected to the local broker {Host}:{Port}.", _host, _port);
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is { IsConnected: true })
        {
            try { await _client.DisconnectAsync(); } catch { /* shutting down anyway */ }
        }
        _client?.Dispose();
        _gate.Dispose();
    }
}
