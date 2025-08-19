using KafkaConsumer.Service.Services;

namespace KafkaConsumer.Service;

/// <summary>
/// Background service that runs the retry consumer for processing delayed retry messages.
/// </summary>
public class RetryWorker : BackgroundService
{
    private readonly IRetryConsumerService _retryConsumerService;
    private readonly ILogger<RetryWorker> _logger;

    public RetryWorker(
        IRetryConsumerService retryConsumerService,
        ILogger<RetryWorker> logger)
    {
        _retryConsumerService = retryConsumerService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting retry worker");

        try
        {
            await _retryConsumerService.StartAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Retry worker cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in retry worker");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping retry worker");

        await _retryConsumerService.StopAsync(cancellationToken);
        await base.StopAsync(cancellationToken);

        _logger.LogInformation("Retry worker stopped");
    }
}