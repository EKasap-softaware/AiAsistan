using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Asistan.Infrastructure.Persistence;

// EF araçları (dotnet ef migrations add / bundle) DbContext'i BU fabrikayla üretir.
//
// Neden gerekli? Fabrika olmazsa araçlar DbContext'i almak için uygulamanın Program.cs'ini çalıştırır; o da
// zorunlu ayarları (Mongo, RabbitMQ, LangFlow...) ister. Şemayı güncellemek için ise SADECE veritabanı bağlantısı
// gerekir. Fabrika, migration işini uygulamanın geri kalanından ve sırlarından ayırır.
//
// Bağlantı cümlesi: 1) ortam değişkeni ConnectionStrings__Sql (varsa)  2) sahte yer tutucu.
// "migrations add" veritabanına bağlanmaz, yer tutucu yeter. "migrations bundle" çalışırken verilen --connection
// seçeneği bunun üstüne yazar.
public class AppDbContextFabrikasi : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var baglanti = Environment.GetEnvironmentVariable("ConnectionStrings__Sql")
                       ?? "Host=localhost;Database=asistan;Username=tasarim;Password=tasarim";

        var secenekler = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(baglanti).Options;
        return new AppDbContext(secenekler);
    }
}
