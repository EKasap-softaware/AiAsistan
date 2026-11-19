using Asistan.Domain.Entities;

namespace Asistan.Application.Bellek;

public interface IHafizaDeposu
{
    Task EkleAsync(Hafiza hafiza, CancellationToken ct = default);

    // En yeni N hafıza (yeniden eskiye).
    Task<IReadOnlyList<Hafiza>> KullaniciHafizalariAsync(string kullaniciId, int adet, CancellationToken ct = default);

    // Başka kullanıcının hafızasını silmeyi engellemek için kullaniciId de kontrol edilir.
    Task<bool> SilAsync(string kullaniciId, string hafizaId, CancellationToken ct = default);
    Task<long> TumunuSilAsync(string kullaniciId, CancellationToken ct = default);
}

public interface IOturumOzetDeposu
{
    Task<OturumOzeti?> GetirAsync(string oturumId, CancellationToken ct = default);
    Task KaydetAsync(string oturumId, string ozet, int ozetlenenMesajSayisi, CancellationToken ct = default);
}
