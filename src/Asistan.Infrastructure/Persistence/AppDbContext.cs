using Asistan.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Asistan.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Kullanici> Kullanicilar => Set<Kullanici>();
    public DbSet<Abonelik> Abonelikler => Set<Abonelik>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Kullanici>(e =>
        {
            e.HasIndex(k => k.Email).IsUnique();
            e.Property(k => k.Email).HasMaxLength(200);
            e.Property(k => k.Ad).HasMaxLength(100);
        });
        mb.Entity<Abonelik>().Property(a => a.Plan).HasMaxLength(50);
    }
}
