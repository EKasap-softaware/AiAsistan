using Asistan.Application.Bellek;
using Asistan.Domain.Entities;

namespace Asistan.Application.Sohbet;

public record SohbetSonucu(string Cevap, string OturumId);

public interface ISohbetServisi
{
    // oturumId verilmezse yeni bir sohbet başlar ve sonuçta yeni OturumId döner.
    Task<SohbetSonucu> SorAsync(string kullaniciId, string soru, string? oturumId = null, CancellationToken ct = default);

    Task<IReadOnlyList<Mesaj>> MesajlariGetirAsync(string oturumId, int adet = 20, CancellationToken ct = default);
}

public class SohbetServisi(ILlmIstemcisi llm, IMesajDeposu mesajlar, IHafizaServisi hafiza) : ISohbetServisi
{
    public const string SistemPrompt = "Sen bir .NET eğitmenisin. Türkçe, kısa ve net cevap ver.";

    public async Task<SohbetSonucu> SorAsync(string kullaniciId, string soru, string? oturumId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(soru))
            throw new ArgumentException("Soru boş olamaz.", nameof(soru));

        oturumId = string.IsNullOrWhiteSpace(oturumId) ? Guid.NewGuid().ToString("N") : oturumId;
        var temizSoru = soru.Trim();

        // 1) Hafızadan bağlamı kur: system (+ kullanıcı hafızaları + özet) + ham mesajlar + yeni soru
        var baglam = await hafiza.BaglamKurAsync(kullaniciId, oturumId, temizSoru, ct);

        // 2) Model cevap versin. Hata olursa (LLM kapalı, kredi yok) hiçbir şey kaydedilmez,
        //    böylece veritabanında cevapsız "yetim" soru kalmaz.
        var cevap = await llm.TamamlaAsync(baglam, ct: ct);

        // 3) Soru ve cevabı kaydet
        var simdi = DateTime.UtcNow;
        await mesajlar.KaydetAsync(
        [
            new Mesaj { OturumId = oturumId, KullaniciId = kullaniciId, Rol = "user", Icerik = temizSoru, Olusturulma = simdi },
            new Mesaj { OturumId = oturumId, KullaniciId = kullaniciId, Rol = "assistant", Icerik = cevap, Olusturulma = simdi }
        ], ct);

        // Özetleme ve kalıcı bilgi çıkarma BURADA yapılmaz: cevap hazır olur olmaz kullanıcıya dönmeli.
        // O iş, Worker'da "SohbetTamamlandi" mesajıyla ayrı bir adım olarak çalışır (Aşama 9).
        return new SohbetSonucu(cevap, oturumId);
    }

    public async Task<IReadOnlyList<Mesaj>> MesajlariGetirAsync(string oturumId, int adet = 20, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(oturumId))
            throw new ArgumentException("Oturum id boş olamaz.", nameof(oturumId));

        return await mesajlar.SonMesajlarAsync(oturumId, Math.Clamp(adet, 1, 100), ct);
    }
}
