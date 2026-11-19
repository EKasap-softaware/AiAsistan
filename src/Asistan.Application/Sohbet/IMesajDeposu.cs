using Asistan.Domain.Entities;

namespace Asistan.Application.Sohbet;

// Application "mesajları bir yere kaydet" der; MongoDB'yi bilmez.
public interface IMesajDeposu
{
    Task KaydetAsync(IEnumerable<Mesaj> mesajlar, CancellationToken ct = default);

    // Eskiden yeniye sıralı, oturumun son N mesajı.
    Task<IReadOnlyList<Mesaj>> SonMesajlarAsync(string oturumId, int adet, CancellationToken ct = default);

    // Oturumdaki toplam mesaj sayısı.
    Task<int> SayAsync(string oturumId, CancellationToken ct = default);

    // Eskiden yeniye sıralı: ilk "atla" mesajı geç, sonraki "adet" mesajı getir.
    Task<IReadOnlyList<Mesaj>> AralikGetirAsync(string oturumId, int atla, int adet, CancellationToken ct = default);
}
