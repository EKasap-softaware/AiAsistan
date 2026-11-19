using Asistan.Application.Kuyruk;
using MassTransit;

namespace Asistan.Worker.Consumers;

// Tüm yeniden denemeler tükenince MassTransit "Fault<AiIstegi>" mesajı yayınlar (asıl mesaj da "_error" kuyruğuna gider).
// Burada takip fişini "Hata" yaparız ki kullanıcı sonsuza dek "Isleniyor" görmesin.
public class AiIstegiHataConsumer(IIsDeposu isler, ILogger<AiIstegiHataConsumer> log) : IConsumer<Fault<AiIstegi>>
{
    public async Task Consume(ConsumeContext<Fault<AiIstegi>> ctx)
    {
        var isId = ctx.Message.Message.IsId;
        var neden = ctx.Message.Exceptions.FirstOrDefault()?.Message ?? "Bilinmeyen hata";

        log.LogError("İş {IsId} kalıcı olarak başarısız: {Neden}", isId, neden);

        // Kullanıcıya iç hata ayrıntısını (bağlantı cümlesi vb. sızabilir) göstermeyiz.
        await isler.HataAsync(isId, "Soru işlenemedi, lütfen tekrar dene.", ctx.CancellationToken);
    }
}
