namespace Asistan.Domain.Entities;

public enum IsDurumu
{
    Bekliyor,     // Kuyruğa atıldı, Worker henüz almadı
    Isleniyor,    // Worker aldı, model cevabını bekliyor
    Tamamlandi,   // Cevap hazır
    Hata          // Defalarca denendi, olmadı
}

// Kuyruğa atılan bir sorunun "takip fişi". API soruyu kuyruğa bırakır, kullanıcı bu kayda bakarak cevabı alır.
public class IsKaydi
{
    public string Id { get; set; } = null!;          // İş numarası (isId); API üretir
    public string KullaniciId { get; set; } = null!;
    public string OturumId { get; set; } = null!;
    public string Soru { get; set; } = null!;
    public IsDurumu Durum { get; set; } = IsDurumu.Bekliyor;
    public string? Cevap { get; set; }
    public string? Hata { get; set; }
    public int DenemeSayisi { get; set; }            // Worker bu işi kaç kez aldı (yeniden denemeleri görmek için)
    public DateTime Olusturulma { get; set; } = DateTime.UtcNow;
    public DateTime Guncellenme { get; set; } = DateTime.UtcNow;
}
