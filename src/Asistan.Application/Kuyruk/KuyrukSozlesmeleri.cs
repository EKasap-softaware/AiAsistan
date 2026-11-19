using Asistan.Domain.Entities;

namespace Asistan.Application.Kuyruk;

// ---- Kuyruğa giden mesajlar (kuyruk sözleşmesi) ----
// Hem API (gönderen) hem Worker (alan) bu sınıfları kullanır. Düz C# kaydıdır: MassTransit'i bilmez.

// "Bu soruyu modele sor." (API → Worker)
public record AiIstegi(string IsId, string KullaniciId, string OturumId, string Soru);

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
