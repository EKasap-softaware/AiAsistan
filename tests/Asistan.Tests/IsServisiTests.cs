using Asistan.Application.Kuyruk;
using Asistan.Domain.Entities;
using FluentAssertions;
using Moq;

namespace Asistan.Tests;

public class IsServisiTests
{
    private readonly Mock<IIsDeposu> _isler = new();
    private readonly Mock<IIsYayinci> _yayinci = new();
    private readonly IsServisi _servis;

    public IsServisiTests() => _servis = new IsServisi(_isler.Object, _yayinci.Object);

    [Fact]
    public async Task KuyrugaAl_Gecerli_OnceFisiYazarSonraKuyrugaGonderir()
    {
        var sira = new List<string>();
        IsKaydi? fis = null;
        AiIstegi? mesaj = null;
        _isler.Setup(d => d.OlusturAsync(It.IsAny<IsKaydi>(), It.IsAny<CancellationToken>()))
              .Callback<IsKaydi, CancellationToken>((k, _) => { sira.Add("fis"); fis = k; })
              .Returns(Task.CompletedTask);
        _yayinci.Setup(y => y.AiIstegiGonderAsync(It.IsAny<AiIstegi>(), It.IsAny<CancellationToken>()))
                .Callback<AiIstegi, CancellationToken>((m, _) => { sira.Add("kuyruk"); mesaj = m; })
                .Returns(Task.CompletedTask);

        var sonuc = await _servis.KuyrugaAlAsync("k1", "  DI nedir?  ", "oturum-A");

        sira.Should().Equal("fis", "kuyruk");            // Önce fiş: kullanıcı hemen sorgulayabilsin
        sonuc.Should().BeSameAs(fis);
        fis!.Durum.Should().Be(IsDurumu.Bekliyor);
        fis.Soru.Should().Be("DI nedir?");                 // kırpılır
        mesaj.Should().Be(new AiIstegi(fis.Id, "k1", "oturum-A", "DI nedir?"));   // fişle kuyruktaki mesaj aynı kimlikte
    }

    [Fact]
    public async Task KuyrugaAl_OturumIdVerilmezse_YeniUretirVerilirseKorur()
    {
        var yeni1 = await _servis.KuyrugaAlAsync("k1", "soru");
        var yeni2 = await _servis.KuyrugaAlAsync("k1", "soru");
        var verilen = await _servis.KuyrugaAlAsync("k1", "soru", "benim-oturumum");

        yeni1.OturumId.Should().NotBeNullOrWhiteSpace();
        yeni2.OturumId.Should().NotBe(yeni1.OturumId);
        yeni1.Id.Should().NotBe(yeni2.Id);                 // her işin kendi numarası var
        verilen.OturumId.Should().Be("benim-oturumum");
    }

