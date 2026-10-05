# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore dependencies first (cached layer).
COPY backend/SEF_Project.Api/SEF_Project.Api.csproj backend/SEF_Project.Api/
RUN dotnet restore backend/SEF_Project.Api/SEF_Project.Api.csproj

# Copy the rest of the source and publish a release build.
COPY backend/SEF_Project.Api/ backend/SEF_Project.Api/
RUN dotnet publish backend/SEF_Project.Api/SEF_Project.Api.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 80

# Bind to the port Render exposes (PORT), falling back to 80 locally.
ENTRYPOINT ["sh", "-c", "dotnet SEF_Project.Api.dll --urls http://0.0.0.0:${PORT:-80}"]
