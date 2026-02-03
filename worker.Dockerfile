FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-env
WORKDIR /src

RUN mkdir -p /root/.nuget/NuGet
COPY ./config/NuGetPackageSource.Config /root/.nuget/NuGet/NuGet.Config

COPY ./src/Worker/*.csproj ./worker/
RUN dotnet restore ./worker/

COPY ./src .
RUN dotnet publish ./worker/ -o /publish --configuration Release

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

RUN groupadd -g 10001 appgroup \
    && useradd -u 10001 -g appgroup -s /usr/sbin/nologin -m appuser

RUN apt-get update && apt-get install -y gss-ntlmssp \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build-env --chown=appuser:appgroup /publish .

ARG git_branch
ENV ASPNETCORE_ENVIRONMENT=$git_branch

USER appuser

ENTRYPOINT ["dotnet", "Worker.dll"]