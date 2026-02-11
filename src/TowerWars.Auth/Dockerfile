FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /build

# Copy project files
COPY ["src/TowerWars.Shared/TowerWars.Shared.csproj", "src/TowerWars.Shared/"]
COPY ["src/TowerWars.Auth/TowerWars.Auth.csproj", "src/TowerWars.Auth/"]

# Restore dependencies
RUN dotnet restore "src/TowerWars.Auth/TowerWars.Auth.csproj"

# Copy source code
COPY ["src/TowerWars.Shared/", "src/TowerWars.Shared/"]
COPY ["src/TowerWars.Auth/", "src/TowerWars.Auth/"]

# Build
RUN dotnet build "src/TowerWars.Auth/TowerWars.Auth.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "src/TowerWars.Auth/TowerWars.Auth.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 7001
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TowerWars.Auth.dll"]
