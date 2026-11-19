using Asistan.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Asistan.Tests;

public class AyarDogrulamaTests
{
    private static IConfiguration Ayarlar(params (string Anahtar, string? Deger)[] ayarlar)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(ayarlar.Select(a => new KeyValuePair<string, string?>(a.Anahtar, a.Deger)))
            .Build();

    private static readonly (string, string?)[] Hepsi =
    [
        ("ConnectionStrings:Sql", "Host=localhost;Database=x"),
        ("Mongo:Url", "mongodb://localhost:27017"),
        ("LiteLlm:BaseUrl", "http://localhost:4000"),
        ("LiteLlm:Key", "anahtar"),
        ("RabbitMq:Host", "localhost"),
        ("RabbitMq:Username", "kullanici"),
        ("RabbitMq:Password", "sifre")
    ];

    [Fact]
    public void AyarlariDogrula_HicAyarYoksa_EksikOlanlariVeCozumuSoyler()
    {
        var islem = () => DependencyInjection.AyarlariDogrula(Ayarlar());

        var hata = islem.Should().Throw<InvalidOperationException>().Which;
        hata.Message.Should()
            .Contain("ConnectionStrings:Sql").And.Contain("Mongo:Url")
            .And.Contain("LiteLlm:BaseUrl").And.Contain("LiteLlm:Key")
            .And.Contain("RabbitMq:Host").And.Contain("RabbitMq:Username").And.Contain("RabbitMq:Password")
            .And.Contain("Development").And.Contain("user-secrets");
    }

    [Fact]
    public void AyarlariDogrula_SadeceMongoUrlEksikse_SadeceOnuListeler()
    {
        var ayarlar = Ayarlar(Hepsi.Where(a => a.Item1 != "Mongo:Url").ToArray());

        var islem = () => DependencyInjection.AyarlariDogrula(ayarlar);

        var mesaj = islem.Should().Throw<InvalidOperationException>().Which.Message;
        mesaj.Should().Contain("Eksik ayar(lar): Mongo:Url.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AyarlariDogrula_DegerBosVeyaBoslukSa_EksikSayilir(string bos)
    {
        var ayarlar = Ayarlar(Hepsi.Select(a => a.Item1 == "LiteLlm:Key" ? (a.Item1, (string?)bos) : a).ToArray());

        var islem = () => DependencyInjection.AyarlariDogrula(ayarlar);

        islem.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("LiteLlm:Key");
    }

    [Fact]
    public void AyarlariDogrula_Mesaji_SirIcermeyenTaniBilgisiVeYedekYoluIcerir()
    {
        var islem = () => DependencyInjection.AyarlariDogrula(Ayarlar());

        var mesaj = islem.Should().Throw<InvalidOperationException>().Which.Message;
        mesaj.Should().Contain("Tanı bilgisi")
            .And.Contain("APPDATA ortam değişkeni")      // .NET kasayı buradan arar
            .And.Contain("UserSecretsId")                   // (testhost'ta "derlemede YOK" yazar, gerçek API'de kasa yolları da çıkar)
            .And.Contain("Ayar kaynakları")
            .And.Contain("appsettings.Local.json");        // yedek yol
    }

    [Fact]
    public void AyarlariDogrula_HepsiVarsa_HataFirlatmaz()
    {
        var islem = () => DependencyInjection.AyarlariDogrula(Ayarlar(Hepsi));

        islem.Should().NotThrow();
    }

    [Fact]
    public void AddInfrastructure_AyarEksikse_ServisleriKaydetmedenAcikHataVerir()
    {
        var islem = () => new ServiceCollection().AddInfrastructure(Ayarlar());

        islem.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("Eksik ayar");
    }

    [Fact]
    public void AddInfrastructure_AyarlarTamsa_HataFirlatmaz()
    {
        // Bağlantılar tembeldir (lazy): servisler kaydedilir ama gerçek bağlantı kurulmaz.
        var islem = () => new ServiceCollection().AddInfrastructure(Ayarlar(Hepsi));

        islem.Should().NotThrow();
    }
}
