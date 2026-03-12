FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/backend/CryptoManager.API/CryptoManager.API.csproj", "src/backend/CryptoManager.API/"]
COPY ["src/backend/CryptoManager.Application/CryptoManager.Application.csproj", "src/backend/CryptoManager.Application/"]
COPY ["src/backend/CryptoManager.Domain/CryptoManager.Domain.csproj", "src/backend/CryptoManager.Domain/"]
COPY ["src/backend/CryptoManager.Infrastructure/CryptoManager.Infrastructure.csproj", "src/backend/CryptoManager.Infrastructure/"]

RUN dotnet restore "src/backend/CryptoManager.API/CryptoManager.API.csproj"

COPY . .

RUN dotnet publish "src/backend/CryptoManager.API/CryptoManager.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

RUN apt-get update && \
    apt-get install -y libusb-1.0-0 pcscd

COPY packages/yubihsm /tmp/yubihsm

COPY packages/yubihsm-runtime/yubihsm_pkcs11.conf /etc/yubihsm_pkcs11.conf

RUN apt-get update && \
    apt-get install -y \
    /tmp/yubihsm/*.deb && \
    rm -rf /tmp/yubihsm

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CryptoManager.API.dll"]