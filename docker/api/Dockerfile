# The API image, built from the repository root:
#   docker build -f docker/api/Dockerfile .               production image
#   docker build -f docker/api/Dockerfile --target dev .  dotnet watch
# .dockerignore lists what the build may read.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS source

WORKDIR /workspace

# The API build compiles the graph engine.
RUN apt-get update \
  && apt-get install -y --no-install-recommends g++ \
  && rm -rf /var/lib/apt/lists/*

# Restored before the sources are copied, so a code change reuses this layer.
COPY Directory.Build.props Directory.Build.targets ./
COPY src/api/Coordina.Api.csproj src/api/
COPY tools/analyzers/Coordina.Analyzers/Coordina.Analyzers.csproj tools/analyzers/Coordina.Analyzers/

RUN dotnet restore src/api/Coordina.Api.csproj -m:1

COPY . .

ENV ASPNETCORE_URLS=http://+:5050
EXPOSE 5050

FROM source AS dev

CMD ["dotnet", "watch", "--project", "src/api/Coordina.Api.csproj", "--urls", "http://+:5050"]

FROM source AS publish

RUN dotnet publish src/api/Coordina.Api.csproj \
  --configuration Release \
  --no-restore \
  --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:11.0 AS runtime

WORKDIR /app

COPY --from=publish /app/publish .

ENV ASPNETCORE_URLS=http://+:5050
EXPOSE 5050

USER $APP_UID

ENTRYPOINT ["dotnet", "Coordina.Api.dll"]
