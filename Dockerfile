# Container image for hosting (used by Render). Build from the repository root:
#   docker build -t eventmanagement .
# The connection string comes from the ConnectionStrings__DefaultConnection
# environment variable, and the port from PORT.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, so this layer is cached until the package list changes.
COPY EventManagement/EventManagement.csproj EventManagement/
RUN dotnet restore EventManagement/EventManagement.csproj

COPY EventManagement/ EventManagement/
RUN dotnet publish EventManagement/EventManagement.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Time zone data, so event times show in Central time rather than UTC.
RUN apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends tzdata \
    && rm -rf /var/lib/apt/lists/*

# FORWARDEDHEADERS: Render serves HTTPS and forwards plain HTTP to the app;
#   this makes the app trust Render's X-Forwarded-Proto/For headers.
# gcServer=0: the workstation garbage collector uses far less memory, which
#   matters on a 512 MB free instance.
# TZ: the app shows times in the server's local zone, as on the team's machines.
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    DOTNET_gcServer=0 \
    TZ=America/Chicago \
    PORT=10000

COPY --from=build /app/publish .

USER $APP_UID
EXPOSE 10000
ENTRYPOINT ["dotnet", "EventManagement.dll"]
