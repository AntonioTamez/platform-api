# https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/docker/building-net-docker-images?view=aspnetcore-10.0
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Layer caching: restore csproj files first (D-07)
# Any change to .cs files will NOT bust this restore layer
COPY src/PersonsAPI.Domain/PersonsAPI.Domain.csproj             ./src/PersonsAPI.Domain/
COPY src/PersonsAPI.Application/PersonsAPI.Application.csproj   ./src/PersonsAPI.Application/
COPY src/PersonsAPI.Infrastructure/PersonsAPI.Infrastructure.csproj ./src/PersonsAPI.Infrastructure/
COPY src/PersonsAPI.Api/PersonsAPI.Api.csproj                   ./src/PersonsAPI.Api/
# Restore against the API project (not the .sln) so test project references in the solution
# are never evaluated — test .csproj files are excluded from the build context by .dockerignore
RUN dotnet restore src/PersonsAPI.Api/PersonsAPI.Api.csproj

# Copy src/ only — tests/ excluded per D-05
COPY src/ ./src/

# Publish in Release mode; --no-restore skips redundant restore after the cached layer above
RUN dotnet publish src/PersonsAPI.Api/PersonsAPI.Api.csproj \
    -c Release \
    --no-restore \
    -o /app/publish

# Final stage — ASP.NET Core runtime only (no SDK)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Install curl — aspnet:10.0 (Ubuntu Noble) does not include it by default
# Required for docker-compose healthcheck CMD (D-09) and Plan-02 healthcheck probe
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Port configuration — ASPNETCORE_HTTP_PORTS is the .NET 8+ canonical approach (D-01)
# Container listens on HTTP only; TLS is terminated upstream (Cloud Run / local proxy)
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PersonsAPI.Api.dll"]
