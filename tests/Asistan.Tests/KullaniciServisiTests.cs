using Asistan.Application.Kullanicilar;
using Asistan.Domain.Entities;
using FluentAssertions;
using Moq;

namespace Asistan.Tests;

public class KullaniciServisiTests
{
    // Her test kendi sahte deposunu ve servisini kurar: testler birbirinden bağımsız.
    private readonly Mock<IKullaniciDeposu> _depo = new();
    private readonly KullaniciServisi _servis;

    public KullaniciServisiTests() => _servis = new KullaniciServisi(_depo.Object);

    [Fact]
    public async Task OlusturAsync_GecerliIstek_KullaniciyiKaydeder()
    {
        _depo.Setup(d => d.EmailVarMiAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(false);

        var sonuc = await _servis.OlusturAsync(new KullaniciOlusturIstegi("Emre", "emre@test.com"));

        sonuc.Ad.Should().Be("Emre");
        _depo.Verify(d => d.EkleAsync(
            It.Is<Kullanici>(k => k.Email == "emre@test.com"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OlusturAsync_EmailBuyukHarfliVeBosluklu_KucukHarfeCevirir()
    {
        _depo.Setup(d => d.EmailVarMiAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(false);

        var sonuc = await _servis.OlusturAsync(new KullaniciOlusturIstegi("Emre", "  Emre@Test.COM "));

        sonuc.Email.Should().Be("emre@test.com");
    }

    [Fact]
    public async Task OlusturAsync_EmailZatenVar_HataFirlatirVeKaydetmez()
    {
        _depo.Setup(d => d.EmailVarMiAsync("emre@test.com", It.IsAny<CancellationToken>()))
             .ReturnsAsync(true);

        var islem = () => _servis.OlusturAsync(new KullaniciOlusturIstegi("Emre", "emre@test.com"));

        await islem.Should().ThrowAsync<EmailZatenVarException>();
        _depo.Verify(d => d.EkleAsync(It.IsAny<Kullanici>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("", "a@b.com")]
    [InlineData("   ", "a@b.com")]
    [InlineData("Emre", "")]
    [InlineData("Emre", "mailyok")]
    public async Task OlusturAsync_GecersizGiris_ArgumentExceptionFirlatir(string ad, string email)
    {
        var islem = () => _servis.OlusturAsync(new KullaniciOlusturIstegi(ad, email));

        await islem.Should().ThrowAsync<ArgumentException>();
        _depo.Verify(d => d.EkleAsync(It.IsAny<Kullanici>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetirAsync_KullaniciVar_DeposundakiniDoner()
    {
        var beklenen = new Kullanici { Id = 7, Ad = "Emre", Email = "emre@test.com" };
        _depo.Setup(d => d.GetirAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(beklenen);

        var sonuc = await _servis.GetirAsync(7);

        sonuc.Should().BeSameAs(beklenen);
    }

    [Fact]
    public async Task GetirAsync_KullaniciYok_NullDoner()
    {
        _depo.Setup(d => d.GetirAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((Kullanici?)null);

        var sonuc = await _servis.GetirAsync(99);

        sonuc.Should().BeNull();
    }
}
