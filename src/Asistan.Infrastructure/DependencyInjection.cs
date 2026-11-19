using Asistan.Application.Bellek;
using Asistan.Application.Kullanicilar;
using Asistan.Application.Sohbet;
using Asistan.Infrastructure.Llm;
using Asistan.Infrastructure.Mongo;
using Asistan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Asistan.Infrastructure;

public static class DependencyInjection
{
    // Uygulamanın çalışması için şart olan ayarlar. Eksikse anlamsız bir sürücü hatası yerine
    // neyin eksik olduğunu ve nasıl verileceğini söyleyen açık bir hata veririz.
    public static readonly string[] ZorunluAyarlar =
        ["ConnectionStrings:Sql", "Mongo:Url", "LiteLlm:BaseUrl", "LiteLlm:Key"];

    public static void AyarlariDogrula(IConfiguration config)
    {
        var eksik = ZorunluAyarlar.Where(k => string.IsNullOrWhiteSpace(config[k])).ToList();
        if (eksik.Count == 0) return;

        var ortam = config["ASPNETCORE_ENVIRONMENT"] ?? config["DOTNET_ENVIRONMENT"] ?? "Production (varsayılan)";
        throw new InvalidOperationException(
            $"Eksik ayar(lar): {string.Join(", ", eksik)}. Çalışma ortamı: {ortam}.\n" +
            "• Bu ayarlar Git'te yoktur. Geliştirmede 'dotnet user-secrets' kasasından okunur, ama kasa SADECE " +
            "ortam 'Development' iken yüklenir. Visual Studio'da başlatma profilini 'http' veya 'https' seç " +
            "(ya da ASPNETCORE_ENVIRONMENT=Development ayarla).\n" +
            "• Kasada yoksa: dotnet user-secrets set \"Mongo:Url\" \"<değer>\" -p src/Asistan.Api  (docs/05-sorun-giderme.md).\n" +
            "• Sunucuda ortam değişkeni kullan: ':' yerine '__' yaz (Mongo__Url, LiteLlm__Key, ConnectionStrings__Sql).");
    }

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        AyarlariDogrula(config);

        // --- PostgreSQL (EF Core) ---
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(config.GetConnectionString("Sql")));
        services.AddScoped<IKullaniciDeposu, KullaniciDeposu>();

        // --- MongoDB ---
        // MongoClient thread-safe'tir ve içinde bağlantı havuzu tutar: Singleton.
        MongoSinifHaritalari.Kaydet();
        services.AddSingleton<IMongoClient>(_ => new MongoClient(config["Mongo:Url"]));
        services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(config["Mongo:Database"] ?? "asistan"));
        services.AddScoped<IMesajDeposu, MongoMesajDeposu>();
        services.AddScoped<IHafizaDeposu, MongoHafizaDeposu>();
        services.AddScoped<IOturumOzetDeposu, MongoOturumOzetDeposu>();
        services.AddHostedService<MongoIndexServisi>();

        // --- LiteLLM ---
        // Typed HttpClient: adres ve anahtar tek yerde, LLM çağrıları 120 sn'ye kadar bekler.
        services.AddHttpClient<ILlmIstemcisi, LiteLlmIstemcisi>(c =>
        {
            c.BaseAddress = new Uri(config["LiteLlm:BaseUrl"]!);
            c.DefaultRequestHeaders.Authorization = new("Bearer", config["LiteLlm:Key"]);
            c.Timeout = TimeSpan.FromSeconds(120);
        });
        return services;
    }
}
