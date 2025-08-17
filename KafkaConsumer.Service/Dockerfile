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
