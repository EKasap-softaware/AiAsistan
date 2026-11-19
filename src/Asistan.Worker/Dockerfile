# Worker imajı. API'nin Dockerfile'ı ile aynı mantık; fark: web sunucusu yok, bu yüzden daha küçük "runtime" imajı.
# Komut: docker build -f src/Asistan.Worker/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/Asistan.Domain/Asistan.Domain.csproj src/Asistan.Domain/
COPY src/Asistan.Application/Asistan.Application.csproj src/Asistan.Application/
COPY src/Asistan.Infrastructure/Asistan.Infrastructure.csproj src/Asistan.Infrastructure/
COPY src/Asistan.Worker/Asistan.Worker.csproj src/Asistan.Worker/
RUN dotnet restore src/Asistan.Worker/Asistan.Worker.csproj

COPY src/ src/
RUN dotnet publish src/Asistan.Worker/Asistan.Worker.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
WORKDIR /app
COPY --from=build /app .
USER app
ENTRYPOINT ["dotnet", "Asistan.Worker.dll"]
