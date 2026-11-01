# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /source

COPY Directory.Build.props global.json ./
COPY src/CodeLogic.Domain/CodeLogic.Domain.csproj src/CodeLogic.Domain/packages.lock.json ./src/CodeLogic.Domain/
COPY src/CodeLogic.Application/CodeLogic.Application.csproj src/CodeLogic.Application/packages.lock.json ./src/CodeLogic.Application/
COPY src/CodeLogic.Infrastructure/CodeLogic.Infrastructure.csproj src/CodeLogic.Infrastructure/packages.lock.json ./src/CodeLogic.Infrastructure/
COPY src/CodeLogic.Web/CodeLogic.Web.csproj src/CodeLogic.Web/packages.lock.json ./src/CodeLogic.Web/
RUN dotnet restore src/CodeLogic.Web/CodeLogic.Web.csproj --locked-mode

COPY src ./src
RUN dotnet publish src/CodeLogic.Web/CodeLogic.Web.csproj \
    --configuration $BUILD_CONFIGURATION \
    --no-restore \
    --output /out \
    /p:UseAppHost=false \
    && mkdir -p /container/data /container/keys

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra@sha256:9b755ea799ae649141a073e30d8b62d8095a5465af2b7694d17866e8c05131d8
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ConnectionStrings__CodeLogic="Data Source=/data/CodeLogic.db" \
    DataProtection__KeysPath=/keys

COPY --from=build --chown=app:app /out ./
COPY --from=build --chown=app:app /container/data /data
COPY --from=build --chown=app:app /container/keys /keys

EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "CodeLogic.Web.dll"]
