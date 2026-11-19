using Asistan.Domain.Entities;

namespace Asistan.Application.Kullanicilar;

// Application "kullanıcıyı bir yere kaydet" der; nereye olduğunu bilmez.
public interface IKullaniciDeposu
{
    Task<Kullanici?> GetirAsync(int id, CancellationToken ct = default);
    Task<bool> EmailVarMiAsync(string email, CancellationToken ct = default);
    Task EkleAsync(Kullanici kullanici, CancellationToken ct = default);
}
