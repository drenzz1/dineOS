FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/DineOS.Api/DineOS.Api.csproj", "src/DineOS.Api/"]
COPY ["src/DineOS.Application/DineOS.Application.csproj", "src/DineOS.Application/"]
COPY ["src/DineOS.Domain/DineOS.Domain.csproj", "src/DineOS.Domain/"]
COPY ["src/DineOS.Infrastructure/DineOS.Infrastructure.csproj", "src/DineOS.Infrastructure/"]

RUN dotnet restore "src/DineOS.Api/DineOS.Api.csproj"

COPY . .

WORKDIR "/src/src/DineOS.Api"
RUN dotnet build "DineOS.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DineOS.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
RUN mkdir -p /app/uploads
ENTRYPOINT ["dotnet", "DineOS.Api.dll"]
