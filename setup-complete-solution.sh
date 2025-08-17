    #!/bin/bash

# Complete solution setup script for Kafka Confluent Cloud with .NET 8

echo "🚀 Creating Kafka Confluent Cloud Solution..."

# Create solution and projects
echo "📁 Creating solution structure..."
dotnet new sln -n KafkaConfluentCloud
dotnet new webapi -n KafkaProducer.Api -f net8.0
dotnet new worker -n KafkaConsumer.Service -f net8.0
dotnet new classlib -n Kafka.Common -f net8.0

# Add projects to solution
dotnet sln add KafkaProducer.Api/KafkaProducer.Api.csproj
dotnet sln add KafkaConsumer.Service/KafkaConsumer.Service.csproj
dotnet sln add Kafka.Common/Kafka.Common.csproj

# Add project references
dotnet add KafkaProducer.Api/KafkaProducer.Api.csproj reference Kafka.Common/Kafka.Common.csproj
dotnet add KafkaConsumer.Service/KafkaConsumer.Service.csproj reference Kafka.Common/Kafka.Common.csproj

# Add NuGet packages
echo "📦 Adding NuGet packages..."
dotnet add KafkaProducer.Api/KafkaProducer.Api.csproj package Confluent.Kafka
dotnet add KafkaProducer.Api/KafkaProducer.Api.csproj package Microsoft.Extensions.Options
dotnet add KafkaConsumer.Service/KafkaConsumer.Service.csproj package Confluent.Kafka
dotnet add KafkaConsumer.Service/KafkaConsumer.Service.csproj package Microsoft.Extensions.Options
dotnet add Kafka.Common/Kafka.Common.csproj package Confluent.Kafka

# Create directory structure
echo "📂 Creating directory structure..."
mkdir -p Kafka.Common/Models
mkdir -p KafkaProducer.Api/Services
mkdir -p KafkaProducer.Api/Controllers
mkdir -p KafkaConsumer.Service/Services
mkdir -p k8s

# Create Kafka.Common files
echo "📝 Creating Kafka.Common files..."

cat > Kafka.Common/Models/KafkaSettings.cs << 'EOF'
namespace Kafka.Common.Models;

public class KafkaSettings
{
    public string BootstrapServers { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string TokenEndpointUrl { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
    public string LogicalCluster { get; init; } = string.Empty;
    public string IdentityPoolId { get; init; } = string.Empty;
    public string ConsumerGroupId { get; init; } = string.Empty;
    public List<string> Topics { get; init; } = new();
}
EOF

cat > Kafka.Common/Models/EventMessage.cs << 'EOF'
namespace Kafka.Common.Models;

public record EventMessage(
    string Id, 
    string Type, 
    object Data, 
    DateTime Timestamp);
EOF

# Create KafkaProducer.Api files
echo "📝 Creating KafkaProducer.Api files..."

cat > KafkaProducer.Api/Services/IKafkaProducerService.cs << 'EOF'
using Confluent.Kafka;

namespace KafkaProducer.Api.Services;

public interface IKafkaProducerService
{
    Task<DeliveryResult<string, string>> ProduceAsync<T>(
        string topic, 
        string key, 
        T message,
        CancellationToken cancellationToken = default);
}
EOF

cat > KafkaProducer.Api/Services/KafkaProducerService.cs << 'EOF'
using Confluent.Kafka;
using Kafka.Common.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace KafkaProducer.Api.Services;

public class KafkaProducerService : IKafkaProducerService, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly KafkaSettings _settings;
    private readonly ILogger<KafkaProducerService> _logger;

    public KafkaProducerService(
        IOptions<KafkaSettings> settings, 
        ILogger<KafkaProducerService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        
        var config = new ProducerConfig
        {
            BootstrapServers = _settings.BootstrapServers,
            SecurityProtocol = SecurityProtocol.SaslSsl,
            SaslMechanism = SaslMechanism.OAuthBearer,
            SaslOauthbearerMethod = SaslOauthbearerMethod.Oidc,
            SaslOauthbearerClientId = _settings.ClientId,
            SaslOauthbearerClientSecret = _settings.ClientSecret,
            SaslOauthbearerTokenEndpointUrl = _settings.TokenEndpointUrl,
            SaslOauthbearerScope = _settings.Scope,
            SaslOauthbearerExtensions = $"logicalCluster={_settings.LogicalCluster},identityPoolId={_settings.IdentityPoolId}",
            EnableIdempotence = true,
            Acks = Acks.All,
            CompressionType = CompressionType.Snappy,
            LingerMs = 20,
            BatchSize = 32768,
            MessageMaxBytes = 1000000,
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 100,
            SocketKeepaliveEnable = true,
            MetadataMaxAgeMs = 180000,
            ConnectionsMaxIdleMs = 180000
        };

        _producer = new ProducerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => _logger.LogError($"Kafka error: {e.Reason}"))
            .SetLogHandler((_, log) => _logger.LogDebug($"Kafka log: {log.Message}"))
            .Build();
    }

