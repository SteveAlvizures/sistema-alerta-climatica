FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY src/ClimateAlert.Api/ClimateAlert.Api.csproj src/ClimateAlert.Api/
COPY src/ClimateAlert.Application/ClimateAlert.Application.csproj src/ClimateAlert.Application/
COPY src/ClimateAlert.Domain/ClimateAlert.Domain.csproj src/ClimateAlert.Domain/
COPY src/ClimateAlert.Infrastructure/ClimateAlert.Infrastructure.csproj src/ClimateAlert.Infrastructure/

RUN dotnet restore src/ClimateAlert.Api/ClimateAlert.Api.csproj

COPY . .

RUN dotnet publish src/ClimateAlert.Api/ClimateAlert.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "ClimateAlert.Api.dll"]
