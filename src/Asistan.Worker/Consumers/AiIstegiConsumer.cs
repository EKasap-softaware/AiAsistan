using Asistan.Application.Kuyruk;
using Asistan.Application.Sohbet;
using MassTransit;

namespace Asistan.Worker.Consumers;

// Kuyruktan "ai-istegi" mesajını alıp soruyu modele soran aşçı.
public class AiIstegiConsumer(IIsDeposu isler, ISohbetServisi sohbet, ILogger<AiIstegiConsumer> log) : IConsumer<AiIstegi>
{
    public async Task Consume(ConsumeContext<AiIstegi> ctx)
    {
        var m = ctx.Message;
        var ct = ctx.CancellationToken;

        // 1) İdempotent kapı: işi "al". İş zaten Tamamlandi/Hata ise (aynı mesaj ikinci kez geldi) ATLA.
        //    Dönüş false olunca return: mesaj onaylanır (ack) ve kuyruktan silinir.
        if (!await isler.IslemeAlAsync(m.IsId, ct))
        {
            log.LogInformation("İş {IsId} zaten işlenmiş veya yok, atlandı.", m.IsId);
            return;
        }

        // 2) Asıl iş: bağlamı kur, modele sor, soru-cevabı MongoDB'ye kaydet.
        //    Hata fırlarsa MassTransit retry kuralı devreye girer (Program.cs): 1 sn, 6 sn, 11 sn...
        var sonuc = await sohbet.SorAsync(m.KullaniciId, m.Soru, m.OturumId, ct);

        // 3) Cevabı takip fişine yaz: kullanıcı GET /api/isler/{isId} ile artık görebilir.
        await isler.TamamlaAsync(m.IsId, sonuc.Cevap, ct);

        // 4) Arka plan işini (özetleme + hafıza çıkarma) AYRI bir mesajla sıraya koy.
        //    Kullanıcı cevabı almak için bu işi beklemez.
        var uc = await ctx.GetSendEndpoint(new Uri($"queue:{KuyrukAdlari.SohbetTamamlandi}"));
        await uc.Send(new SohbetTamamlandi(m.KullaniciId, sonuc.OturumId, m.Soru, sonuc.Cevap), ct);

        log.LogInformation("İş {IsId} tamamlandı.", m.IsId);
    }
}