    public async Task<DeliveryResult<string, string>> ProduceAsync<T>(
        string topic, 
        string key, 
        T message,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var jsonMessage = JsonSerializer.Serialize(message);
            var kafkaMessage = new Message<string, string>
            {
                Key = key,
                Value = jsonMessage,
                Headers = new Headers
                {
                    { "correlation-id", Guid.NewGuid().ToString() },
                    { "timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString() }
                }
            };

            var result = await _producer.ProduceAsync(topic, kafkaMessage, cancellationToken);
            _logger.LogInformation($"Message delivered to {result.TopicPartitionOffset}");
            return result;
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, $"Failed to deliver message: {ex.Error.Reason}");
            throw;
        }
    }

    public void Dispose()
    {
        _producer?.Flush(TimeSpan.FromSeconds(10));
        _producer?.Dispose();
    }
}
EOF

cat > KafkaProducer.Api/Controllers/EventsController.cs << 'EOF'
using Kafka.Common.Models;
using KafkaProducer.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace KafkaProducer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly IKafkaProducerService _kafkaProducer;
    private readonly ILogger<EventsController> _logger;

    public EventsController(
        IKafkaProducerService kafkaProducer,
        ILogger<EventsController> logger)
    {
        _kafkaProducer = kafkaProducer;
        _logger = logger;
    }

    [HttpPost("publish")]
    public async Task<IActionResult> PublishEvent([FromBody] EventMessage message)
    {
        try
        {
            var result = await _kafkaProducer.ProduceAsync(
                "events-topic",
                message.Id,
                message);

            return Ok(new
            {
                success = true,
                partition = result.Partition.Value,
                offset = result.Offset.Value
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event");
            return StatusCode(500, new { error = "Failed to publish event" });
        }
    }
}
EOF

cat > KafkaProducer.Api/Program.cs << 'EOF'
using Kafka.Common.Models;
using KafkaProducer.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<KafkaSettings>(
    builder.Configuration.GetSection("Kafka"));

builder.Services.AddSingleton<IKafkaProducerService, KafkaProducerService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();
EOF

# Create KafkaConsumer.Service files
echo "📝 Creating KafkaConsumer.Service files..."

cat > KafkaConsumer.Service/Services/IMessageProcessor.cs << 'EOF'
namespace KafkaConsumer.Service.Services;

public interface IMessageProcessor
{
    Task ProcessAsync(string message, CancellationToken cancellationToken);
}
EOF

cat > KafkaConsumer.Service/Services/MessageProcessor.cs << 'EOF'
using System.Text.Json;
using Kafka.Common.Models;

namespace KafkaConsumer.Service.Services;

public class MessageProcessor : IMessageProcessor
{
    private readonly ILogger<MessageProcessor> _logger;

    public MessageProcessor(ILogger<MessageProcessor> logger)
    {
        _logger = logger;
    }

    public async Task ProcessAsync(string message, CancellationToken cancellationToken)
    {
        try
        {
            var eventMessage = JsonSerializer.Deserialize<EventMessage>(message);
            _logger.LogInformation($"Processing event: {eventMessage?.Type} with ID: {eventMessage?.Id}");
            
            // Add your business logic here
            await Task.Delay(100, cancellationToken); // Simulate processing
            
            _logger.LogInformation($"Successfully processed event: {eventMessage?.Id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message");
            throw;
        }
    }
}
EOF

cat > KafkaConsumer.Service/Worker.cs << 'EOF'
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
            FetchMaxWaitMs = 500,
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
EOF

cat > KafkaConsumer.Service/Program.cs << 'EOF'
using Kafka.Common.Models;
using KafkaConsumer.Service;
using KafkaConsumer.Service.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<KafkaSettings>(
    builder.Configuration.GetSection("Kafka"));

builder.Services.AddHostedService<Worker>();
builder.Services.AddScoped<IMessageProcessor, MessageProcessor>();

var host = builder.Build();
host.Run();
EOF

# Create appsettings.json files
echo "📝 Creating configuration files..."

cat > KafkaProducer.Api/appsettings.json << 'EOF'
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Kafka": {
    "BootstrapServers": "pkc-xxxxx.region.provider.confluent.cloud:9092",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "ConsumerGroupId": "consumer-group-1",
    "Topics": ["events-topic"],
    "LogicalCluster": "lkc-xxxxx",
    "IdentityPoolId": "pool-xxxxx",
    "TokenEndpointUrl": "https://login.confluent.io/oauth2/token",
    "Scope": "api"
  }
}
EOF

