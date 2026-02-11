FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /build

# Copy project files
COPY ["src/TowerWars.Shared/TowerWars.Shared.csproj", "src/TowerWars.Shared/"]
COPY ["src/TowerWars.Gateway/TowerWars.Gateway.csproj", "src/TowerWars.Gateway/"]

# Restore dependencies
RUN dotnet restore "src/TowerWars.Gateway/TowerWars.Gateway.csproj"

# Copy source code
COPY ["src/TowerWars.Shared/", "src/TowerWars.Shared/"]
COPY ["src/TowerWars.Gateway/", "src/TowerWars.Gateway/"]

# Build
RUN dotnet build "src/TowerWars.Gateway/TowerWars.Gateway.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "src/TowerWars.Gateway/TowerWars.Gateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 7000
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TowerWars.Gateway.dll"]
