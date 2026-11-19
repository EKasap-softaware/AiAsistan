using Asistan.Application.Bellek;
using Asistan.Application.Sohbet;
using Asistan.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Asistan.Tests;

public class HafizaServisiTests
{
    private readonly Mock<IMesajDeposu> _mesajlar = new();
    private readonly Mock<IHafizaDeposu> _hafizalar = new();
    private readonly Mock<IOturumOzetDeposu> _ozetler = new();
    private readonly Mock<ILlmIstemcisi> _llm = new();
    private readonly HafizaServisi _servis;

    // Küçük sayılarla test etmek kolay: ham pencere 2 mesaj, özetleme eşiği 4 mesaj.
    public HafizaServisiTests()
    {
        _servis = new HafizaServisi(
            _mesajlar.Object, _hafizalar.Object, _ozetler.Object, _llm.Object,
            new HafizaAyarlari(PencereBoyutu: 2, OzetEsigi: 4, MaksHafiza: 20, HizliModel: "hizli"),
            NullLogger<HafizaServisi>.Instance);

        _hafizalar.Setup(h => h.KullaniciHafizalariAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new List<Hafiza>());
        _ozetler.Setup(o => o.GetirAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((OturumOzeti?)null);
        _mesajlar.Setup(m => m.AralikGetirAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<Mesaj>());
    }

    private static Mesaj M(string rol, string icerik) => new() { Rol = rol, Icerik = icerik };

    private void LlmCevaplari(params string[] cevaplar)
    {
        var seri = _llm.SetupSequence(x => x.TamamlaAsync(It.IsAny<IEnumerable<LlmMesaj>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()));
        foreach (var c in cevaplar) seri = seri.ReturnsAsync(c);
    }

    // Sırayla cevap verir ve gelen her isteği kaydeder (isteklerin içeriğini doğrulamak için).
    private List<List<LlmMesaj>> LlmKaydet(params string[] cevaplar)
    {
        var istekler = new List<List<LlmMesaj>>();
        _llm.Setup(x => x.TamamlaAsync(It.IsAny<IEnumerable<LlmMesaj>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<LlmMesaj> m, string _, CancellationToken _) =>
            {
                istekler.Add(m.ToList());
                return Task.FromResult(cevaplar[Math.Min(istekler.Count - 1, cevaplar.Length - 1)]);
            });
        return istekler;
    }

    // ---------------- BaglamKurAsync ----------------

    [Fact]
    public async Task BaglamKur_HafizaVeOzetYok_SistemPromptiSadeceTemelMetindir()
    {
        var baglam = await _servis.BaglamKurAsync("k1", "o1", "Merhaba");

        baglam.Should().HaveCount(2);
        baglam[0].Should().Be(new LlmMesaj("system", SohbetServisi.SistemPrompt));
        baglam[1].Should().Be(new LlmMesaj("user", "Merhaba"));
    }

    [Fact]
    public async Task BaglamKur_HafizaVeOzetVarsa_SistemPromptunaEkler()
    {
        _hafizalar.Setup(h => h.KullaniciHafizalariAsync("k1", 20, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new List<Hafiza> { new() { Icerik = "Kullanıcının adı Emre." }, new() { Icerik = "Kullanıcı C# öğreniyor." } });
        _ozetler.Setup(o => o.GetirAsync("o1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OturumOzeti { OturumId = "o1", Ozet = "DI konuşuldu.", OzetlenenMesajSayisi = 6 });

        var baglam = await _servis.BaglamKurAsync("k1", "o1", "Devam");

        baglam[0].Rol.Should().Be("system");
        baglam[0].Icerik.Should().StartWith(SohbetServisi.SistemPrompt)
            .And.Contain("Kullanıcının adı Emre.")
            .And.Contain("Kullanıcı C# öğreniyor.")
            .And.Contain("DI konuşuldu.");
    }

    [Fact]
    public async Task BaglamKur_OzetlenmisMesajlariAtlarVeHamMesajlariSirayaKoyar()
    {
        _ozetler.Setup(o => o.GetirAsync("o1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OturumOzeti { OturumId = "o1", Ozet = "özet", OzetlenenMesajSayisi = 6 });
        _mesajlar.Setup(m => m.AralikGetirAsync("o1", 6, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<Mesaj> { M("user", "s7"), M("assistant", "c7") });

        var baglam = await _servis.BaglamKurAsync("k1", "o1", "s8");

        baglam.Select(m => m.Rol).Should().Equal("system", "user", "assistant", "user");
        baglam.Select(m => m.Icerik).Skip(1).Should().Equal("s7", "c7", "s8");
        _mesajlar.Verify(m => m.AralikGetirAsync("o1", 6, It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------------- SohbetSonrasiAsync: özetleme ----------------

    [Fact]
    public async Task SohbetSonrasi_EsikAsilmadiysa_OzetlemezSadeceHafizaCikarmayiDener()
    {
        _mesajlar.Setup(m => m.SayAsync("o1", It.IsAny<CancellationToken>())).ReturnsAsync(4);  // 4 - 0 = 4, eşik 4 → aşılmadı
        LlmCevaplari("YOK");

        await _servis.SohbetSonrasiAsync("k1", "o1", "soru", "cevap");

        _ozetler.Verify(o => o.KaydetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _llm.Verify(x => x.TamamlaAsync(It.IsAny<IEnumerable<LlmMesaj>>(), "hizli", It.IsAny<CancellationToken>()), Times.Once); // sadece hafıza çıkarma
    }

    [Fact]
    public async Task SohbetSonrasi_EsikAsildiysa_EskileriOzetleyipYeniSinirlaKaydeder()
    {
        _mesajlar.Setup(m => m.SayAsync("o1", It.IsAny<CancellationToken>())).ReturnsAsync(6);   // 6 > 4
        // Aralık: ilk 0 mesajı atla, (6 - pencere 2) - 0 = 4 mesaj al
        _mesajlar.Setup(m => m.AralikGetirAsync("o1", 0, 4, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<Mesaj> { M("user", "ilk soru"), M("assistant", "ilk cevap"), M("user", "ikinci soru"), M("assistant", "ikinci cevap") });
        var istekler = LlmKaydet("  Kısa özet.  ", "YOK");

        await _servis.SohbetSonrasiAsync("k1", "o1", "soru", "cevap");

        _ozetler.Verify(o => o.KaydetAsync("o1", "Kısa özet.", 4, It.IsAny<CancellationToken>()), Times.Once);
        istekler.Should().HaveCount(2);   // 1) özetleme  2) hafıza çıkarma
        istekler[0][1].Icerik.Should().Contain("Kullanıcı: ilk soru").And.Contain("Asistan: ilk cevap").And.Contain("Kullanıcı: ikinci soru");
    }

    [Fact]
    public async Task SohbetSonrasi_OncekiOzetVarsa_YeniOzetIstegiOnuDaIcerir()
    {
        _ozetler.Setup(o => o.GetirAsync("o1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OturumOzeti { OturumId = "o1", Ozet = "ESKI OZET", OzetlenenMesajSayisi = 4 });
        _mesajlar.Setup(m => m.SayAsync("o1", It.IsAny<CancellationToken>())).ReturnsAsync(10);   // 10 - 4 = 6 > 4
        _mesajlar.Setup(m => m.AralikGetirAsync("o1", 4, 4, It.IsAny<CancellationToken>()))       // (10-2) - 4 = 4 mesaj
                 .ReturnsAsync(new List<Mesaj> { M("user", "a"), M("assistant", "b"), M("user", "c"), M("assistant", "d") });
        var istekler = LlmKaydet("YENI OZET", "YOK");

        await _servis.SohbetSonrasiAsync("k1", "o1", "soru", "cevap");

        istekler[0][1].Icerik.Should().Contain("Önceki özet: ESKI OZET");
        _ozetler.Verify(o => o.KaydetAsync("o1", "YENI OZET", 8, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------------- SohbetSonrasiAsync: kalıcı hafıza ----------------

    [Fact]
    public async Task SohbetSonrasi_ModelYeniBilgiVerirse_HafizayaKaydeder()
    {
        LlmCevaplari("Kullanıcının adı Emre.");

        await _servis.SohbetSonrasiAsync("k1", "o1", "Ben Emre", "Merhaba Emre");

        _hafizalar.Verify(h => h.EkleAsync(
            It.Is<Hafiza>(x => x.KullaniciId == "k1" && x.Icerik == "Kullanıcının adı Emre."),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("YOK")]
    [InlineData("yok.")]
    [InlineData("  Yok  ")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SohbetSonrasi_ModelYokDerseVeyaBosDonerse_HafizaKaydetmez(string modelCiktisi)
    {
        LlmCevaplari(modelCiktisi);

        await _servis.SohbetSonrasiAsync("k1", "o1", "soru", "cevap");

        _hafizalar.Verify(h => h.EkleAsync(It.IsAny<Hafiza>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SohbetSonrasi_ZatenBilinenBilgiyiBuyukKucukHarfFarkinaBakmadanTekrarlamaz()
    {
        _hafizalar.Setup(h => h.KullaniciHafizalariAsync("k1", 20, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new List<Hafiza> { new() { Icerik = "Kullanıcının adı Emre." } });
        LlmCevaplari("kullanıcının adı emre.");

        await _servis.SohbetSonrasiAsync("k1", "o1", "soru", "cevap");

        _hafizalar.Verify(h => h.EkleAsync(It.IsAny<Hafiza>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SohbetSonrasi_CokUzunCikti_HafizayaYazmaz()
    {
        LlmCevaplari(new string('a', 201));

        await _servis.SohbetSonrasiAsync("k1", "o1", "soru", "cevap");

        _hafizalar.Verify(h => h.EkleAsync(It.IsAny<Hafiza>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SohbetSonrasi_CokSatirliCikti_TekSatirlaBirlestirirVeMaddeIsaretiniAtar()
    {
        LlmCevaplari("- Kullanıcı Emre.\nİstanbul'da yaşıyor.");   // system prompt'a satır sonu enjekte edilemesin

        await _servis.SohbetSonrasiAsync("k1", "o1", "soru", "cevap");

        _hafizalar.Verify(h => h.EkleAsync(
            It.Is<Hafiza>(x => x.Icerik == "Kullanıcı Emre. İstanbul'da yaşıyor."),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------------- Dayanıklılık ----------------

    [Fact]
    public async Task SohbetSonrasi_LlmHataVerirse_HataFirlatmaz()
    {
        _llm.Setup(x => x.TamamlaAsync(It.IsAny<IEnumerable<LlmMesaj>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("LLM kapalı"));

        var islem = () => _servis.SohbetSonrasiAsync("k1", "o1", "soru", "cevap");

        await islem.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SohbetSonrasi_OzetlemeBasarisizOlsaBile_HafizaCikarmayiYineDener()
    {
        _mesajlar.Setup(m => m.SayAsync("o1", It.IsAny<CancellationToken>())).ReturnsAsync(6);
        _mesajlar.Setup(m => m.AralikGetirAsync("o1", 0, 4, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<Mesaj> { M("user", "a") });
        _llm.SetupSequence(x => x.TamamlaAsync(It.IsAny<IEnumerable<LlmMesaj>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("özetleme patladı"))
            .ReturnsAsync("Kullanıcı C# öğreniyor.");

        await _servis.SohbetSonrasiAsync("k1", "o1", "soru", "cevap");

        _hafizalar.Verify(h => h.EkleAsync(It.Is<Hafiza>(x => x.Icerik == "Kullanıcı C# öğreniyor."), It.IsAny<CancellationToken>()), Times.Once);
        _ozetler.Verify(o => o.KaydetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------- Hafıza yönetimi (görme ve silme hakkı) ----------------

    [Fact]
    public async Task HafizaSil_DeposuAynenKullanicidaGecirir()
    {
        _hafizalar.Setup(h => h.SilAsync("k1", "h1", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var sonuc = await _servis.HafizaSilAsync("k1", "h1");

        sonuc.Should().BeTrue();
        _hafizalar.Verify(h => h.SilAsync("k1", "h1", It.IsAny<CancellationToken>()), Times.Once);
    }
}
