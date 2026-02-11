# See https://aka.ms/customizecontainer to learn how to customize your debug container.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["BoilerPlate.Diagnostics.WebApi/BoilerPlate.Diagnostics.WebApi.csproj", "BoilerPlate.Diagnostics.WebApi/"]
RUN dotnet restore "./BoilerPlate.Diagnostics.WebApi/BoilerPlate.Diagnostics.WebApi.csproj"
COPY . .
WORKDIR "/src/BoilerPlate.Diagnostics.WebApi"
RUN dotnet build "./BoilerPlate.Diagnostics.WebApi.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./BoilerPlate.Diagnostics.WebApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BoilerPlate.Diagnostics.WebApi.dll"]
