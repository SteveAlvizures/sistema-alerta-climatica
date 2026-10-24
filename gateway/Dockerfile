FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ClimateAlert.Gateway/ClimateAlert.Gateway.csproj src/ClimateAlert.Gateway/
RUN dotnet restore src/ClimateAlert.Gateway/ClimateAlert.Gateway.csproj
COPY src/ src/
RUN dotnet publish src/ClimateAlert.Gateway/ClimateAlert.Gateway.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "ClimateAlert.Gateway.dll"]
