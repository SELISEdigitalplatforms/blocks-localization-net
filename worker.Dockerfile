# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-env
WORKDIR /src

RUN mkdir -p /root/.nuget/NuGet
COPY ./config/NuGetPackageSource.Config /root/.nuget/NuGet/NuGet.Config
COPY ./src .

# Copy the .csproj file and restore
COPY ./src/Worker/*.csproj ./worker/
RUN dotnet restore ./worker/

# Copy everything else and build
COPY ./src/Worker ./worker/
RUN dotnet build ./worker/

# Publish the application
RUN dotnet publish ./worker/ -o /publish --configuration Release
RUN ls /publish

# Publish Stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build-env /publish .
ARG git_branch

RUN apt-get update && apt-get install -y gss-ntlmssp \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_ENVIRONMENT=$git_branch

RUN groupadd -g 10001 appgroup \
    && useradd -u 10001 -g appgroup -s /usr/sbin/nologin -m appuser \
    && chown -R appuser:appgroup /app

USER appuser

ENTRYPOINT ["dotnet", "Worker.dll"]