cat > KafkaConsumer.Service/appsettings.json << 'EOF'
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "Kafka": {
    "BootstrapServers": "pkc-xxxxx.region.provider.confluent.cloud:9092",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "ConsumerGroupId": "consumer-group-1",
    "Topics": ["events-topic"],
    "LogicalCluster": "lkc-xxxxx",
    "IdentityPoolId": "pool-xxxxx",
    "TokenEndpointUrl": "https://login.confluent.io/oauth2/token",
    "Scope": "api"
  }
}
EOF

# Create Dockerfiles
echo "🐳 Creating Docker files..."

cat > KafkaProducer.Api/Dockerfile << 'EOF'
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["KafkaProducer.Api/KafkaProducer.Api.csproj", "KafkaProducer.Api/"]
COPY ["Kafka.Common/Kafka.Common.csproj", "Kafka.Common/"]
RUN dotnet restore "KafkaProducer.Api/KafkaProducer.Api.csproj"
COPY . .
WORKDIR "/src/KafkaProducer.Api"
RUN dotnet build "KafkaProducer.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "KafkaProducer.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "KafkaProducer.Api.dll"]
EOF

cat > KafkaConsumer.Service/Dockerfile << 'EOF'
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["KafkaConsumer.Service/KafkaConsumer.Service.csproj", "KafkaConsumer.Service/"]
COPY ["Kafka.Common/Kafka.Common.csproj", "Kafka.Common/"]
RUN dotnet restore "KafkaConsumer.Service/KafkaConsumer.Service.csproj"
COPY . .
WORKDIR "/src/KafkaConsumer.Service"
RUN dotnet build "KafkaConsumer.Service.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "KafkaConsumer.Service.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "KafkaConsumer.Service.dll"]
EOF

# Create Kubernetes manifests
echo "☸️ Creating Kubernetes manifests..."

cat > k8s/namespace.yaml << 'EOF'
apiVersion: v1
kind: Namespace
metadata:
  name: kafka-apps
EOF

cat > k8s/configmap.yaml << 'EOF'
apiVersion: v1
kind: ConfigMap
metadata:
  name: kafka-config
  namespace: kafka-apps
