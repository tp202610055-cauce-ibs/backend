# Imagen de la API de Cauce. Una sola imagen sirve para el servidor y para los comandos
# de línea (migrate, seed-catalogs, seed-qa): docker run <imagen> migrate.

FROM mcr.microsoft.com/dotnet/sdk:9.0-bookworm-slim AS build
WORKDIR /src

# Restaura primero solo con los proyectos, para que la capa se reutilice si no cambian.
COPY src/Cauce.Domain/Cauce.Domain.csproj src/Cauce.Domain/
COPY src/Cauce.Application/Cauce.Application.csproj src/Cauce.Application/
COPY src/Cauce.Infrastructure/Cauce.Infrastructure.csproj src/Cauce.Infrastructure/
COPY src/Cauce.Api/Cauce.Api.csproj src/Cauce.Api/
RUN dotnet restore src/Cauce.Api/Cauce.Api.csproj

COPY src/ src/
# El manifiesto de QA se publica junto a la API (qa/); el resto de docs/ no entra a la imagen.
COPY docs/qa/ docs/qa/
RUN dotnet publish src/Cauce.Api/Cauce.Api.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0-bookworm-slim AS runtime

# tzdata: recordatorios en hora de Lima. libfontconfig1 y una fuente: QuestPDF y ScottPlot (SkiaSharp
# sin dependencias) no dibujan texto sin ellas. curl: healthcheck del contenedor.
RUN apt-get update \
    && apt-get install -y --no-install-recommends tzdata libfontconfig1 fonts-dejavu-core curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080 \
    TZ=UTC
EXPOSE 8080

USER app
ENTRYPOINT ["dotnet", "Cauce.Api.dll"]
