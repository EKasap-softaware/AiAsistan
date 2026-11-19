using Asistan.Application.Bellek;
using Asistan.Application.Kuyruk;
using MassTransit;

namespace Asistan.Worker.Consumers;

// Cevap verildikten SONRA çalışan arka plan işi: oturumu özetle, kullanıcı hakkında kalıcı bilgi çıkar.
// (Aşama 8'de bu iş HTTP isteğinin içindeydi ve her soruda 1-2 ekstra model çağrısı bekletiyordu.)
public class SohbetTamamlandiConsumer(IHafizaServisi hafiza) : IConsumer<SohbetTamamlandi>
{
    public Task Consume(ConsumeContext<SohbetTamamlandi> ctx)
    {
        var m = ctx.Message;
        // SohbetSonrasiAsync kendi içinde hata yakalar ve loglar; kullanıcıyı etkilemez.
        return hafiza.SohbetSonrasiAsync(m.KullaniciId, m.OturumId, m.Soru, m.Cevap, ctx.CancellationToken);
    }
}
