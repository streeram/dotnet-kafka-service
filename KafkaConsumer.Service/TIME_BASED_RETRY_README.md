# Time-Based Exponential Backoff Retry Pattern for Kafka

## Overview

This implementation provides a scalable, production-ready exponential backoff retry pattern for Kafka message processing without using `Task.Delay` or in-memory delays. The solution uses **time-based message scheduling with Kafka headers** to achieve delayed processing.

## Key Features

- ✅ **No in-memory delays** - Uses Kafka's native message timestamp and header capabilities
- ✅ **Same schema for retry topics** - Retry topics maintain the same schema as original topics
- ✅ **Exponential backoff with jitter** - Prevents thundering herd problems
- ✅ **Configurable retry limits** - Maximum attempts, min/max delays
- ✅ **Separate retry consumer** - Dedicated consumer for processing delayed messages
- ✅ **Production-ready** - Proper error handling, logging, and monitoring
- ✅ **Scalable** - Can handle high-throughput scenarios

## How It Works

### 1. Message Failure Handling

When a message fails processing:

1. **ErrorHandlingService** determines if the message should be retried
2. Calculates the next retry delay using exponential backoff: `baseDelay * (multiplier ^ (attempt - 1))`
3. Adds jitter (±10% randomization) to prevent thundering herd
4. Stores retry metadata in Kafka message headers:
   - `x-retry-attempt`: Current retry attempt number
   - `x-process-after`: Unix timestamp when message should be processed
   - `x-original-topic`: Original topic name
   - `x-first-attempt-timestamp`: When the first attempt was made
   - `x-last-error-type`: Type of the last exception
   - `x-last-error-message`: Last error message
   - `x-api-status-code`: HTTP status code (if applicable)
   - `x-api-response`: API response (if applicable)

### 2. Time-Based Processing

The **RetryConsumerService**:

1. Subscribes to all retry topics (`{original-topic}-retry`)
2. Continuously polls for messages
3. For each message, checks the `x-process-after` header
4. Only processes messages where `current_time >= process_after_time`
5. Messages not ready for processing are left uncommitted and will be reprocessed

### 3. Exponential Backoff Calculation

```csharp
// Base calculation
var exponentialDelay = baseDelayMs * Math.Pow(backoffMultiplier, retryAttempt - 1);

// Add jitter (±10%)
var jitterFactor = 1.0 + (Random.Shared.NextDouble() - 0.5) * 0.2;
var delayWithJitter = exponentialDelay * jitterFactor;

// Apply bounds
var finalDelay = Math.Min(delayWithJitter, maxRetryDelayMs);
finalDelay = Math.Max(finalDelay, minRetryDelayMs);
```

## Configuration

### appsettings.json

```json
{
  "Kafka": {
    "RetryConfiguration": {
      "MaxRetryAttempts": 3,
      "RetryTopicSuffix": "-retry",
      "DeadLetterTopicSuffix": "-dlq",
      "RetryDelayMs": 5000,
      "UseExponentialBackoff": true,
      "BackoffMultiplier": 2.0,
      "MaxRetryDelayMs": 300000,
      "MinRetryDelayMs": 1000,
      "RetryConsumerPollingIntervalMs": 1000
    }
  }
}
```

### Configuration Properties

| Property | Description | Default |
|----------|-------------|---------|
| `MaxRetryAttempts` | Maximum number of retry attempts | 3 |
| `RetryDelayMs` | Base delay in milliseconds | 5000 |
| `UseExponentialBackoff` | Enable exponential backoff | true |
| `BackoffMultiplier` | Multiplier for exponential backoff | 2.0 |
| `MaxRetryDelayMs` | Maximum delay cap | 300000 (5 min) |
| `MinRetryDelayMs` | Minimum delay floor | 1000 (1 sec) |
| `RetryConsumerPollingIntervalMs` | Retry consumer polling interval | 1000 |

