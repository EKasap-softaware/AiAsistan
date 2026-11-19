using System.Net;
using System.Text;
using Asistan.Application.Dokuman;
using Asistan.Application.Kuyruk;
using Asistan.Application.Sohbet;
using Asistan.Domain.Entities;
using Asistan.Infrastructure.LangFlow;
using FluentAssertions;
using Moq;

namespace Asistan.Tests;

public class DokumanServisiTests
{
    private readonly Mock<ILangFlowIstemcisi> _langflow = new();
    private readonly Mock<IMesajDeposu> _depo = new();
    private readonly DokumanServisi _servis;

    public DokumanServisiTests() => _servis = new DokumanServisi(_langflow.Object, _depo.Object);

    [Fact]
    public async Task SorAsync_Basarili_LangFlowCevabiniDonerVeSoruCevabiKaydeder()
    {
        _langflow.Setup(l => l.CalistirAsync("Worker nedir?", "o1", It.IsAny<CancellationToken>())).ReturnsAsync("Kuyruktaki işi yapan program.");
        List<Mesaj>? kaydedilen = null;
        _depo.Setup(d => d.KaydetAsync(It.IsAny<IEnumerable<Mesaj>>(), It.IsAny<CancellationToken>()))
             .Callback<IEnumerable<Mesaj>, CancellationToken>((m, _) => kaydedilen = m.ToList()).Returns(Task.CompletedTask);

        var sonuc = await _servis.SorAsync("k1", "  Worker nedir?  ", "o1");

        sonuc.Should().Be(new SohbetSonucu("Kuyruktaki işi yapan program.", "o1"));
        kaydedilen.Should().HaveCount(2);
        kaydedilen![0].Should().BeEquivalentTo(new { Rol = "user", Icerik = "Worker nedir?", OturumId = "o1", KullaniciId = "k1" });
        kaydedilen[1].Should().BeEquivalentTo(new { Rol = "assistant", Icerik = "Kuyruktaki işi yapan program." });
    }

    [Fact]
    public async Task SorAsync_OturumVerilmezse_YeniUretirVeLangFlowaAyniniGonderir()
    {
        string? gonderilenOturum = null;
        _langflow.Setup(l => l.CalistirAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .Callback<string, string, CancellationToken>((_, o, _) => gonderilenOturum = o).ReturnsAsync("cevap");

        var sonuc = await _servis.SorAsync("k1", "soru");

        sonuc.OturumId.Should().NotBeNullOrWhiteSpace();
        gonderilenOturum.Should().Be(sonuc.OturumId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SorAsync_BosSoru_HataVerirHicbirSeyYapmaz(string soru)
    {
        var islem = () => _servis.SorAsync("k1", soru);

        await islem.Should().ThrowAsync<ArgumentException>();
        _langflow.Verify(l => l.CalistirAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _depo.Verify(d => d.KaydetAsync(It.IsAny<IEnumerable<Mesaj>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SorAsync_LangFlowHataVerirse_HicbirSeyKaydedilmezHataYutulmaz()
    {
        _langflow.Setup(l => l.CalistirAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ThrowsAsync(new HttpRequestException("LangFlow kapalı"));

        var islem = () => _servis.SorAsync("k1", "soru");

        await islem.Should().ThrowAsync<HttpRequestException>();    // Worker'ın yeniden deneyebilmesi için fırlar
        _depo.Verify(d => d.KaydetAsync(It.IsAny<IEnumerable<Mesaj>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

public class LangFlowIstemcisiTests
{
    // HTTP katmanını sahteleriz: LangFlow'a gerçekten gitmeden, isteğin şeklini ve yanıt çözümünü sınarız.
    private sealed class SahteYanit(HttpStatusCode kod, string icerik) : HttpMessageHandler
    {
        public HttpRequestMessage? Istek;
        public string? Govde;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage istek, CancellationToken ct)
        {
            Istek = istek;
            Govde = istek.Content is null ? null : await istek.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(kod) { Content = new StringContent(icerik, Encoding.UTF8, "application/json") };
        }
    }

    private static (LangFlowIstemcisi Istemci, SahteYanit Sahte) Kur(HttpStatusCode kod, string icerik)
    {
        var sahte = new SahteYanit(kod, icerik);
        var http = new HttpClient(sahte) { BaseAddress = new Uri("http://localhost:7860") };
        return (new LangFlowIstemcisi(http, new LangFlowAyarlari("dokuman-asistani")), sahte);
    }

    private const string Basarili = """{"outputs":[{"outputs":[{"results":{"message":{"text":"Merhaba"}}}]}]}""";

    [Fact]
    public async Task Calistir_DogruUcaDogruGovdeylePostEderVeMetniCozer()
    {
        var (istemci, sahte) = Kur(HttpStatusCode.OK, Basarili);

        var cevap = await istemci.CalistirAsync("Worker nedir?", "oturum-1");

        cevap.Should().Be("Merhaba");
        sahte.Istek!.Method.Should().Be(HttpMethod.Post);
        sahte.Istek.RequestUri!.PathAndQuery.Should().Be("/api/v1/run/dokuman-asistani?stream=false");
        sahte.Govde.Should().Contain("\"input_value\":\"Worker nedir?\"")
                  .And.Contain("\"session_id\":\"oturum-1\"")
                  .And.Contain("\"input_type\":\"chat\"");
    }

    [Fact]
    public async Task Calistir_HttpHatasiDonerse_HttpRequestExceptionFirlatirKiWorkerYenidenDenesin()
    {
        var (istemci, _) = Kur(HttpStatusCode.InternalServerError, """{"detail":"Error running graph"}""");

        var islem = () => istemci.CalistirAsync("soru", "o1");

        await islem.Should().ThrowAsync<HttpRequestException>();
    }

    [Theory]
    [InlineData("""{"outputs":[]}""")]
    [InlineData("""{"baska":"bicim"}""")]
    [InlineData("""{"outputs":[{"outputs":[{"results":{"message":"duz-yazi"}}]}]}""")]
    public async Task Calistir_YanitBicimiBeklenenDegilse_NeOldugunuSoyleyenHataVerir(string yanit)
    {
        var (istemci, _) = Kur(HttpStatusCode.OK, yanit);

        var islem = () => istemci.CalistirAsync("soru", "o1");

        var hata = await islem.Should().ThrowAsync<InvalidOperationException>();
        hata.Which.Message.Should().Contain("beklenen biçimde değil");
    }

    [Fact]
    public async Task Calistir_MetinBossa_SessizceBosCevapDonmezHataVerir()
    {
        var (istemci, _) = Kur(HttpStatusCode.OK, """{"outputs":[{"outputs":[{"results":{"message":{"text":"  "}}}]}]}""");

        var islem = () => istemci.CalistirAsync("soru", "o1");

        var hata = await islem.Should().ThrowAsync<InvalidOperationException>();
        hata.Which.Message.Should().Contain("boş");
    }
}
