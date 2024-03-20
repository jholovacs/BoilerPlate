#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["BoilerPlate.Authentication.WebApi/BoilerPlate.Authentication.WebApi.csproj", "BoilerPlate.Authentication.WebApi/"]
RUN dotnet restore "./BoilerPlate.Authentication.WebApi/BoilerPlate.Authentication.WebApi.csproj"
COPY . .
WORKDIR "/src/BoilerPlate.Authentication.WebApi"
RUN dotnet build "./BoilerPlate.Authentication.WebApi.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./BoilerPlate.Authentication.WebApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BoilerPlate.Authentication.WebApi.dll"]