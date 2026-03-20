# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src

COPY ["AdrPortal.slnx", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["global.json", "./"]
COPY ["src/AdrPortal.Web/AdrPortal.Web.csproj", "src/AdrPortal.Web/"]
COPY ["src/AdrPortal.Core/AdrPortal.Core.csproj", "src/AdrPortal.Core/"]
COPY ["src/AdrPortal.Infrastructure/AdrPortal.Infrastructure.csproj", "src/AdrPortal.Infrastructure/"]
COPY ["src/AdrPortal.ServiceDefaults/AdrPortal.ServiceDefaults.csproj", "src/AdrPortal.ServiceDefaults/"]

RUN dotnet restore "src/AdrPortal.Web/AdrPortal.Web.csproj"

FROM restore AS publish
COPY ["src/", "src/"]
RUN dotnet publish "src/AdrPortal.Web/AdrPortal.Web.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0 \
    ConnectionStrings__AdrPortal=Filename=/app/data/adrportal.db

WORKDIR /app

RUN groupadd --gid 10001 adrportal \
    && useradd --uid 10001 --gid adrportal --create-home --home-dir /home/adrportal adrportal \
    && mkdir -p /app/data /repos \
    && chown -R adrportal:adrportal /app /repos /home/adrportal

COPY --from=publish /app/publish/ ./

EXPOSE 8080
VOLUME ["/app/data", "/repos"]

USER adrportal

ENTRYPOINT ["dotnet", "AdrPortal.Web.dll"]
