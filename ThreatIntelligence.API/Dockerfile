FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["ThreatIntelligence.API/ThreatIntelligence.API.csproj", "ThreatIntelligence.API/"]
COPY ["ThreatIntelligence.Application/ThreatIntelligence.Application.csproj", "ThreatIntelligence.Application/"]
COPY ["ThreatIntelligence.Domain/ThreatIntelligence.Domain.csproj", "ThreatIntelligence.Domain/"]
COPY ["ThreatIntelligence.Infrastructure/ThreatIntelligence.Infrastructure.csproj", "ThreatIntelligence.Infrastructure/"]
RUN dotnet restore "ThreatIntelligence.API/ThreatIntelligence.API.csproj"
COPY . .
WORKDIR "/src/ThreatIntelligence.API"
RUN dotnet build "ThreatIntelligence.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ThreatIntelligence.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ThreatIntelligence.API.dll"]
