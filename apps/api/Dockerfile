# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS project-files
WORKDIR /metadata
COPY apps/api/Source/ Source/
RUN find Source -type f \
      ! -name '*.csproj' \
      ! -name '*.props' \
      ! -name '*.targets' \
      -delete

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY apps/api/GameGuild.sln apps/api/Directory.Build.props apps/api/Directory.Packages.props apps/api/global.json ./
COPY --from=project-files /metadata/Source/ Source/
RUN --mount=type=cache,id=nuget-api,target=/root/.nuget/packages \
    dotnet restore "Source/GameGuild.API/GameGuild.API.csproj" --disable-parallel

COPY apps/api/Source/ Source/
RUN --mount=type=cache,id=nuget-api,target=/root/.nuget/packages \
    dotnet publish "Source/GameGuild.API/GameGuild.API.csproj" \
      -m:1 \
      -o /app/publish \
      -c Release \
      /p:UseAppHost=false \
      --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
ARG SOURCE_SHA=unknown
ARG SOURCE_TREE=unknown
ARG RELEASE_SHA=unknown
ARG VERSION=unknown
ARG BUILD_TIMESTAMP=unknown

LABEL org.opencontainers.image.source="https://github.com/gameguild-gg/gameguild" \
      org.opencontainers.image.revision="${SOURCE_SHA}" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.created="${BUILD_TIMESTAMP}" \
      io.gameguild.source-tree="${SOURCE_TREE}"

WORKDIR /app
RUN apk add --no-cache curl krb5-libs icu-libs icu-data-full openssl3 libssl3 tzdata \
    && addgroup -g 1001 -S appuser \
    && adduser -S appuser -G appuser -u 1001 \
    && chown -R appuser:appuser /app

COPY --from=build --chown=appuser:appuser /app/publish .
USER appuser
EXPOSE 8080

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    GAMEGUILD_VERSION=${VERSION} \
    RELEASE_SHA=${RELEASE_SHA} \
    SOURCE_TREE=${SOURCE_TREE} \
    BUILT_AT=${BUILD_TIMESTAMP} \
    IMAGE_DIGEST=Unknown \
    DEPLOYED_AT=Unknown

HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://127.0.0.1:8080/live || exit 1

ENTRYPOINT ["dotnet", "GameGuild.API.dll"]
