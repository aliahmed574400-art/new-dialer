FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["global.json", "./"]
COPY ["src/NewDialer.Api/NewDialer.Api.csproj", "src/NewDialer.Api/"]
COPY ["src/NewDialer.Application/NewDialer.Application.csproj", "src/NewDialer.Application/"]
COPY ["src/NewDialer.Infrastructure/NewDialer.Infrastructure.csproj", "src/NewDialer.Infrastructure/"]
COPY ["src/NewDialer.Contracts/NewDialer.Contracts.csproj", "src/NewDialer.Contracts/"]
COPY ["src/NewDialer.Domain/NewDialer.Domain.csproj", "src/NewDialer.Domain/"]

RUN dotnet restore "src/NewDialer.Api/NewDialer.Api.csproj"

COPY . .
RUN dotnet publish "src/NewDialer.Api/NewDialer.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 10000

CMD ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-10000} dotnet NewDialer.Api.dll"]
