FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /build

# Copy project files
COPY ["src/TowerWars.Shared/TowerWars.Shared.csproj", "src/TowerWars.Shared/"]
COPY ["src/TowerWars.Persistence/TowerWars.Persistence.csproj", "src/TowerWars.Persistence/"]

# Restore dependencies
RUN dotnet restore "src/TowerWars.Persistence/TowerWars.Persistence.csproj"

# Copy source code
COPY ["src/TowerWars.Shared/", "src/TowerWars.Shared/"]
COPY ["src/TowerWars.Persistence/", "src/TowerWars.Persistence/"]

# Build
RUN dotnet build "src/TowerWars.Persistence/TowerWars.Persistence.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "src/TowerWars.Persistence/TowerWars.Persistence.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TowerWars.Persistence.dll"]
