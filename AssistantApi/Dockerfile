FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY AssistantApi.Core/AssistantApi.Core.csproj AssistantApi.Core/
COPY AssistantApi.Application/AssistantApi.Application.csproj AssistantApi.Application/
COPY AssistantApi.Infrastructure/AssistantApi.Infrastructure.csproj AssistantApi.Infrastructure/
COPY AssistantApi/AssistantApi.csproj AssistantApi/

RUN dotnet restore AssistantApi/AssistantApi.csproj

COPY . .
RUN dotnet publish AssistantApi/AssistantApi.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AssistantApi.dll"]
