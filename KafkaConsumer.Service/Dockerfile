FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
WORKDIR /app

FROM base AS final
WORKDIR /app
COPY artifacts/publish/KafkaConsumer.Service/ .
ENTRYPOINT ["dotnet", "KafkaConsumer.Service.dll"]