FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["CryptoManager.API/CryptoManager.API.csproj", "CryptoManager.API/"]
COPY ["CryptoManager.Application/CryptoManager.Application.csproj", "CryptoManager.Application/"]
COPY ["CryptoManager.Domain/CryptoManager.Domain.csproj", "CryptoManager.Domain/"]
COPY ["CryptoManager.Infrastructure/CryptoManager.Infrastructure.csproj", "CryptoManager.Infrastructure/"]
RUN dotnet restore "CryptoManager.API/CryptoManager.API.csproj"
COPY . .
RUN dotnet publish "CryptoManager.API/CryptoManager.API.csproj" -c Release -o /app/publish /p:UseAppHost=false       

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CryptoManager.API.dll"]