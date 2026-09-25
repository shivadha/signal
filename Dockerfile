# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy solution and project files first for optimal layer caching
COPY Signal.sln .
COPY src/Signal.Domain/Signal.Domain.csproj src/Signal.Domain/
COPY src/Signal.Application/Signal.Application.csproj src/Signal.Application/
COPY src/Signal.Infrastructure/Signal.Infrastructure.csproj src/Signal.Infrastructure/
COPY src/Signal.Api/Signal.Api.csproj src/Signal.Api/
COPY src/Signal.Worker/Signal.Worker.csproj src/Signal.Worker/
COPY tests/Signal.UnitTests/Signal.UnitTests.csproj tests/Signal.UnitTests/
COPY tests/Signal.IntegrationTests/Signal.IntegrationTests.csproj tests/Signal.IntegrationTests/

RUN dotnet restore

# Copy full source and build
COPY . .
RUN dotnet build -c Release --no-restore
RUN dotnet test -c Release --no-build

# Publish API & Worker
RUN dotnet publish src/Signal.Api/Signal.Api.csproj -c Release -o /app/api --no-build -p:ErrorOnDuplicatePublishOutputFiles=false
RUN dotnet publish src/Signal.Worker/Signal.Worker.csproj -c Release -o /app/worker --no-build -p:ErrorOnDuplicatePublishOutputFiles=false

# Runtime Stage for API
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

RUN mkdir -p /app/data && chown -R $APP_UID:$APP_UID /app/data
VOLUME ["/app/data"]

USER $APP_UID
COPY --from=build --chown=$APP_UID:$APP_UID /app/api .
ENTRYPOINT ["dotnet", "Signal.Api.dll"]
