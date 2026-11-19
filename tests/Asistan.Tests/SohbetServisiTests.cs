using Asistan.Application.Sohbet;
using Asistan.Domain.Entities;
using FluentAssertions;
using Moq;

namespace Asistan.Tests;

public class SohbetServisiTests
{
    private readonly Mock<ILlmIstemcisi> _llm = new();
    private readonly Mock<IMesajDeposu> _depo = new();
    private readonly SohbetServisi _servis;

    public SohbetServisiTests() => _servis = new SohbetServisi(_llm.Object, _depo.Object);

    private void LlmCevapVerir(string cevap)
        => _llm.Setup(x => x.TamamlaAsync(It.IsAny<IEnumerable<LlmMesaj>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(cevap);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SorAsync_BosSoru_HataFirlatirLlmCagrilmazKaydedilmez(string soru)
    {
        var islem = () => _servis.SorAsync("kullanici-1", soru);

        await islem.Should().ThrowAsync<ArgumentException>();
        _llm.Verify(x => x.TamamlaAsync(It.IsAny<IEnumerable<LlmMesaj>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _depo.Verify(d => d.KaydetAsync(It.IsAny<IEnumerable<Mesaj>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SorAsync_GecerliSoru_ModelinCevabiniDoner()
    {
        LlmCevapVerir("Merhaba!");

        var sonuc = await _servis.SorAsync("kullanici-1", "Selam");

        sonuc.Cevap.Should().Be("Merhaba!");
    }

    [Fact]
    public async Task SorAsync_GecerliSoru_SistemPromptuVeSoruyuSirayladaGonderir()
    {
        List<LlmMesaj>? gonderilen = null;
        _llm.Setup(x => x.TamamlaAsync(It.IsAny<IEnumerable<LlmMesaj>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<LlmMesaj>, string, CancellationToken>((m, _, _) => gonderilen = m.ToList())
            .ReturnsAsync("ok");

        await _servis.SorAsync("kullanici-1", "  DI nedir?  ");

        gonderilen.Should().NotBeNull();
        gonderilen!.Should().HaveCount(2);
        gonderilen[0].Should().Be(new LlmMesaj("system", SohbetServisi.SistemPrompt));
        gonderilen[1].Should().Be(new LlmMesaj("user", "DI nedir?"));
    }

    [Fact]
    public async Task SorAsync_Basarili_SoruVeCevabiAyniOturumaKaydeder()
    {
        LlmCevapVerir("Cevap");
        List<Mesaj>? kaydedilen = null;
        _depo.Setup(d => d.KaydetAsync(It.IsAny<IEnumerable<Mesaj>>(), It.IsAny<CancellationToken>()))
             .Callback<IEnumerable<Mesaj>, CancellationToken>((m, _) => kaydedilen = m.ToList())
             .Returns(Task.CompletedTask);

        var sonuc = await _servis.SorAsync("kullanici-1", "  Soru  ", "oturum-A");

        sonuc.OturumId.Should().Be("oturum-A");
        kaydedilen.Should().NotBeNull();
        kaydedilen!.Should().HaveCount(2);
        kaydedilen.Should().OnlyContain(m => m.OturumId == "oturum-A" && m.KullaniciId == "kullanici-1");
        kaydedilen[0].Should().BeEquivalentTo(new { Rol = "user", Icerik = "Soru" });
        kaydedilen[1].Should().BeEquivalentTo(new { Rol = "assistant", Icerik = "Cevap" });
    }

    [Fact]
    public async Task SorAsync_OturumIdVerilmezse_YeniOturumIdUretir()
    {
        LlmCevapVerir("Cevap");

        var ilk = await _servis.SorAsync("kullanici-1", "Soru");
        var ikinci = await _servis.SorAsync("kullanici-1", "Soru");

        ilk.OturumId.Should().NotBeNullOrWhiteSpace();
        ikinci.OturumId.Should().NotBe(ilk.OturumId);
    }

    [Fact]
    public async Task SorAsync_LlmHataVerirse_HicbirSeyKaydedilmez()
    {
        _llm.Setup(x => x.TamamlaAsync(It.IsAny<IEnumerable<LlmMesaj>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("LLM kapalı"));

        var islem = () => _servis.SorAsync("kullanici-1", "Soru");

        await islem.Should().ThrowAsync<HttpRequestException>();
        _depo.Verify(d => d.KaydetAsync(It.IsAny<IEnumerable<Mesaj>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MesajlariGetirAsync_AdetiSinirlar()
    {
        _depo.Setup(d => d.SonMesajlarAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<Mesaj>());

        await _servis.MesajlariGetirAsync("oturum-A", 5000);

        _depo.Verify(d => d.SonMesajlarAsync("oturum-A", 100, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MesajlariGetirAsync_BosOturumId_HataFirlatir()
    {
        var islem = () => _servis.MesajlariGetirAsync(" ");

        await islem.Should().ThrowAsync<ArgumentException>();
    }
}
