FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY AssistantApi.Core/AssistantApi.Core.csproj AssistantApi.Core/
COPY AssistantApi.Infrastructure/AssistantApi.Infrastructure.csproj AssistantApi.Infrastructure/
COPY IngestionService/IngestionService.csproj IngestionService/

RUN dotnet restore IngestionService/IngestionService.csproj

COPY . .
RUN dotnet publish IngestionService/IngestionService.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "IngestionService.dll"]
