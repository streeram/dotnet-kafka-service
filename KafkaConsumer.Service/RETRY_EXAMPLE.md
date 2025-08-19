# Time-Based Retry Example

## Scenario: Processing a User Registration Event

### Original Message
```json
{
  "userId": "12345",
  "email": "user@example.com",
  "registrationDate": "2024-01-15T10:30:00Z",
  "source": "web"
}
```

### Processing Flow

#### 1. Initial Processing (Main Consumer)
- **Topic**: `user-events`
- **Message**: Original user registration JSON
- **Processing**: Fails due to third-party API timeout (500 error)

#### 2. First Retry (After ~2 seconds)
- **Topic**: `user-events-retry`
- **Message**: Same original JSON
- **Headers**:
  ```
  x-retry-attempt: 1
  x-process-after: 1705315802000  // Unix timestamp
  x-original-topic: user-events
  x-first-attempt-timestamp: 1705315800000
  x-last-error-type: ThirdPartyApiException
  x-last-error-message: Request timeout
  x-api-status-code: 500
  ```
- **Processing**: Fails again (503 error)

#### 3. Second Retry (After ~4 seconds)
- **Topic**: `user-events-retry`
- **Message**: Same original JSON
- **Headers**:
  ```
  x-retry-attempt: 2
  x-process-after: 1705315806000  // 4 seconds later
  x-original-topic: user-events
  x-first-attempt-timestamp: 1705315800000
  x-last-error-type: ThirdPartyApiException
  x-last-error-message: Service unavailable
  x-api-status-code: 503
  ```
- **Processing**: Fails again (500 error)

#### 4. Third Retry (After ~8 seconds)
- **Topic**: `user-events-retry`
- **Message**: Same original JSON
- **Headers**:
  ```
  x-retry-attempt: 3
  x-process-after: 1705315814000  // 8 seconds later
  x-original-topic: user-events
  x-first-attempt-timestamp: 1705315800000
  x-last-error-type: ThirdPartyApiException
  x-last-error-message: Internal server error
  x-api-status-code: 500
  ```
- **Processing**: Fails (max attempts reached)

#### 5. Dead Letter Queue
- **Topic**: `user-events-dlq`
- **Message**: Full retry context with original message
```json
{
  "originalMessage": "{\"userId\":\"12345\",\"email\":\"user@example.com\",\"registrationDate\":\"2024-01-15T10:30:00Z\",\"source\":\"web\"}",
  "originalTopic": "user-events",
  "originalKey": "12345",
  "retryAttempt": 3,
  "maxRetryAttempts": 3,
  "firstAttemptTimestamp": "2024-01-15T10:30:00Z",
  "lastAttemptTimestamp": "2024-01-15T10:30:14Z",
  "lastError": {
    "message": "Internal server error",
    "exceptionType": "ThirdPartyApiException",
    "httpStatusCode": 500,
    "apiResponse": "Service temporarily unavailable",
    "timestamp": "2024-01-15T10:30:14Z"
  },
  "metadata": {
    "isFinalFailure": "true",
    "processingHost": "consumer-pod-1",
    "consumerGroupId": "user-service-consumers"
  }
}
```

## Key Benefits Demonstrated

### 1. Schema Preservation
- **Retry topics** contain the exact same message format as original topics
- **No wrapper objects** in retry topics
- **Easy debugging** - can replay messages directly

### 2. Time-Based Processing
- **No memory usage** during delays
- **Persistent state** in Kafka headers
- **Restart resilient** - delays survive application restarts

### 3. Exponential Backoff
- **Attempt 1**: ~2 seconds (base delay)
- **Attempt 2**: ~4 seconds (2 * 1.5^1 with jitter)
- **Attempt 3**: ~8 seconds (2 * 1.5^2 with jitter)
- **Jitter prevents** thundering herd problems

### 4. Comprehensive Error Context
- **Full error history** preserved
- **Timing information** for analysis
- **API response details** for debugging
- **Processing metadata** for operations

## Monitoring Example

### Kafka Topics
```bash
# Check retry topic lag
kafka-consumer-groups --bootstrap-server localhost:9092 \
  --group user-service-consumers-retry --describe

# Monitor message flow
kafka-console-consumer --bootstrap-server localhost:9092 \
  --topic user-events-retry --property print.headers=true
```

### Application Logs
```
[INFO] Handling failed message from topic user-events, partition 0, offset 12345. Retry attempt: 0, API Status: 500
[INFO] Message sent to retry topic user-events-retry. Retry attempt: 1, Delay: 2150ms, Process after: 2024-01-15T10:30:02.150Z
[INFO] Processing retry message: Key=12345, Partition=0, Offset=67890
[WARN] Third-party API error processing retry message at user-events-retry-0@67890. Status: 503
[INFO] Message sent to retry topic user-events-retry. Retry attempt: 2, Delay: 3890ms, Process after: 2024-01-15T10:30:06.040Z
```

This example shows how the time-based retry mechanism maintains message fidelity while providing robust error handling and exponential backoff without blocking resources.