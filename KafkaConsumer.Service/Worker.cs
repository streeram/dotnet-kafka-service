using Confluent.Kafka;
using Kafka.Common.Models;
using KafkaConsumer.Service.Services;
using Microsoft.Extensions.Options;

namespace KafkaConsumer.Service;

public class Worker : BackgroundService
{
    private readonly KafkaSettings _settings;
    private readonly ILogger<Worker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private IConsumer<string, string>? _consumer;

    public Worker(
        IOptions<KafkaSettings> settings,
        ILogger<Worker> logger,
        IServiceProvider serviceProvider)
    {
        _settings = settings.Value;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
    }

    private async Task StartConsumerLoop(CancellationToken cancellationToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _settings.BootstrapServers,
            GroupId = _settings.ConsumerGroupId,
            SecurityProtocol = SecurityProtocol.SaslSsl,
            SaslMechanism = SaslMechanism.OAuthBearer,
            SaslOauthbearerMethod = SaslOauthbearerMethod.Oidc,
            SaslOauthbearerClientId = _settings.ClientId,
            SaslOauthbearerClientSecret = _settings.ClientSecret,
            SaslOauthbearerTokenEndpointUrl = _settings.TokenEndpointUrl,
            SaslOauthbearerScope = _settings.Scope,
            SaslOauthbearerExtensions = $"logicalCluster={_settings.LogicalCluster},identityPoolId={_settings.IdentityPoolId}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false,
            FetchMinBytes = 1024,
            FetchWaitMaxMs = 500,
            MaxPartitionFetchBytes = 1048576,
            SessionTimeoutMs = 45000,
            HeartbeatIntervalMs = 3000,
            SocketKeepaliveEnable = true,
            ReconnectBackoffMs = 50,
            ReconnectBackoffMaxMs = 10000
        };

        _consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => _logger.LogError($"Consumer error: {e.Reason}"))
            .SetPartitionsAssignedHandler((c, partitions) =>
            {
                _logger.LogInformation($"Partitions assigned: [{string.Join(", ", partitions)}]");
            })
            .SetPartitionsRevokedHandler((c, partitions) =>
            {
                _logger.LogInformation($"Partitions revoked: [{string.Join(", ", partitions)}]");
            })
            .Build();

        _consumer.Subscribe(_settings.Topics);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = _consumer.Consume(cancellationToken);
                    
                    if (consumeResult?.Message != null)
                    {
                        await ProcessMessage(consumeResult, cancellationToken);
                        _consumer.Commit(consumeResult);
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, $"Consume error: {ex.Error.Reason}");
                    if (ex.Error.IsFatal) break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Consumer loop cancelled");
        }
        finally
        {
            _consumer?.Close();
            _consumer?.Dispose();
        }
    }

    private async Task ProcessMessage(
        ConsumeResult<string, string> consumeResult,
        CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        
        try
        {
            _logger.LogInformation(
                $"Processing message: Key={consumeResult.Message.Key}, " +
                $"Partition={consumeResult.Partition}, Offset={consumeResult.Offset}");

            var messageProcessor = scope.ServiceProvider
                .GetRequiredService<IMessageProcessor>();
            
            await messageProcessor.ProcessAsync(
                consumeResult.Message.Value, 
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                $"Error processing message at {consumeResult.TopicPartitionOffset}");
            await HandleFailedMessage(consumeResult, ex);
        }
    }

    private async Task HandleFailedMessage(
        ConsumeResult<string, string> consumeResult,
        Exception exception)
    {
        _logger.LogError($"Moving message to DLQ: {consumeResult.TopicPartitionOffset}");
        await Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Kafka consumer");
        await base.StopAsync(cancellationToken);
    }
}
