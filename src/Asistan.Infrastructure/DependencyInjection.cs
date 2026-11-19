using System.Reflection;
using Asistan.Application.Bellek;
using Asistan.Application.Kuyruk;
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
        ["ConnectionStrings:Sql", "Mongo:Url", "LiteLlm:BaseUrl", "LiteLlm:Key",
         "RabbitMq:Host", "RabbitMq:Username", "RabbitMq:Password"];

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
            "• Sunucuda ortam değişkeni kullan: ':' yerine '__' yaz (Mongo__Url, LiteLlm__Key, ConnectionStrings__Sql).\n" +
            "• Geliştirmede yedek yol: src/Asistan.Api/appsettings.Local.json dosyası (Git'e girmez).\n\n" +
            TaniBilgisi(config));
    }

    // "Ayar neden okunamadı?" sorusunu cevaplayan, sır İÇERMEYEN tanı bilgisi:
    // hangi kullanıcı, hangi AppData, kasa dosyası var mı, hangi yapılandırma kaynakları yüklü.
    private static string TaniBilgisi(IConfiguration config)
    {
        // ÖNEMLİ: .NET kasayı Windows'un "bilinen AppData klasöründen" DEĞİL, APPDATA ORTAM DEĞİŞKENİNDEN bulur
        // (yoksa HOME). Süreç farklı bir APPDATA ile başlarsa kasa "kayıp" görünür; ikisini de gösteriyoruz.
        var appDataOrtam = Environment.GetEnvironmentVariable("APPDATA");
        var appDataBilinen = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var giris = Assembly.GetEntryAssembly();
        var satirlar = new List<string>
        {
            "Tanı bilgisi (sır içermez):",
            $"  Windows kullanıcısı        : {Environment.UserName}",
            $"  APPDATA ortam değişkeni    : {appDataOrtam ?? "(TANIMSIZ)"}   <- .NET kasayı BURADAN arar",
            $"  AppData (Windows klasörü)  : {appDataBilinen}",
            $"  Giriş derlemesi            : {giris?.GetName().Name ?? "(bulunamadı)"}",
        };

        // UserSecretsId, derlemeye gömülü bir attribute'tur. Paketi referans etmemek için yansıma (reflection) ile okuruz.
        var nitelik = giris?.GetCustomAttributes().FirstOrDefault(a => a.GetType().Name == "UserSecretsIdAttribute");
        var id = nitelik?.GetType().GetProperty("UserSecretsId")?.GetValue(nitelik) as string;
        if (id is null)
        {
            satirlar.Add("  UserSecretsId              : derlemede YOK (kasa yüklenemez)");
        }
        else
        {
            satirlar.Add($"  UserSecretsId              : {id}");

            // .NET'in gerçekten baktığı yer (APPDATA ortam değişkeni) ve Windows'un bilinen klasörü
            foreach (var (etiket, kok) in new[] { ("Kasa (.NET'in baktığı yer)", appDataOrtam), ("Kasa (Windows klasöründe)  ", appDataBilinen) })
            {
                if (string.IsNullOrEmpty(kok)) { satirlar.Add($"  {etiket}: (APPDATA yok, bakılamadı)"); continue; }
                var yol = Path.Combine(kok, "Microsoft", "UserSecrets", id, "secrets.json");
                satirlar.Add($"  {etiket}: {yol} ({(File.Exists(yol) ? "VAR" : "YOK")})");
            }
        }

        if (config is IConfigurationRoot koku)
            satirlar.Add("  Ayar kaynakları            : " + string.Join(" > ", koku.Providers.Select(p => p.ToString())));

        return string.Join("\n", satirlar);
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
        services.AddScoped<IIsDeposu, MongoIsDeposu>();
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
