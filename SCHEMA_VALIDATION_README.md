# Schema Validation for Kafka Producer

This document explains how to use the schema validation functionality that has been added to the Kafka Producer API.

## Overview

The schema validation feature validates messages against schemas stored in Confluent Cloud Schema Registry before publishing them to Kafka topics. This ensures data quality and prevents invalid messages from being published.

## Configuration

### appsettings.json

Add the following Schema Registry configuration to your `appsettings.json`:

```json
{
  "Kafka": {
    "BootstrapServers": "pkc-xxxxx.region.provider.confluent.cloud:9092",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "SchemaRegistryUrl": "https://psrc-xxxxx.region.provider.confluent.cloud",
    "SchemaRegistryApiKey": "your-schema-registry-api-key",
    "SchemaRegistryApiSecret": "your-schema-registry-api-secret",
    "EnableSchemaValidation": true
  }
}
```

### Configuration Properties

- `SchemaRegistryUrl`: The URL of your Confluent Cloud Schema Registry
- `SchemaRegistryApiKey`: API key for Schema Registry authentication
- `SchemaRegistryApiSecret`: API secret for Schema Registry authentication
- `EnableSchemaValidation`: Boolean flag to enable/disable schema validation (default: true)

## Usage

### Publishing Messages with Schema Validation

#### Endpoint: `POST /api/events/publish-with-validation`

This endpoint validates messages against the schema before publishing to Kafka.

**Request:**
```json
{
  "id": "event-123",
  "type": "UserCreated",
  "data": {
    "userId": "user-456",
    "email": "user@example.com",
    "name": "John Doe"
  },
  "timestamp": "2024-01-15T10:30:00Z"
}
```

**Query Parameters:**
- `schemaSubject` (optional): Override the default schema subject. If not provided, defaults to `{topic}-value`

**Response (Success):**
```json
{
  "success": true,
  "partition": 0,
  "offset": 12345,
  "schemaValidated": true,
  "schemaSubject": "events-topic-value"
}
```

**Response (Schema Validation Error):**
```json
{
  "error": "Schema validation failed",
  "subject": "events-topic-value",
  "details": "Schema validation failed: /data/email: Required property 'email' not found"
}
```

### Publishing Messages without Schema Validation

#### Endpoint: `POST /api/events/publish`

This endpoint publishes messages without schema validation (legacy behavior).

### Schema Management Endpoints

#### Get Latest Schema
`GET /api/schema/{subject}/latest`

Retrieves the latest schema for a given subject.

**Response:**
```json
{
  "id": 123,
  "subject": "events-topic-value",
  "version": 1,
  "schemaType": "JSON",
  "schema": "{\"type\":\"object\",\"properties\":{...}}"
}
```

#### Validate Message Against Schema
`POST /api/schema/{subject}/validate`

Validates a message against a specific schema subject without publishing.

**Request Body:** Any JSON object to validate

**Response (Valid):**
```json
{
  "valid": true,
  "subject": "events-topic-value",
  "message": "Schema validation passed"
}
```

**Response (Invalid):**
```json
{
  "valid": false,
  "subject": "events-topic-value",
  "error": "Schema validation failed: /email: Required property 'email' not found"
}
```

## Schema Subject Naming Convention

By default, the schema subject follows the pattern: `{topic}-value`

For example:
- Topic: `events-topic` → Schema Subject: `events-topic-value`
- Topic: `user-events` → Schema Subject: `user-events-value`

You can override this by providing the `schemaSubject` query parameter.

## Error Handling

The API returns different HTTP status codes based on the type of error:

- `400 Bad Request`: Schema validation failed
- `503 Service Unavailable`: Schema Registry is unavailable
- `500 Internal Server Error`: Unexpected errors

## Performance Considerations

- **Schema Caching**: Schemas are cached in memory to improve performance
- **Async Operations**: All schema operations are asynchronous
- **Connection Pooling**: Schema Registry client uses connection pooling

## Troubleshooting

### Common Issues

1. **Schema Registry Connection Issues**
   - Verify the `SchemaRegistryUrl` is correct
   - Check API key and secret credentials
   - Ensure network connectivity to Schema Registry

2. **Schema Not Found**
   - Verify the schema subject exists in Schema Registry
   - Check the subject naming convention
   - Ensure the schema is registered for the topic

3. **Validation Failures**
   - Review the validation error details in the response
   - Verify the message structure matches the schema
   - Check for required fields and data types

### Logging

Enable debug logging to see detailed schema validation information:

```json
{
  "Logging": {
    "LogLevel": {
      "KafkaProducer.Api.Services.SchemaValidationService": "Debug"
    }
  }
}
```

## Example Schema

Here's an example JSON schema for the EventMessage:

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "properties": {
    "id": {
      "type": "string",
      "description": "Unique identifier for the event"
    },
    "type": {
      "type": "string",
      "description": "Type of the event"
    },
    "data": {
      "type": "object",
      "description": "Event payload data"
    },
    "timestamp": {
      "type": "string",
      "format": "date-time",
      "description": "Event timestamp"
    }
  },
  "required": ["id", "type", "data", "timestamp"],
  "additionalProperties": false
}
```

## Best Practices

1. **Always use schema validation** for production environments
2. **Version your schemas** properly in Schema Registry
3. **Test schema changes** before deploying to production
4. **Monitor validation failures** and fix data quality issues
5. **Use meaningful schema subjects** that follow your naming conventions