    [Theory]
    [InlineData("", "soru")]
    [InlineData("   ", "soru")]
    [InlineData("k1", "")]
    [InlineData("k1", "   ")]
    public async Task KuyrugaAl_BosKullaniciVeyaSoru_HataVerirHicbirSeyYapmaz(string kullanici, string soru)
    {
        var islem = () => _servis.KuyrugaAlAsync(kullanici, soru);

        await islem.Should().ThrowAsync<ArgumentException>();
        _isler.Verify(d => d.OlusturAsync(It.IsAny<IsKaydi>(), It.IsAny<CancellationToken>()), Times.Never);
        _yayinci.Verify(y => y.AiIstegiGonderAsync(It.IsAny<AiIstegi>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task KuyrugaAl_GonderimBasarisizsa_FisiHataYapipKuyrukHatasiFirlatir()
    {
        _yayinci.Setup(y => y.AiIstegiGonderAsync(It.IsAny<AiIstegi>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("RabbitMQ kapalı"));

        var islem = () => _servis.KuyrugaAlAsync("k1", "soru");

        var hata = await islem.Should().ThrowAsync<KuyrukHatasi>();
        hata.Which.InnerException.Should().BeOfType<InvalidOperationException>();   // asıl neden kaybolmaz
        // Fiş sonsuza dek "Bekliyor" kalmasın: Hata olarak işaretlenir.
        _isler.Verify(d => d.HataAsync(It.IsAny<string>(), "Kuyruğa atılamadı.", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task KuyrugaAl_GonderimAsiliKalirsa_ZamanAsimindaFisiHataYapipKuyrukHatasiFirlatir()
    {
        // RabbitMQ kapalıyken gerçekte yaşanan durum: gönderim ASLA bitmez.
        _yayinci.Setup(y => y.AiIstegiGonderAsync(It.IsAny<AiIstegi>(), It.IsAny<CancellationToken>()))
                .Returns(new TaskCompletionSource().Task);
        var servis = new IsServisi(_isler.Object, _yayinci.Object, gonderimZamanAsimi: TimeSpan.FromMilliseconds(50));

        var islem = () => servis.KuyrugaAlAsync("k1", "soru");

        var hata = await islem.Should().ThrowAsync<KuyrukHatasi>();
        hata.Which.InnerException.Should().BeOfType<TimeoutException>();
        _isler.Verify(d => d.HataAsync(It.IsAny<string>(), "Kuyruğa atılamadı.", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task KuyrugaAl_IstemciVazgecerse_FisiYineKapatirVeIptaliOlduguGibiIletir()
    {
        using var iptal = new CancellationTokenSource();
        _yayinci.Setup(y => y.AiIstegiGonderAsync(It.IsAny<AiIstegi>(), It.IsAny<CancellationToken>()))
                .Returns(new TaskCompletionSource().Task);
        var servis = new IsServisi(_isler.Object, _yayinci.Object, gonderimZamanAsimi: TimeSpan.FromSeconds(30));

        var islem = () => servis.KuyrugaAlAsync("k1", "soru", ct: iptal.Token);
        var gorev = islem();
        iptal.Cancel();

        await gorev.Invoking(g => g).Should().ThrowAsync<OperationCanceledException>();
        // İptal sırasında ct iptal olmuş olsa da fiş sahipsiz kalmasın diye Hata yazılır (CancellationToken.None ile).
        _isler.Verify(d => d.HataAsync(It.IsAny<string>(), "Kuyruğa atılamadı.", CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task KuyrugaAl_ModVerilmezse_SohbetOlurVerilirseKorunurKucukHarfeCevrilir()
    {
        AiIstegi? mesaj = null;
        _yayinci.Setup(y => y.AiIstegiGonderAsync(It.IsAny<AiIstegi>(), It.IsAny<CancellationToken>()))
                .Callback<AiIstegi, CancellationToken>((m, _) => mesaj = m).Returns(Task.CompletedTask);

        var varsayilan = await _servis.KuyrugaAlAsync("k1", "soru");
        var dokuman = await _servis.KuyrugaAlAsync("k1", "soru", mod: "  DOKUMAN ");

        varsayilan.Mod.Should().Be("sohbet");
        dokuman.Mod.Should().Be("dokuman");
        mesaj!.Mod.Should().Be("dokuman");               // fişteki mod kuyruğa giden mesaja da taşınır
    }

    [Fact]
    public async Task KuyrugaAl_BilinmeyenMod_HataVerirHicbirSeyYapmaz()
    {
        var islem = () => _servis.KuyrugaAlAsync("k1", "soru", mod: "hackle");

        var hata = await islem.Should().ThrowAsync<ArgumentException>();
        hata.Which.Message.Should().Contain("sohbet").And.Contain("dokuman");   // geçerli modları söyler
        _isler.Verify(d => d.OlusturAsync(It.IsAny<IsKaydi>(), It.IsAny<CancellationToken>()), Times.Never);
        _yayinci.Verify(y => y.AiIstegiGonderAsync(It.IsAny<AiIstegi>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Getir_DeposuyaIletir()
    {
        var kayit = new IsKaydi { Id = "i1" };
        _isler.Setup(d => d.GetirAsync("i1", It.IsAny<CancellationToken>())).ReturnsAsync(kayit);

        (await _servis.GetirAsync("i1")).Should().BeSameAs(kayit);
    }
}
