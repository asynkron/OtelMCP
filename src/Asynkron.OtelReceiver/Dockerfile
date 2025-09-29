FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 4317

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Asynkron.OtelReceiver.Core ./Asynkron.OtelReceiver.Core
COPY Asynkron.OtelReceiver ./Asynkron.OtelReceiver
WORKDIR /src/Asynkron.OtelReceiver
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Asynkron.OtelReceiver.dll"]
