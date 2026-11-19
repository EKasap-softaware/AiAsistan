# Lab 01 çözümü: mevcut dosyalarda yapılacak değişiklikler

1. `src/Asistan.Infrastructure/Persistence/AppDbContext.cs`
   - `public DbSet<Etiket> Etiketler => Set<Etiket>();`
   - `OnModelCreating` içine: `mb.Entity<Etiket>().Property(e => e.Ad).HasMaxLength(50);`
2. `src/Asistan.Application/DependencyInjection.cs`
   - `using Asistan.Application.Etiketler;` ve `services.AddScoped<IEtiketServisi, EtiketServisi>();`
3. `src/Asistan.Infrastructure/DependencyInjection.cs`
   - `services.AddScoped<IEtiketDeposu, EtiketDeposu>();` (IKullaniciDeposu satırının altına) + `using Asistan.Application.Etiketler;`
4. Migration:
   `dotnet ef migrations add EtiketEkle -p src/Asistan.Infrastructure -s src/Asistan.Api -o Persistence/Migrations`
