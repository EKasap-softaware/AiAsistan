namespace Asistan.Domain.Entities;

public class Kullanici
{
    public int Id { get; set; }
    public string Ad { get; set; } = null!;
    public string Email { get; set; } = null!;
    public DateTime KayitTarihi { get; set; } = DateTime.UtcNow;

    public List<Abonelik> Abonelikler { get; set; } = new(); // 1-N ilişki
}
