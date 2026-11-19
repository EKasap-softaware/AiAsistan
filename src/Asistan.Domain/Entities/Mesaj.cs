namespace Asistan.Domain.Entities;

// MongoDB'de "mesajlar" koleksiyonundaki bir belge.
// Domain MongoDB'yi tanımaz; Id'nin ObjectId olarak saklanması Infrastructure'da ayarlanır.
public class Mesaj
{
    public string Id { get; set; } = null!;          // MongoDB üretir
    public string OturumId { get; set; } = null!;    // Aynı sohbetin mesajlarını birbirine bağlar
    public string KullaniciId { get; set; } = null!;
    public string Rol { get; set; } = null!;         // "user" | "assistant"
    public string Icerik { get; set; } = null!;
    public DateTime Olusturulma { get; set; } = DateTime.UtcNow;
}
