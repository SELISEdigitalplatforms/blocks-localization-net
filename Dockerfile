FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-env
WORKDIR /src
ARG git_branch

RUN mkdir -p /root/.nuget/NuGet
COPY ./config/NuGetPackageSource.Config /root/.nuget/NuGet/NuGet.Config

COPY ./src/Api/*.csproj ./Api/
RUN dotnet restore ./Api/

COPY ./src .
ENV ASPNETCORE_ENVIRONMENT=$git_branch

RUN dotnet publish ./Api/ -o /publish --configuration Release

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

RUN groupadd -g 10001 appgroup \
    && useradd -u 10001 -g appgroup -s /usr/sbin/nologin -m appuser

COPY --from=build-env --chown=appuser:appgroup /publish .

ENV port=8080
ARG git_branch

ENV ASPNETCORE_ENVIRONMENT=$git_branch
ENV ASPNETCORE_URLS=http://+:$port

USER appuser

ENTRYPOINT ["dotnet", "Api.dll"]