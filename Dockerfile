FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY Phisio.sln ./
COPY Phisio.Domain/Phisio.Domain.csproj Phisio.Domain/
COPY Phisio.Application/Phisio.Application.csproj Phisio.Application/
COPY Phisio.Infrastructure/Phisio.Infrastructure.csproj Phisio.Infrastructure/
COPY Phisio.Api/Phisio.Api.csproj Phisio.Api/

RUN dotnet restore Phisio.Api/Phisio.Api.csproj

COPY Phisio.Domain/ Phisio.Domain/
COPY Phisio.Application/ Phisio.Application/
COPY Phisio.Infrastructure/ Phisio.Infrastructure/
COPY Phisio.Api/ Phisio.Api/

RUN dotnet publish Phisio.Api/Phisio.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
  CMD curl -f http://localhost:8080/api/health || exit 1

ENTRYPOINT ["dotnet", "Phisio.Api.dll"]
