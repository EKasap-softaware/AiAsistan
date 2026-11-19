using Asistan.Domain.Entities;

namespace Asistan.Application.Sohbet;

// Application "mesajları bir yere kaydet" der; MongoDB'yi bilmez.
public interface IMesajDeposu
{
    Task KaydetAsync(IEnumerable<Mesaj> mesajlar, CancellationToken ct = default);

    // Eskiden yeniye sıralı, oturumun son N mesajı.
    Task<IReadOnlyList<Mesaj>> SonMesajlarAsync(string oturumId, int adet, CancellationToken ct = default);
}
