# Construction multi-etapes : SDK pour compiler, ASP.NET pour executer
# Multi-stage build: SDK to compile, ASP.NET runtime to run
# (BUILD_FROM fourni par HA est volontairement ignore / BUILD_FROM from HA is intentionally ignored)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Garderie.Web/Garderie.Web.csproj src/Garderie.Web/
RUN dotnet restore src/Garderie.Web/Garderie.Web.csproj
COPY src/ src/
RUN dotnet publish src/Garderie.Web/Garderie.Web.csproj -c Release -o /app --no-restore -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
# Fuseaux horaires (America/Toronto) et police du PDF / Time zones and PDF font
RUN apt-get update \
    && apt-get install -y --no-install-recommends tzdata fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*
ENV TZ=America/Toronto \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    ASPNETCORE_ENVIRONMENT=Production \
    Garderie__DataDir=/data
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080 8090
ENTRYPOINT ["dotnet", "Garderie.Web.dll"]
