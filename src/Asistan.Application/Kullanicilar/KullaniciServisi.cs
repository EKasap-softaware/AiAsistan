using Asistan.Domain.Entities;

namespace Asistan.Application.Kullanicilar;

public record KullaniciOlusturIstegi(string Ad, string Email);

public class EmailZatenVarException(string email)
    : Exception($"'{email}' adresiyle kayıtlı bir kullanıcı zaten var.");

public interface IKullaniciServisi
{
    Task<Kullanici> OlusturAsync(KullaniciOlusturIstegi istek, CancellationToken ct = default);
    Task<Kullanici?> GetirAsync(int id, CancellationToken ct = default);
}

public class KullaniciServisi(IKullaniciDeposu depo) : IKullaniciServisi
{
    public async Task<Kullanici> OlusturAsync(KullaniciOlusturIstegi istek, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(istek.Ad))
            throw new ArgumentException("Ad boş olamaz.", nameof(istek));
        if (string.IsNullOrWhiteSpace(istek.Email) || !istek.Email.Contains('@'))
            throw new ArgumentException("Geçerli bir e-posta gerekli.", nameof(istek));

        var email = istek.Email.Trim().ToLowerInvariant();
        if (await depo.EmailVarMiAsync(email, ct))
            throw new EmailZatenVarException(email);

        var kullanici = new Kullanici { Ad = istek.Ad.Trim(), Email = email };
        await depo.EkleAsync(kullanici, ct);
        return kullanici;
    }

    public Task<Kullanici?> GetirAsync(int id, CancellationToken ct = default)
        => depo.GetirAsync(id, ct);
}
