# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first so package layers are cached when only source changes.
COPY src/FleetPulse.Api/FleetPulse.Api.csproj src/FleetPulse.Api/
RUN dotnet restore src/FleetPulse.Api/FleetPulse.Api.csproj

COPY src/FleetPulse.Api/ src/FleetPulse.Api/
RUN dotnet publish src/FleetPulse.Api/FleetPulse.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Render (and most hosts) inject PORT; fall back to 8080 for a plain docker run.
ENV PORT=8080
EXPOSE 8080

ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} exec dotnet FleetPulse.Api.dll"]
