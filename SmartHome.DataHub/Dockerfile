# Set a default project name, can be overridden with --build-arg PROJECT_NAME=<new-name> at build time
ARG PROJECT_NAME=SmartHome.DataHub

# Base image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER app
WORKDIR /app
EXPOSE 8080

# Build image
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG TARGETARCH
ARG PROJECT_NAME
WORKDIR /src

# Copy solution and project files first for better layer caching
COPY ["${PROJECT_NAME}/${PROJECT_NAME}.sln", "${PROJECT_NAME}/"]
COPY ["${PROJECT_NAME}/${PROJECT_NAME}/${PROJECT_NAME}.csproj", "${PROJECT_NAME}/${PROJECT_NAME}/"]
COPY ["${PROJECT_NAME}/Influx3Connector/Influx3Connector.csproj", "${PROJECT_NAME}/Influx3Connector/"]
COPY ["MQTTClient/MQTTClient.csproj", "MQTTClient/"]
COPY ["SharedContracts/SharedContracts.csproj", "SharedContracts/"]
COPY ["SmartHomeHelpers/SmartHomeHelpers.csproj", "SmartHomeHelpers/"]

# Restore dependencies - this layer will be cached if project files don't change
RUN dotnet restore "${PROJECT_NAME}/${PROJECT_NAME}.sln" -a $TARGETARCH

# Copy remaining source files
COPY . .

# Publish directly (skip separate build step)
WORKDIR "/src/${PROJECT_NAME}/${PROJECT_NAME}"
RUN dotnet publish "${PROJECT_NAME}.csproj" \
    -c $BUILD_CONFIGURATION \
    -a $TARGETARCH \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# Final image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SmartHome.DataHub.dll"]
