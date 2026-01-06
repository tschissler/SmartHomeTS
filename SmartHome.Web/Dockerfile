# Set a default project name, can be overridden with --build-arg PROJECT_NAME=<new-name> at build time
ARG PROJECT_NAME=SmartHome.Web

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
ARG VERSION=0.0.0
ARG BUILD_DATE=unknown
ARG GIT_COMMIT=unknown
ARG BUILD_NUMBER=0

# Set environment variables to speed up builds
ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE=true
ENV DOTNET_CLI_TELEMETRY_OPTOUT=true
ENV DOTNET_NOLOGO=true

WORKDIR /src

# Copy solution and project files first for better layer caching
COPY ["${PROJECT_NAME}/${PROJECT_NAME}.sln", "${PROJECT_NAME}/"]
COPY ["${PROJECT_NAME}/${PROJECT_NAME}/${PROJECT_NAME}.csproj", "${PROJECT_NAME}/${PROJECT_NAME}/"]
COPY ["${PROJECT_NAME}/UIComponentsLib/UIComponentsLib.csproj", "${PROJECT_NAME}/UIComponentsLib/"]
COPY ["${PROJECT_NAME}/SmartHomeWebManagers/SmartHomeWebManagers.csproj", "${PROJECT_NAME}/SmartHomeWebManagers/"]
COPY ["DataContracts/DataContracts.csproj", "DataContracts/"]
COPY ["SharedContracts/SharedContracts.csproj", "SharedContracts/"]
COPY ["SmartHomeHelpers/SmartHomeHelpers.csproj", "SmartHomeHelpers/"]

# Restore dependencies with caching - this layer will be cached if project files don't change
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    dotnet restore "${PROJECT_NAME}/${PROJECT_NAME}.sln" -a $TARGETARCH \
    --disable-parallel \
    --verbosity minimal

# Copy remaining source files
COPY . .

# Publish directly with version information and optimizations
WORKDIR "/src/${PROJECT_NAME}/${PROJECT_NAME}"
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    dotnet publish "${PROJECT_NAME}.csproj" \
    -c $BUILD_CONFIGURATION \
    -a $TARGETARCH \
    -o /app/publish \
    --no-restore \
    --verbosity minimal \
    -p:UseAppHost=false \
    -p:Version=${VERSION} \
    -p:InformationalVersion="${VERSION}+${GIT_COMMIT}" \
    -p:AssemblyVersion=${VERSION} \
    -p:FileVersion=${VERSION} \
    -p:DebugType=None \
    -p:DebugSymbols=false

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

# Install locale data
USER root
RUN apt-get update && \
    apt-get install -y --no-install-recommends locales && \
    sed -i -e 's/# de_DE.UTF-8 UTF-8/de_DE.UTF-8 UTF-8/' /etc/locale.gen && \
    locale-gen && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Set the locale
ENV LANG=de_DE.UTF-8
ENV LANGUAGE=de_DE:de
ENV LC_ALL=de_DE.UTF-8

# Set the time zone
ENV TZ=Europe/Berlin
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

USER app
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SmartHome.Web.dll"]
