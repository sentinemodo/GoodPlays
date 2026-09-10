FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY src/GoodPlays.Api/GoodPlays.Api.csproj src/GoodPlays.Api/
COPY src/GoodPlays.Domain/GoodPlays.Domain.csproj src/GoodPlays.Domain/
COPY src/GoodPlays.Infrastructure/GoodPlays.Infrastructure.csproj src/GoodPlays.Infrastructure/
COPY src/GoodPlays.Ml/GoodPlays.Ml.csproj src/GoodPlays.Ml/

RUN dotnet restore src/GoodPlays.Api/GoodPlays.Api.csproj

COPY src/ src/
RUN dotnet publish src/GoodPlays.Api/GoodPlays.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "GoodPlays.Api.dll"]
