# ──────────────────────────────────────────────
# Multi-stage Dockerfile for FundManager .NET services
# Build from repo root:
#   docker build -f Dockerfile --build-arg SERVICE_NAME=FundManager.Identity --build-arg SERVICE_PATH=src/services/FundManager.Identity -t fundmanager-identity .
# ──────────────────────────────────────────────

# ── Stage 1: Restore ──
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS restore
WORKDIR /source

# Copy solution and all .csproj files for restore caching
COPY FundManager.sln ./
COPY src/shared/FundManager.ServiceDefaults/FundManager.ServiceDefaults.csproj src/shared/FundManager.ServiceDefaults/
COPY src/shared/FundManager.BuildingBlocks/FundManager.BuildingBlocks.csproj src/shared/FundManager.BuildingBlocks/
COPY src/shared/FundManager.Contracts/FundManager.Contracts.csproj src/shared/FundManager.Contracts/
COPY src/services/FundManager.Identity/FundManager.Identity.csproj src/services/FundManager.Identity/
COPY src/services/FundManager.FundAdmin/FundManager.FundAdmin.csproj src/services/FundManager.FundAdmin/
COPY src/services/FundManager.Contributions/FundManager.Contributions.csproj src/services/FundManager.Contributions/
COPY src/services/FundManager.Loans/FundManager.Loans.csproj src/services/FundManager.Loans/
COPY src/services/FundManager.Dissolution/FundManager.Dissolution.csproj src/services/FundManager.Dissolution/
COPY src/services/FundManager.Notifications/FundManager.Notifications.csproj src/services/FundManager.Notifications/
COPY src/services/FundManager.Audit/FundManager.Audit.csproj src/services/FundManager.Audit/
COPY src/gateway/FundManager.ApiGateway/FundManager.ApiGateway.csproj src/gateway/FundManager.ApiGateway/

RUN dotnet restore

# ── Stage 2: Build & Publish ──
FROM restore AS publish
ARG SERVICE_PATH
WORKDIR /source

COPY src/ src/
RUN dotnet publish ${SERVICE_PATH} -c Release -o /app/publish --no-restore

# ── Stage 3: Runtime ──
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

# Create non-root user
RUN addgroup -S appgroup && adduser -S appuser -G appgroup

COPY --from=publish /app/publish .

# Use non-root user
USER appuser

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ARG SERVICE_NAME
ENV SERVICE_DLL=${SERVICE_NAME}.dll

# Shell form so $SERVICE_DLL gets expanded at runtime
CMD dotnet $SERVICE_DLL
