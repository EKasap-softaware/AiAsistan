using Asistan.Application.Dokuman;
using Asistan.Application.Kuyruk;
using Asistan.Application.Sohbet;
using MassTransit;

namespace Asistan.Worker.Consumers;

// Kuyruktan "ai-istegi" mesajını alıp soruyu cevaplayan aşçı.
public class AiIstegiConsumer(
    IIsDeposu isler,
    ISohbetServisi sohbet,
    IDokumanServisi dokuman,
    ILogger<AiIstegiConsumer> log) : IConsumer<AiIstegi>
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

        // 2) Asıl iş: mod'a göre doğru servise yönlendir. Hata fırlarsa MassTransit retry kuralı devreye girer.
        //    sohbet : hafızalı normal sohbet (bağlamı kur, LiteLLM'e sor, kaydet)
        //    dokuman: proje dokümanlarını okuyan LangFlow akışına sor, kaydet
        var dokumanModu = m.Mod == SohbetModlari.Dokuman;
        var sonuc = dokumanModu
            ? await dokuman.SorAsync(m.KullaniciId, m.Soru, m.OturumId, ct)
            : await sohbet.SorAsync(m.KullaniciId, m.Soru, m.OturumId, ct);

        // 3) Cevabı takip fişine yaz: kullanıcı GET /api/isler/{isId} ile artık görebilir.
        await isler.TamamlaAsync(m.IsId, sonuc.Cevap, ct);

        // 4) Arka plan işi (özetleme + hafıza çıkarma) SADECE sohbet modunda: doküman soruları kullanıcı
        //    hakkında kalıcı bilgi taşımaz ve hafızayı kirletmesin.
        if (!dokumanModu)
        {
            var uc = await ctx.GetSendEndpoint(new Uri($"queue:{KuyrukAdlari.SohbetTamamlandi}"));
            await uc.Send(new SohbetTamamlandi(m.KullaniciId, sonuc.OturumId, m.Soru, sonuc.Cevap), ct);
        }

        log.LogInformation("İş {IsId} tamamlandı (mod: {Mod}).", m.IsId, m.Mod);
    }
}
