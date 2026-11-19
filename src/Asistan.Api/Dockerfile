# API imajı: multi-stage build. Build bağlamı (context) çözüm klasörüdür (AiAsistan/), çünkü
# Directory.Build.props ve tüm projeler gerekir. Komut: docker build -f src/Asistan.Api/Dockerfile .

# ---- 1) Derleme aşaması: büyük SDK imajı (~800 MB). Son imaja GİRMEZ.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Önce SADECE proje dosyaları: paketleri indiren "restore" katmanı, kod değişse bile önbellekten gelir.
# (Kod her commit'te değişir, paketler nadiren; bu sıra derlemeyi dakikalardan saniyelere indirir.)
COPY Directory.Build.props ./
COPY src/Asistan.Domain/Asistan.Domain.csproj src/Asistan.Domain/
COPY src/Asistan.Application/Asistan.Application.csproj src/Asistan.Application/
COPY src/Asistan.Infrastructure/Asistan.Infrastructure.csproj src/Asistan.Infrastructure/
COPY src/Asistan.Api/Asistan.Api.csproj src/Asistan.Api/
RUN dotnet restore src/Asistan.Api/Asistan.Api.csproj

# Sonra kaynak kod ve yayınlama
COPY src/ src/
RUN dotnet publish src/Asistan.Api/Asistan.Api.csproj -c Release -o /app --no-restore

# ---- 2) Çalışma aşaması: sadece çalıştırmaya yetecek küçük imaj (~220 MB), SDK yok.
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app .

# Root olmayan kullanıcıyla çalış (imajda hazır gelen "app" kullanıcısı): imaj ele geçirilse bile yetki sınırlı kalır.
USER app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Asistan.Api.dll"]