## Example Retry Timeline

With default settings (base: 5s, multiplier: 2.0):

| Attempt | Delay Calculation | Actual Delay (with jitter) | Total Time |
|---------|-------------------|----------------------------|------------|
| 1 | 5s * 2^0 = 5s | ~4.5-5.5s | ~5s |
| 2 | 5s * 2^1 = 10s | ~9-11s | ~15s |
| 3 | 5s * 2^2 = 20s | ~18-22s | ~37s |
| 4 → DLQ | - | - | - |

## Architecture Components

### 1. ErrorHandlingService (Updated)
- Handles message failures
- Calculates retry delays
- Sends messages to retry topics with headers
- Maintains original message schema

### 2. RetryConsumerService (New)
- Dedicated consumer for retry topics
- Time-based message processing
- Handles retry message failures

### 3. RetryWorker (New)
- Background service running RetryConsumerService
- Lifecycle management

### 4. ConsumerKafkaProducerService (Updated)
- Added support for custom headers
- Maintains backward compatibility

## Topic Structure

```
Original Topic: events-topic
├── Schema: YourMessageSchema
├── Processing: Main consumer (Worker)
└── Failures → Retry Topic

Retry Topic: events-topic-retry
├── Schema: YourMessageSchema (same as original)
├── Headers: Retry metadata
├── Processing: Retry consumer (RetryWorker)
└── Failures → Next retry or DLQ

Dead Letter Queue: events-topic-dlq
├── Schema: RetryMessage (with full context)
├── Processing: Manual intervention
└── Contains: Final failure information
```

## Benefits Over Task.Delay Approach

### ❌ Task.Delay Problems
- **Memory consumption**: Holds messages in memory during delays
- **Resource waste**: Threads blocked waiting
- **Scalability issues**: Limited by available memory/threads
- **Restart problems**: In-flight delays lost on restart
- **No persistence**: Retry state not durable

### ✅ Time-Based Header Approach
- **Zero memory overhead**: No in-memory message storage
- **Resource efficient**: No blocked threads
- **Highly scalable**: Limited only by Kafka throughput
- **Restart resilient**: Retry state persisted in Kafka
- **Durable**: Survives application restarts
- **Observable**: Retry state visible in Kafka tools

## Monitoring and Observability

### Metrics to Monitor
- Retry topic lag
- Retry processing rate
- Messages in retry vs DLQ
- Average retry delay accuracy
- Consumer group health

### Logging
The implementation provides comprehensive logging:
- Retry attempt information
- Delay calculations
- Processing timestamps
- Error details
- Consumer lifecycle events

## Production Considerations

### 1. Topic Configuration
```bash
# Create retry topics with appropriate retention
kafka-topics --create --topic events-topic-retry \
  --partitions 3 \
  --replication-factor 3 \
  --config retention.ms=604800000  # 7 days
```

### 2. Consumer Group Management
- Main consumer: `consumer-group-1`
- Retry consumer: `consumer-group-1-retry`
- Separate consumer groups prevent interference

### 3. Scaling
- Scale retry consumers independently
- Monitor retry topic lag
- Adjust polling intervals based on load

### 4. Error Handling
- Retry consumer failures also go through retry logic
- Ultimate failures go to DLQ
- Manual intervention processes for DLQ

## Migration from Task.Delay

If you have existing Task.Delay-based retry logic:

1. **Deploy new version** with both mechanisms
2. **Monitor** retry topic processing
3. **Gradually migrate** traffic to new pattern
4. **Remove** old Task.Delay code
5. **Clean up** any temporary configurations

## Testing

### Unit Tests
- Test delay calculations
- Test header parsing
- Test time-based processing logic

### Integration Tests
- End-to-end retry scenarios
- Consumer restart scenarios
- High-load scenarios

This implementation provides a robust, scalable solution for handling Kafka message retries with exponential backoff while maintaining the same schema across original and retry topics.