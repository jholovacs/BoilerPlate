# Dockerfile for the LDAP server host.
# Builds a container that runs the LDAP server authenticating against BoilerPlate services.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY . .
RUN dotnet restore "./BoilerPlate.Authentication.LdapServer.Host/BoilerPlate.Authentication.LdapServer.Host.csproj"
WORKDIR "/src/BoilerPlate.Authentication.LdapServer.Host"
RUN dotnet build "./BoilerPlate.Authentication.LdapServer.Host.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./BoilerPlate.Authentication.LdapServer.Host.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BoilerPlate.Authentication.LdapServer.Host.dll"]
