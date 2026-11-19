namespace Asistan.Domain.Entities;

// Uzun süreli hafıza: kullanıcı hakkında kalıcı, kısa bir bilgi ("Kullanıcı C# öğreniyor").
public class Hafiza
{
    public string Id { get; set; } = null!;
    public string KullaniciId { get; set; } = null!;
    public string Icerik { get; set; } = null!;
    public DateTime Olusturulma { get; set; } = DateTime.UtcNow;
}
