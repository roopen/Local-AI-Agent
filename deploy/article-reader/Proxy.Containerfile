FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY LocalAIAgent.Application/News/PublicNetworkHttpHandler.cs LocalAIAgent.Application/News/
COPY deploy/article-reader/proxy/ deploy/article-reader/proxy/
RUN dotnet publish deploy/article-reader/proxy/ArticleEgressProxy.csproj -c Release -o /out
FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /out/ ./
USER 1654:1654
ENTRYPOINT ["dotnet", "ArticleEgressProxy.dll"]
