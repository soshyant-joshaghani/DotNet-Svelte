# Build context is the kit root: compose uses `context: .` and `dockerfile: backend/Dockerfile`.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /build

COPY backend/src/DotnetSvelte.Api.csproj backend/src/
COPY backend/worker/DotnetSvelte.Worker.csproj backend/worker/
RUN dotnet restore backend/worker/DotnetSvelte.Worker.csproj

COPY backend/src backend/src
COPY backend/worker backend/worker
RUN dotnet publish backend/src/DotnetSvelte.Api.csproj -c Release --no-restore -o /out/api \
    && dotnet publish backend/worker/DotnetSvelte.Worker.csproj -c Release --no-restore -o /out/worker

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

COPY --from=build /out/api /app
COPY --from=build /out/worker /app/worker
COPY backend/migrations /app/migrations

ENV MIGRATIONS_DIR=/app/migrations
EXPOSE 8000
USER $APP_UID
CMD ["dotnet", "/app/DotnetSvelte.Api.dll"]
