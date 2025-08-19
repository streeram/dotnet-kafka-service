using Kafka.Common.Models;
using KafkaConsumer.Service;
using KafkaConsumer.Service.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<KafkaSettings>(
    builder.Configuration.GetSection("Kafka"));

// Register HttpClient for third-party API service
builder.Services.AddHttpClient<IThirdPartyApiService, ThirdPartyApiService>();

// Register core services
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<RetryWorker>();
builder.Services.AddScoped<IMessageProcessor, MessageProcessor>();

// Register error handling and producer services
builder.Services.AddScoped<IErrorHandlingService, ErrorHandlingService>();
builder.Services.AddSingleton<IConsumerKafkaProducerService, ConsumerKafkaProducerService>();

// Register retry consumer service
builder.Services.AddSingleton<IRetryConsumerService, RetryConsumerService>();

// Register third-party API service
builder.Services.AddScoped<IThirdPartyApiService, ThirdPartyApiService>();

var host = builder.Build();
host.Run();