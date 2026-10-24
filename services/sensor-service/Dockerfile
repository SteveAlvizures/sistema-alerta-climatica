FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY sensor-service.csproj ./
RUN dotnet restore sensor-service.csproj

COPY . ./

RUN dotnet publish sensor-service.csproj \
    -c Release \
    -o /app/publish \
    --no-restore


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

ENV ASPNETCORE_URLS=http://+:3004
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 3004

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "sensor-service.dll"]
