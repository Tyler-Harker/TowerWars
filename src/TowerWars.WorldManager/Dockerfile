FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /build

# Copy project files
COPY ["src/TowerWars.Shared/TowerWars.Shared.csproj", "src/TowerWars.Shared/"]
COPY ["src/TowerWars.WorldManager/TowerWars.WorldManager.csproj", "src/TowerWars.WorldManager/"]

# Restore dependencies
RUN dotnet restore "src/TowerWars.WorldManager/TowerWars.WorldManager.csproj"

# Copy source code
COPY ["src/TowerWars.Shared/", "src/TowerWars.Shared/"]
COPY ["src/TowerWars.WorldManager/", "src/TowerWars.WorldManager/"]

# Build
RUN dotnet build "src/TowerWars.WorldManager/TowerWars.WorldManager.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "src/TowerWars.WorldManager/TowerWars.WorldManager.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 7002
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TowerWars.WorldManager.dll"]
