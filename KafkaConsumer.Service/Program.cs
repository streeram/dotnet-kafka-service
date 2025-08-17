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
