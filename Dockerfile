# syntax=docker/dockerfile:1

# ===== BUILD STAGE =====
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH

WORKDIR /source

# Copy solution and project files
COPY Mewdeko.sln ./
COPY src/Mewdeko/Mewdeko.csproj ./src/Mewdeko/
COPY src/Mewdeko.Votes/Mewdeko.Votes.csproj ./src/Mewdeko.Votes/

# Restore dependencies (with proper architecture)
RUN dotnet restore -a $TARGETARCH

# Copy the rest of the source code
COPY src/ ./src/

# Build and publish
WORKDIR /source/src/Mewdeko
RUN dotnet publish \
    -c Release \
    -a $TARGETARCH \
    --no-restore \
    -o /app/publish \
    /p:UseAppHost=false

# ===== RUNTIME STAGE =====
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# Install curl for healthchecks
RUN apt-get update && \
    apt-get install -y --no-install-recommends curl && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Copy published application
COPY --from=build /app/publish .

# Copy data directory if it exists (bot strings, images, etc.)
COPY --from=build /source/src/Mewdeko/data ./data

# Create directories for runtime data
RUN mkdir -p /app/data /app/logs

# Set environment variables
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    DOTNET_EnableDiagnostics=0 \
    ASPNETCORE_URLS=http://+:5001

# Expose API port
EXPOSE 5001

# Health check endpoint (assumes API is enabled)
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:5001/health || exit 1

# Run the application
ENTRYPOINT ["dotnet", "Mewdeko.dll"]
