FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["LT_Web_Nhom4/LT_Web_Nhom4.csproj", "LT_Web_Nhom4/"]
RUN dotnet restore "LT_Web_Nhom4/LT_Web_Nhom4.csproj"

COPY . .
RUN dotnet publish "LT_Web_Nhom4/LT_Web_Nhom4.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install --yes --no-install-recommends ca-certificates curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
RUN mkdir -p /app/App_Data/Uploads /home/app/.aspnet/DataProtection-Keys \
    && chown -R app:app /app \
    && chown -R app:app /home/app/.aspnet

USER app
EXPOSE 8080

ENV ASPNETCORE_HTTP_PORTS=8080
ENTRYPOINT ["sh", "-c", "dotnet LT_Web_Nhom4.dll --urls http://0.0.0.0:${PORT:-8080}"]