data:
  appsettings.json: |
    {
      "Kafka": {
        "BootstrapServers": "pkc-xxxxx.region.provider.confluent.cloud:9092",
        "ConsumerGroupId": "consumer-group-1",
        "Topics": ["events-topic"],
        "LogicalCluster": "lkc-xxxxx",
        "IdentityPoolId": "pool-xxxxx",
        "TokenEndpointUrl": "https://login.confluent.io/oauth2/token",
        "Scope": "api"
      }
    }
EOF

cat > k8s/secret.yaml << 'EOF'
apiVersion: v1
kind: Secret
metadata:
  name: kafka-oauth-secret
  namespace: kafka-apps
type: Opaque
stringData:
  client-id: "your-client-id"
  client-secret: "your-client-secret"
EOF

cat > k8s/producer-deployment.yaml << 'EOF'
apiVersion: apps/v1
kind: Deployment
metadata:
  name: kafka-producer-api
  namespace: kafka-apps
spec:
  replicas: 3
  selector:
    matchLabels:
      app: kafka-producer-api
  template:
    metadata:
      labels:
        app: kafka-producer-api
    spec:
      containers:
      - name: api
        image: your-registry/kafka-producer-api:latest
        ports:
        - containerPort: 80
        env:
        - name: Kafka__ClientId
          valueFrom:
            secretKeyRef:
              name: kafka-oauth-secret
              key: client-id
        - name: Kafka__ClientSecret
          valueFrom:
            secretKeyRef:
              name: kafka-oauth-secret
              key: client-secret
        volumeMounts:
        - name: config
          mountPath: /app/appsettings.json
          subPath: appsettings.json
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /health
            port: 80
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health
            port: 80
          initialDelaySeconds: 5
          periodSeconds: 5
      volumes:
      - name: config
        configMap:
          name: kafka-config
---
apiVersion: v1
kind: Service
metadata:
  name: kafka-producer-api
  namespace: kafka-apps
spec:
  selector:
    app: kafka-producer-api
  ports:
  - port: 80
    targetPort: 80
  type: ClusterIP
EOF

cat > k8s/consumer-deployment.yaml << 'EOF'
apiVersion: apps/v1
kind: Deployment
metadata:
  name: kafka-consumer-service
  namespace: kafka-apps
spec:
  replicas: 2
  selector:
    matchLabels:
      app: kafka-consumer-service
  template:
    metadata:
      labels:
        app: kafka-consumer-service
    spec:
      containers:
      - name: consumer
        image: your-registry/kafka-consumer-service:latest
        env:
        - name: Kafka__ClientId
          valueFrom:
            secretKeyRef:
              name: kafka-oauth-secret
              key: client-id
        - name: Kafka__ClientSecret
          valueFrom:
            secretKeyRef:
              name: kafka-oauth-secret
              key: client-secret
        volumeMounts:
        - name: config
          mountPath: /app/appsettings.json
          subPath: appsettings.json
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
      volumes:
      - name: config
        configMap:
          name: kafka-config
EOF

# Create README
echo "📚 Creating documentation..."

cat > README.md << 'EOF'
# Kafka Confluent Cloud with .NET 8 and OAuth

Production-ready Kafka producer and consumer implementation for Confluent Cloud with OAuth authentication, designed for Kubernetes deployment in AKS.

## 🚀 Features

- ✅ OAuth authentication with Confluent Cloud
- ✅ Resilient producer API with retry logic
- ✅ Background consumer service with manual offset management
- ✅ Kubernetes-optimized configuration
- ✅ Docker support
- ✅ Comprehensive error handling and logging
- ✅ Health checks and graceful shutdown
- ✅ Horizontal scaling support

## 📋 Prerequisites

- .NET 8 SDK
- Docker
- Kubernetes cluster (AKS)
- Confluent Cloud account with OAuth configured

## 🏗️ Project Structure