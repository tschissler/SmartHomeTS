# Set a default project name, can be overridden with --build-arg PROJECT_NAME=<new-name> at build time
ARG PROJECT_NAME=SmartHome.DataHub

# Base image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER app
WORKDIR /app
EXPOSE 8080

# Build image
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
# Install dependencies
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
    clang zlib1g-dev
ARG BUILD_CONFIGURATION=Release
ARG TARGETARCH
ARG PROJECT_NAME
WORKDIR /src
# COPY ["NuGet.config", "."]
# COPY ["${PROJECT_NAME}/${PROJECT_NAME}/${PROJECT_NAME}.csproj", "${PROJECT_NAME}/"]
COPY . .
RUN dotnet restore "/src/${PROJECT_NAME}/${PROJECT_NAME}.sln"
WORKDIR "/src/${PROJECT_NAME}/${PROJECT_NAME}"
RUN dotnet build "${PROJECT_NAME}.csproj" -c $BUILD_CONFIGURATION -o /app/build
#We need to set environment variables before being able to run tests in the docker container
#RUN dotnet test "../${PROJECT_NAME}.sln" -c $BUILD_CONFIGURATION
# Publish image
FROM build AS publish
RUN dotnet publish "${PROJECT_NAME}.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=true

# Final image
FROM build AS final
WORKDIR /app
EXPOSE 8080
COPY --from=publish /app/publish .
ENTRYPOINT ["./SmartHome.DataHub"]
