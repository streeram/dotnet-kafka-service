# Kafka Consumer Error Handling with Third-Party API Integration

This document describes the enhanced error handling functionality in the Kafka Consumer Service that integrates with third-party APIs and implements retry/dead letter queue (DLQ) patterns.

## Overview

The consumer now includes sophisticated error handling that:
1. Calls a third-party API with the message payload
2. Routes messages to retry or DLQ topics based on API response status codes
3. Supports configurable retry policies and topic naming conventions
4. Preserves message metadata and error information throughout the retry process

## Architecture

### Key Components

1. **MessageProcessor**: Enhanced to call third-party APIs and handle both regular and retry messages
2. **ThirdPartyApiService**: Handles HTTP calls to external APIs with proper error handling
3. **ErrorHandlingService**: Determines message routing based on error types and API responses
4. **ConsumerKafkaProducerService**: Publishes messages to retry and DLQ topics
5. **RetryMessage Model**: Encapsulates retry metadata and error information

### Message Flow

```
Original Message → MessageProcessor → Third-Party API
                                   ↓
                              Success? → Continue Processing
                                   ↓ No
                              ErrorHandlingService
                                   ↓
                         Should Retry? → Retry Topic (-retry)
                                   ↓ No
                              Dead Letter Queue (-dlq)
```

## Configuration

### Retry Configuration

```json
{
  "Kafka": {
    "RetryConfiguration": {
      "MaxRetryAttempts": 3,
      "RetryTopicSuffix": "-retry",
      "DeadLetterTopicSuffix": "-dlq",
      "RetryDelayMs": 5000,
      "UseExponentialBackoff": true,
      "BackoffMultiplier": 2.0
    }
  }
}
```

### Third-Party API Configuration

```json
{
  "Kafka": {
    "ThirdPartyApiConfiguration": {
      "BaseUrl": "https://api.example.com",
      "ApiKey": "your-api-key",
      "TimeoutMs": 30000,
      "RetryableStatusCodes": [500, 502, 503, 504, 408, 429],
      "DeadLetterStatusCodes": [400, 401, 403, 404, 422]
    }
  }
}
```

## Topic Naming Convention

- **Original Topic**: `events-topic`
- **Retry Topic**: `events-topic-retry`
- **Dead Letter Queue**: `events-topic-dlq`

## Error Handling Logic

### Retry Conditions

Messages are retried when:
- API returns status codes in `RetryableStatusCodes` (default: 500, 502, 503, 504, 408, 429)
- Transient exceptions occur (TimeoutException, HttpRequestException, etc.)
- Current retry attempt is less than `MaxRetryAttempts`

### DLQ Conditions

Messages are sent to DLQ when:
- API returns status codes in `DeadLetterStatusCodes` (default: 400, 401, 403, 404, 422)
- Maximum retry attempts exceeded
- Non-transient exceptions occur
- Unable to publish to retry topic

## Retry Message Structure

```json
{
  "originalMessage": "original message content",
  "originalTopic": "events-topic",
  "originalKey": "message-key",
  "retryAttempt": 2,
  "maxRetryAttempts": 3,
  "firstAttemptTimestamp": "2024-01-01T10:00:00Z",
  "lastAttemptTimestamp": "2024-01-01T10:05:00Z",
  "lastError": {
    "message": "API returned 503",
    "exceptionType": "ThirdPartyApiException",
    "httpStatusCode": 503,
    "apiResponse": "Service temporarily unavailable",
    "timestamp": "2024-01-01T10:05:00Z"
  },
  "metadata": {
    "isFinalFailure": "false",
    "processingHost": "consumer-host-1",
    "consumerGroupId": "consumer-group-1"
  }
}
```

## Monitoring and Observability

### Log Levels

- **Information**: Successful processing, retry attempts, topic routing
- **Warning**: API failures, retry exhaustion
- **Error**: Unexpected exceptions, producer failures
- **Critical**: DLQ publishing failures (potential message loss)

### Key Metrics to Monitor

1. **Message Processing Rate**: Messages processed per second
2. **API Success Rate**: Percentage of successful third-party API calls
3. **Retry Rate**: Messages sent to retry topics
4. **DLQ Rate**: Messages sent to dead letter queues
5. **API Response Times**: Third-party API latency
6. **Error Rates by Status Code**: Breakdown of API failures

## Deployment Considerations

### Topic Creation

Ensure retry and DLQ topics are created before deployment:
```bash
# For topic: events-topic
kafka-topics --create --topic events-topic-retry --partitions 3 --replication-factor 3
kafka-topics --create --topic events-topic-dlq --partitions 3 --replication-factor 3
```

### Environment Variables

For production deployment, override sensitive configuration:
```bash
export Kafka__ClientSecret="production-secret"
export Kafka__ThirdPartyApiConfiguration__ApiKey="production-api-key"
export Kafka__ThirdPartyApiConfiguration__BaseUrl="https://api.production.example.com"
```

## Testing

### Unit Tests

Test scenarios should cover:
- Successful API calls
- Various HTTP status codes
- Retry logic and exhaustion
- Message serialization/deserialization
- Topic name generation

### Integration Tests

- End-to-end message flow
- Actual API integration
- Kafka topic publishing
- Error handling scenarios

## Troubleshooting

### Common Issues

1. **Messages not retrying**: Check `RetryableStatusCodes` configuration
2. **Messages going to DLQ immediately**: Verify status code mappings
3. **API timeouts**: Adjust `TimeoutMs` setting
4. **Topic not found errors**: Ensure retry/DLQ topics exist
5. **High memory usage**: Monitor retry message accumulation

### Debug Logging

Enable debug logging for detailed troubleshooting:
```json
{
  "Logging": {
    "LogLevel": {
      "KafkaConsumer.Service": "Debug"
    }
  }
}
```

## Security Considerations

- Store API keys securely (Azure Key Vault, AWS Secrets Manager, etc.)
- Use HTTPS for all third-party API calls
- Implement proper authentication and authorization
- Monitor for sensitive data in retry/DLQ messages
- Consider message encryption for sensitive payloads