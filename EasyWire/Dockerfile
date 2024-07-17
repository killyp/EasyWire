FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_ID
WORKDIR /app

RUN apt-get update && apt-get install -y \
    iproute2 \
    wireguard \
    iptables \
    && rm -rf /var/lib/apt/lists/*
RUN mkdir /app/keys && chown $APP_UID:$APP_UID /app/keys

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["EasyWire/EasyWire.csproj", "EasyWire/"]
RUN dotnet restore "EasyWire/EasyWire.csproj"
COPY . .
WORKDIR "/src/EasyWire"
RUN dotnet build "EasyWire.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "EasyWire.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EasyWire.dll"]
