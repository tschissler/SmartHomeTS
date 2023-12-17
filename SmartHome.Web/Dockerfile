# Set a default project name, can be overridden with --build-arg PROJECT_NAME=<new-name> at build time
ARG PROJECT_NAME=SmartHome.Web

# Base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080

# Build image
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
# Install dependencies
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
    clang zlib1g-dev
ARG BUILD_CONFIGURATION=Release
ARG TARGETARCH
ARG PROJECT_NAME
WORKDIR /src
# COPY ["NuGet.config", "."]
COPY ["${PROJECT_NAME}/${PROJECT_NAME}/${PROJECT_NAME}.csproj", "${PROJECT_NAME}/"]
RUN dotnet restore "${PROJECT_NAME}/${PROJECT_NAME}.csproj"
COPY . .
WORKDIR "/src/${PROJECT_NAME}/${PROJECT_NAME}"
RUN dotnet --version && echo $(dotnet --version)
RUN dotnet build "${PROJECT_NAME}.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish image
FROM build AS publish
RUN dotnet publish "${PROJECT_NAME}.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=true

# Final image
FROM build AS final
WORKDIR /app
EXPOSE 8080
COPY --from=publish /app/publish .
ENTRYPOINT ["./SmartHome.Web"]
