# Set a default project name, can be overridden with --build-arg PROJECT_NAME=<new-name> at build time
ARG PROJECT_NAME=ShellyConnector

# Base image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER app
WORKDIR /app
EXPOSE 8080

# Build image
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
# Install dependencies
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
    clang zlib1g-dev \
    && rm -rf /var/lib/apt/lists/*
ARG BUILD_CONFIGURATION=Release
ARG TARGETARCH
ARG PROJECT_NAME
ARG VERSION=0.0.0
ARG BUILD_DATE=unknown
ARG GIT_COMMIT=unknown
ARG BUILD_NUMBER=0
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
ARG BUILD_CONFIGURATION=Release
ARG PROJECT_NAME
ARG VERSION=0.0.0
ARG BUILD_DATE=unknown
ARG GIT_COMMIT=unknown
ARG BUILD_NUMBER=0
RUN dotnet publish "${PROJECT_NAME}.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false \
    /p:Version=${VERSION} \
    /p:InformationalVersion="${VERSION}+${GIT_COMMIT}" \
    /p:AssemblyVersion=${VERSION} \
    /p:FileVersion=${VERSION}

# Create version info file
RUN echo "{ \"version\": \"${VERSION}\", \"buildDate\": \"${BUILD_DATE}\", \"gitCommit\": \"${GIT_COMMIT}\", \"buildNumber\": \"${BUILD_NUMBER}\" }" > /app/publish/version.json

# Final image
FROM base AS final
ARG VERSION=0.0.0
ARG BUILD_DATE=unknown
ARG GIT_COMMIT=unknown

# Add version labels to the image
LABEL version="${VERSION}" \
      build-date="${BUILD_DATE}" \
      git-commit="${GIT_COMMIT}" \
      maintainer="tschissler"

WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ShellyConnector.dll"]
