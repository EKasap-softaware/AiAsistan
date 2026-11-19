using Asistan.Application.Bellek;
using Asistan.Application.Kuyruk;
using Asistan.Application.Sohbet;
using Asistan.Worker.Consumers;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Asistan.Tests;

// Consumer'ları RabbitMQ olmadan test ederiz: ConsumeContext (kuyruktan gelen mesaj + gönderme yeteneği) sahte.
public class WorkerConsumerTests
{
    private readonly Mock<IIsDeposu> _isler = new();
    private readonly Mock<ISohbetServisi> _sohbet = new();
    private readonly Mock<IHafizaServisi> _hafiza = new();

    private static readonly AiIstegi Istek = new("i1", "k1", "o1", "DI nedir?");

    // Sahte ConsumeContext + sahte "gönderme ucu" (SohbetTamamlandi buradan gider).
    private static (Mock<ConsumeContext<T>> Ctx, Mock<ISendEndpoint> Uc) Baglam<T>(T mesaj) where T : class
    {
        var ctx = new Mock<ConsumeContext<T>>();
        ctx.SetupGet(c => c.Message).Returns(mesaj);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        var uc = new Mock<ISendEndpoint>();
        uc.Setup(u => u.Send(It.IsAny<SohbetTamamlandi>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        ctx.Setup(c => c.GetSendEndpoint(It.IsAny<Uri>())).ReturnsAsync(uc.Object);
        return (ctx, uc);
    }

    private AiIstegiConsumer AiIstegiConsumerKur()
        => new(_isler.Object, _sohbet.Object, NullLogger<AiIstegiConsumer>.Instance);

    // ---------------- AiIstegiConsumer ----------------

    [Fact]
    public async Task AiIstegi_Normal_AlirSorarTamamlarVeArkaPlanIsiniSiraya_Koyar()
    {
        _isler.Setup(d => d.IslemeAlAsync("i1", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _sohbet.Setup(s => s.SorAsync("k1", "DI nedir?", "o1", It.IsAny<CancellationToken>()))
               .ReturnsAsync(new SohbetSonucu("Bağımlılığı dışarıdan almak.", "o1"));
        var (ctx, uc) = Baglam(Istek);

        await AiIstegiConsumerKur().Consume(ctx.Object);

        _isler.Verify(d => d.TamamlaAsync("i1", "Bağımlılığı dışarıdan almak.", It.IsAny<CancellationToken>()), Times.Once);
        // Arka plan işi, doğru KUYRUĞA ve doğru içerikle gönderilir.
        ctx.Verify(c => c.GetSendEndpoint(new Uri("queue:sohbet-tamamlandi")), Times.Once);
        uc.Verify(u => u.Send(new SohbetTamamlandi("k1", "o1", "DI nedir?", "Bağımlılığı dışarıdan almak."), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AiIstegi_IsZatenIslenmisse_HicbirSeyYapmazIdempotent()
    {
        _isler.Setup(d => d.IslemeAlAsync("i1", It.IsAny<CancellationToken>())).ReturnsAsync(false);   // Tamamlandi/Hata/yok
        var (ctx, uc) = Baglam(Istek);

        await AiIstegiConsumerKur().Consume(ctx.Object);   // hata fırlatmaz: mesaj onaylanıp kuyruktan silinir

        _sohbet.Verify(s => s.SorAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        _isler.Verify(d => d.TamamlaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        uc.Verify(u => u.Send(It.IsAny<SohbetTamamlandi>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AiIstegi_ModelHataVerirse_HatayiFirlatirKiYenidenDenensin_FisiTamamlamaz()
    {
        _isler.Setup(d => d.IslemeAlAsync("i1", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _sohbet.Setup(s => s.SorAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new HttpRequestException("LiteLLM kapalı"));
        var (ctx, uc) = Baglam(Istek);

        var islem = () => AiIstegiConsumerKur().Consume(ctx.Object);

        // Hata YUTULMAZ: yutulsaydı MassTransit "başarılı" sanıp mesajı silerdi, yeniden deneme olmazdı.
        await islem.Should().ThrowAsync<HttpRequestException>();
        _isler.Verify(d => d.TamamlaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        uc.Verify(u => u.Send(It.IsAny<SohbetTamamlandi>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------- SohbetTamamlandiConsumer ----------------

    [Fact]
    public async Task SohbetTamamlandi_HafizaServisininSohbetSonrasiniCalistirir()
    {
        var mesaj = new SohbetTamamlandi("k1", "o1", "soru", "cevap");
        var (ctx, _) = Baglam(mesaj);

        await new SohbetTamamlandiConsumer(_hafiza.Object).Consume(ctx.Object);

        _hafiza.Verify(h => h.SohbetSonrasiAsync("k1", "o1", "soru", "cevap", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------------- AiIstegiHataConsumer ----------------

    [Fact]
    public async Task Hata_TumDenemelerBitince_FisiHataYaparVeIcHatayiKullaniciyaSizdirmaz()
    {
        var ic = new Mock<ExceptionInfo>();
        ic.SetupGet(e => e.Message).Returns("Host=localhost;Password=GIZLI");   // iç hata ayrıntısı

        var fault = new Mock<Fault<AiIstegi>>();
        fault.SetupGet(f => f.Message).Returns(Istek);
        fault.SetupGet(f => f.Exceptions).Returns([ic.Object]);

        var ctx = new Mock<ConsumeContext<Fault<AiIstegi>>>();
        ctx.SetupGet(c => c.Message).Returns(fault.Object);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        await new AiIstegiHataConsumer(_isler.Object, NullLogger<AiIstegiHataConsumer>.Instance).Consume(ctx.Object);

        _isler.Verify(d => d.HataAsync("i1", "Soru işlenemedi, lütfen tekrar dene.", It.IsAny<CancellationToken>()), Times.Once);
        _isler.Verify(d => d.HataAsync(It.IsAny<string>(), It.Is<string>(s => s.Contains("GIZLI")), It.IsAny<CancellationToken>()), Times.Never);
    }
}
