# ── build & publish ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution + every .csproj so the restore layer is cached independently
# of source changes (invalidated only when DineOS.slnx or a .csproj changes).
COPY DineOS.slnx Directory.Build.props ./
COPY src/DineOS.Api/DineOS.Api.csproj                           src/DineOS.Api/
COPY src/DineOS.Application/DineOS.Application.csproj           src/DineOS.Application/
COPY src/DineOS.Domain/DineOS.Domain.csproj                     src/DineOS.Domain/
COPY src/DineOS.Infrastructure/DineOS.Infrastructure.csproj     src/DineOS.Infrastructure/
COPY tests/DineOS.Tests/DineOS.Tests.csproj                     tests/DineOS.Tests/

RUN dotnet restore DineOS.slnx

COPY . .

RUN dotnet publish src/DineOS.Api/DineOS.Api.csproj \
      -c Release \
      --no-restore \
      -o /app/publish \
      /p:UseAppHost=false

# ── runtime ───────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

RUN addgroup -S -g 1001 appgroup \
 && adduser  -S -u 1001 -G appgroup appuser \
 && mkdir -p /app/uploads \
 && chown -R appuser:appgroup /app

COPY --from=build --chown=appuser:appgroup /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true

USER 1001
EXPOSE 8080
ENTRYPOINT ["dotnet", "DineOS.Api.dll"]
