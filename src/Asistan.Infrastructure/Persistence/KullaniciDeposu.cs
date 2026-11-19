using Asistan.Application.Kullanicilar;
using Asistan.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Asistan.Infrastructure.Persistence;

public class KullaniciDeposu(AppDbContext db) : IKullaniciDeposu
{
    public Task<Kullanici?> GetirAsync(int id, CancellationToken ct = default)
        => db.Kullanicilar.AsNoTracking()
             .Include(k => k.Abonelikler)
             .FirstOrDefaultAsync(k => k.Id == id, ct);

    public Task<bool> EmailVarMiAsync(string email, CancellationToken ct = default)
        => db.Kullanicilar.AnyAsync(k => k.Email == email, ct);

    public async Task EkleAsync(Kullanici kullanici, CancellationToken ct = default)
    {
        db.Kullanicilar.Add(kullanici);
        await db.SaveChangesAsync(ct);
    }
}
