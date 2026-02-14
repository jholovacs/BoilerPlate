#See https://aka.ms/customizecontainer to learn how to customize your debug container

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["BoilerPlate.EventLogs.Abstractions/BoilerPlate.EventLogs.Abstractions.csproj", "BoilerPlate.EventLogs.Abstractions/"]
COPY ["BoilerPlate.Services.EventLogs/BoilerPlate.Services.EventLogs.csproj", "BoilerPlate.Services.EventLogs/"]
RUN dotnet restore "./BoilerPlate.Services.EventLogs/BoilerPlate.Services.EventLogs.csproj"
COPY . .
WORKDIR "/src/BoilerPlate.Services.EventLogs"
RUN dotnet build "./BoilerPlate.Services.EventLogs.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./BoilerPlate.Services.EventLogs.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BoilerPlate.Services.EventLogs.dll"]
