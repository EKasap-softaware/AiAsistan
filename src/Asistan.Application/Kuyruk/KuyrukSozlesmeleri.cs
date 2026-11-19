using Asistan.Domain.Entities;

namespace Asistan.Application.Kuyruk;

// ---- Kuyruğa giden mesajlar (kuyruk sözleşmesi) ----
// Hem API (gönderen) hem Worker (alan) bu sınıfları kullanır. Düz C# kaydıdır: MassTransit'i bilmez.

// "Bu soruyu modele sor." (API → Worker)
// Mod: "sohbet" (hafızalı normal sohbet) veya "dokuman" (proje dokümanlarını okuyan LangFlow akışı).
// Varsayılan değer sayesinde eski biçimdeki (Mod'suz) mesajlar da "sohbet" olarak okunur.
public record AiIstegi(string IsId, string KullaniciId, string OturumId, string Soru, string Mod = SohbetModlari.Sohbet);

// Sorunun nasıl cevaplanacağını seçer.
public static class SohbetModlari
{
    public const string Sohbet = "sohbet";
    public const string Dokuman = "dokuman";

    // Boş/null → "sohbet". Büyük/küçük harf fark etmez. Bilinmeyen mod → ArgumentException (API bunu 400 yapar).
    public static string Dogrula(string? mod)
    {
        if (string.IsNullOrWhiteSpace(mod)) return Sohbet;
        var temiz = mod.Trim().ToLowerInvariant();
        return temiz is Sohbet or Dokuman
            ? temiz
            : throw new ArgumentException($"Geçersiz mod '{mod}'. Geçerli modlar: {Sohbet}, {Dokuman}.", nameof(mod));
    }
}

// "Cevap verildi, şimdi özetleme ve hafıza çıkarma işini yap." (Worker → Worker, arka plan işi)
public record SohbetTamamlandi(string KullaniciId, string OturumId, string Soru, string Cevap);

// Kuyruk adları tek yerde: gönderen ve alan aynı adı kullanmak ZORUNDA.
public static class KuyrukAdlari
{
    public const string AiIstegi = "ai-istegi";
    public const string AiIstegiHata = "ai-istegi-hata";
    public const string SohbetTamamlandi = "sohbet-tamamlandi";
}

// ---- Arayüzler (Application "ne" olacağını söyler, "nasıl" Infrastructure'da) ----

// Kuyruğa mesaj gönderme. Gerçek hali MassTransit + RabbitMQ.
public interface IIsYayinci
{
    Task AiIstegiGonderAsync(AiIstegi istek, CancellationToken ct = default);
}

// İş takip fişlerinin deposu. Gerçek hali MongoDB.
public interface IIsDeposu
{
    Task OlusturAsync(IsKaydi is_, CancellationToken ct = default);
    Task<IsKaydi?> GetirAsync(string isId, CancellationToken ct = default);

    // Worker işi almadan önce çağırır. Sadece Bekliyor/Isleniyor durumundaki iş alınabilir ve
    // atomik olarak Isleniyor yapılır (DenemeSayisi artar). Tamamlandi/Hata/yok ise false döner:
    // aynı mesaj ikinci kez gelirse (at-least-once teslimat) tekrar işlenmez = idempotent.
    Task<bool> IslemeAlAsync(string isId, CancellationToken ct = default);

    Task TamamlaAsync(string isId, string cevap, CancellationToken ct = default);
    Task HataAsync(string isId, string hata, CancellationToken ct = default);
}
