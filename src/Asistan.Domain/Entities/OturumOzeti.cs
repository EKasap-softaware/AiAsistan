namespace Asistan.Domain.Entities;

// Kısa süreli hafıza: bir oturumun eski mesajlarının özeti.
public class OturumOzeti
{
    public string Id { get; set; } = null!;
    public string OturumId { get; set; } = null!;
    public string Ozet { get; set; } = null!;

    // Oturumun ilk kaç mesajı bu özete dahil edildi? (Kalanlar prompt'a olduğu gibi girer.)
    public int OzetlenenMesajSayisi { get; set; }

    public DateTime Guncellenme { get; set; } = DateTime.UtcNow;
}
