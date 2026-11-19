namespace Asistan.Domain.Entities;

public class Abonelik
{
    public int Id { get; set; }
    public string Plan { get; set; } = null!;
    public int KullaniciId { get; set; }                // Foreign key
    public Kullanici Kullanici { get; set; } = null!;   // Navigation property
}
