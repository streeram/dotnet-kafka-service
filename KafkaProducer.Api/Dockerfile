FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM base AS final
WORKDIR /app
COPY artifacts/publish/KafkaProducer.Api/ .
ENTRYPOINT ["dotnet", "KafkaProducer.Api.dll"]