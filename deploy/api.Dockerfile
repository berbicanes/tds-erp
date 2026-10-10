# Build context: repository root (see docker-compose.yml)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props ./
COPY src/Tds.Api/Tds.Api.csproj src/Tds.Api/
RUN dotnet restore src/Tds.Api/Tds.Api.csproj
COPY src/Tds.Api/ src/Tds.Api/
RUN dotnet publish src/Tds.Api/Tds.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    FileStorage__Root=/data/files \
    DataProtection__KeysPath=/data/keys
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Tds.Api.dll"]
