# ── Etapa 1: Build Frontend (React 19 + Vite) ─────────────────────────
FROM node:22-alpine AS frontend-build
WORKDIR /src/frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# ── Etapa 2: Build Backend (.NET 8 Web API) ───────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build
WORKDIR /src/backend
COPY backend/ ./
RUN dotnet publish src/Geolaboratorio.Api/Geolaboratorio.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# ── Etapa 3: Runtime Unificado (.NET 8 + SPA estático en wwwroot) ─────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=backend-build /app/publish ./
COPY --from=frontend-build /src/frontend/dist ./wwwroot

ENV ASPNETCORE_ENVIRONMENT=Production
ENV Seed__Demo=true
EXPOSE 8080

ENTRYPOINT ["dotnet", "Geolaboratorio.Api.dll"]
