using Asistan.Application.Sohbet;
using Asistan.Domain.Entities;

namespace Asistan.Application.Dokuman;

// "LangFlow'daki akışı çalıştır" arayüzü. LangFlow'u, HTTP'yi bilmez; gerçek hali Infrastructure'da.
public interface ILangFlowIstemcisi
{
    Task<string> CalistirAsync(string soru, string oturumId, CancellationToken ct = default);
}

public interface IDokumanServisi
{
    Task<SohbetSonucu> SorAsync(string kullaniciId, string soru, string? oturumId = null, CancellationToken ct = default);
}

// Doküman modu: soruyu, proje dokümanlarını okuyan LangFlow akışına sorar.
// Sohbet modundan farkı: hafıza ve özet kullanılmaz (her soru dokümanlardan cevaplanır), ama soru-cevap yine
// kaydedilir, böylece GET /api/sohbet/{oturumId}/mesajlar ile görülebilir.
public class DokumanServisi(ILangFlowIstemcisi langflow, IMesajDeposu mesajlar) : IDokumanServisi
{
    public async Task<SohbetSonucu> SorAsync(string kullaniciId, string soru, string? oturumId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(soru))
            throw new ArgumentException("Soru boş olamaz.", nameof(soru));

        oturumId = string.IsNullOrWhiteSpace(oturumId) ? Guid.NewGuid().ToString("N") : oturumId;
        var temizSoru = soru.Trim();

        // Önce LangFlow cevap versin. Hata olursa hiçbir şey kaydedilmez (yetim soru kalmasın).
        var cevap = await langflow.CalistirAsync(temizSoru, oturumId, ct);

        var simdi = DateTime.UtcNow;
        await mesajlar.KaydetAsync(
        [
            new Mesaj { OturumId = oturumId, KullaniciId = kullaniciId, Rol = "user", Icerik = temizSoru, Olusturulma = simdi },
            new Mesaj { OturumId = oturumId, KullaniciId = kullaniciId, Rol = "assistant", Icerik = cevap, Olusturulma = simdi }
        ], ct);

        return new SohbetSonucu(cevap, oturumId);
    }
}
