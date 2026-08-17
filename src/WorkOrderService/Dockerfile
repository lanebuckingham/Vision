# Vision — WorkOrderService
#
# Multi-stage build. Build context must be the repository root so this stage
# can see the centrally-managed Directory.Build.props / Directory.Packages.props
# (do not build with `docker build` from inside this directory).
#
#   docker build -f src/WorkOrderService/Dockerfile -t vision-work-order-service .

# ---- Build stage ---------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only what's needed to restore first, so dependency restore is cached
# independently of application source changes.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/WorkOrderService/Vision.WorkOrderService.csproj src/WorkOrderService/
RUN dotnet restore src/WorkOrderService/Vision.WorkOrderService.csproj

# Now bring in the rest of the service source and publish.
COPY src/WorkOrderService/ src/WorkOrderService/
RUN dotnet publish src/WorkOrderService/Vision.WorkOrderService.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ---- Runtime stage --------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl is not present in the base aspnet image. It's added here solely so
# Docker/Compose can run a lightweight HTTP health check against
# /health/ready without depending on a developer's host machine having any
# tool installed. This is a deliberate, small tradeoff documented per
# docs/development/docker.md — not a general-purpose toolchain addition.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# The aspnet base image already ships a non-root "app" user (UID 64198 on
# Debian-based images). Run as that user rather than root.
USER app

# Vision backends listen on a single predictable HTTP port inside the
# container; Compose/host maps this to the existing developer-facing port.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Vision.WorkOrderService.dll"]
