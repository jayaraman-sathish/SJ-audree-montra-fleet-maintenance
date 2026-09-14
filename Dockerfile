# AU-Fleet-Ops v0.7 - Angular + ASP.NET Core single Render service
FROM node:20-alpine AS frontend-build
WORKDIR /src/frontend

COPY frontend/package*.json ./
RUN npm install

COPY frontend/ ./
RUN npm run build
RUN test -f /src/frontend/dist/montra-fleet-web/browser/index.html

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build
WORKDIR /src/backend

COPY backend/src/MontraFleet.Api/MontraFleet.Api.csproj ./
RUN dotnet restore MontraFleet.Api.csproj

COPY backend/src/MontraFleet.Api/ ./
RUN dotnet publish MontraFleet.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=backend-build /app/publish ./
COPY --from=frontend-build /src/frontend/dist/montra-fleet-web/browser ./wwwroot

RUN test -f /app/wwwroot/index.html

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://0.0.0.0:10000

EXPOSE 10000
ENTRYPOINT ["dotnet", "MontraFleet.Api.dll"]
