FROM node:24-bookworm-slim AS web-build
WORKDIR /src/LocalAIAgent.WebUI

COPY LocalAIAgent.WebUI/package.json LocalAIAgent.WebUI/package-lock.json ./
RUN npm ci

COPY LocalAIAgent.WebUI/ ./
COPY LocalAIAgent.API/openapi/LocalAIAgent.API.json /src/LocalAIAgent.API/openapi/LocalAIAgent.API.json
RUN npm run generate:api && npm run build -- --mode production

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /src
COPY . ./
RUN dotnet restore LocalAIAgent.API/LocalAIAgent.API.csproj
RUN dotnet publish LocalAIAgent.API/LocalAIAgent.API.csproj \
    --configuration Release \
    --output /out \
    --no-restore \
    --property:SkipClientBuild=true \
    --property:SelfContained=false \
    --property:PublishSingleFile=false \
    --property:RuntimeIdentifier=

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl ca-certificates \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir --parents /app/wwwroot /data \
    && chown --recursive 1654:1654 /app /data

WORKDIR /app
COPY --from=api-build --chown=1654:1654 /out/ ./
COPY --from=web-build --chown=1654:1654 /src/LocalAIAgent.WebUI/dist/ ./wwwroot/

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://0.0.0.0:8080 \
    DOTNET_EnableDiagnostics=0 \
    SQLITE_DATASOURCE=/data/ainews.db \
    DATA_PROTECTION_KEYS_PATH=/data/keys

VOLUME ["/data"]
EXPOSE 8080
USER 1654:1654
ENTRYPOINT ["dotnet", "LocalAIAgent.API.dll"]
