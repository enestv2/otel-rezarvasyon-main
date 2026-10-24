FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy shared build settings and project files first to keep package restore cached.
COPY Directory.Build.props ./
COPY src/RPAOtelRezervasyon.Api/RPAOtelRezervasyon.Api.csproj src/RPAOtelRezervasyon.Api/
COPY src/RPAOtelRezervasyon.Application/RPAOtelRezervasyon.Application.csproj src/RPAOtelRezervasyon.Application/
COPY src/RPAOtelRezervasyon.Domain/RPAOtelRezervasyon.Domain.csproj src/RPAOtelRezervasyon.Domain/
COPY src/RPAOtelRezervasyon.Infrastructure/RPAOtelRezervasyon.Infrastructure.csproj src/RPAOtelRezervasyon.Infrastructure/
RUN dotnet restore src/RPAOtelRezervasyon.Api/RPAOtelRezervasyon.Api.csproj

# Copy source and the SVG embedded by the Infrastructure project explicitly.
COPY src/RPAOtelRezervasyon.Api/ src/RPAOtelRezervasyon.Api/
COPY src/RPAOtelRezervasyon.Application/ src/RPAOtelRezervasyon.Application/
COPY src/RPAOtelRezervasyon.Domain/ src/RPAOtelRezervasyon.Domain/
COPY src/RPAOtelRezervasyon.Infrastructure/ src/RPAOtelRezervasyon.Infrastructure/
COPY assets/logo.svg assets/logo.svg
RUN dotnet publish src/RPAOtelRezervasyon.Api/RPAOtelRezervasyon.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .
USER $APP_UID

ENTRYPOINT ["dotnet", "RPAOtelRezervasyon.Api.dll"]
