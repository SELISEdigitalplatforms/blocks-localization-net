# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-env
WORKDIR /src
ARG git_branch

RUN mkdir -p /root/.nuget/NuGet
COPY ./config/NuGetPackageSource.Config /root/.nuget/NuGet/NuGet.Config
COPY ./src .

ENV ASPNETCORE_ENVIRONMENT=$git_branch

#   Copy only .csproj and restore
COPY ./src/Api/*.csproj ./Api/
RUN dotnet restore ./Api/

#   Copy everything else and build
COPY ./src/Api ./Api/
RUN dotnet build ./Api/

#   Publish
RUN dotnet publish ./Api/ -o /publish --configuration Release
RUN ls /publish

# Publish Stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0
ARG git_branch

WORKDIR /app
COPY --from=build-env /publish .
ENV port=8080
ARG git_branch

ENV ASPNETCORE_ENVIRONMENT=$git_branch
ENV ASPNETCORE_URLS=http://+:$port

RUN groupadd -g 10001 appgroup \
    && useradd -u 10001 -g appgroup -s /usr/sbin/nologin -m appuser \
    && chown -R appuser:appgroup /app

USER appuser

ENTRYPOINT ["dotnet", "Api.dll"]
