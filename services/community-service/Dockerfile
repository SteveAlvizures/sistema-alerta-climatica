FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY CommunityService.csproj ./
RUN dotnet restore CommunityService.csproj

COPY . ./

RUN dotnet publish CommunityService.csproj \
    -c Release \
    -o /app/publish \
    --no-restore


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

ENV ASPNETCORE_URLS=http://+:3002
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 3002

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "CommunityService.dll"]
