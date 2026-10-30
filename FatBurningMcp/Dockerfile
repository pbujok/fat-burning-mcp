FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["FatBurner.Abstractions/FatBurner.Abstractions.csproj", "FatBurner.Abstractions/"]
COPY ["FatBurner.Strava/FatBurner.Strava.csproj", "FatBurner.Strava/"]
COPY ["FatBurner.Mcp/FatBurner.Mcp.csproj", "FatBurner.Mcp/"]
RUN dotnet restore "FatBurner.Mcp/FatBurner.Mcp.csproj"

COPY . .
WORKDIR /src/FatBurner.Mcp
RUN dotnet publish "FatBurner.Mcp.csproj" --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
RUN mkdir -p /data && chown "$APP_UID:$APP_UID" /data
USER $APP_UID

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "FatBurner.Mcp.dll"]