# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /app/out

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out ./

# Render provides PORT env var. Bind Kestrel to it.
# Don't EXPOSE a fixed port; just listen on $PORT.
CMD ["/bin/bash", "-lc", "ASPNETCORE_URLS=http://0.0.0.0:$PORT dotnet UrlShortener.dll"]
