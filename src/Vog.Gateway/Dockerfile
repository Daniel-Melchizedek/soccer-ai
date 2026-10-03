# The gateway image. Built in Azure by infra/provision.ps1 (`az acr build`), with the repository root
# as the context so the shared build settings (global.json, Directory.*.props) are included.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/ src/
RUN dotnet publish src/Vog.Gateway/Vog.Gateway.csproj -c Release -o /app --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0
# The Speech SDK's native library loads ALSA and OpenSSL on Linux, even when it only synthesizes.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libasound2t64 ca-certificates \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Vog.Gateway.dll"]
