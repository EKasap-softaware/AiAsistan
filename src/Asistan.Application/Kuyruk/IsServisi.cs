using Asistan.Domain.Entities;

namespace Asistan.Application.Kuyruk;

// Kuyruğa mesaj atılamadığında (RabbitMQ kapalı vb.) fırlatılır; API bunu 503'e çevirir.
public class KuyrukHatasi(string mesaj, Exception? ic = null) : Exception(mesaj, ic);

public interface IIsServisi
{
    // Soruyu kuyruğa bırakır ve hemen döner (cevap beklemez).
    Task<IsKaydi> KuyrugaAlAsync(string kullaniciId, string soru, string? oturumId = null, CancellationToken ct = default);

    Task<IsKaydi?> GetirAsync(string isId, CancellationToken ct = default);
}

public class IsServisi(IIsDeposu isler, IIsYayinci yayinci, TimeSpan? gonderimZamanAsimi = null) : IIsServisi
{
    // RabbitMQ kapalıyken kuyruk istemcisi çok uzun (bizde >70 sn) bekleyebilir. Kullanıcıyı asılı bırakmayız.
    private readonly TimeSpan _zamanAsimi = gonderimZamanAsimi ?? TimeSpan.FromSeconds(5);

    public async Task<IsKaydi> KuyrugaAlAsync(string kullaniciId, string soru, string? oturumId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(kullaniciId))
            throw new ArgumentException("Kullanıcı id boş olamaz.", nameof(kullaniciId));
        if (string.IsNullOrWhiteSpace(soru))
            throw new ArgumentException("Soru boş olamaz.", nameof(soru));

        var kayit = new IsKaydi
        {
            Id = Guid.NewGuid().ToString("N"),
            KullaniciId = kullaniciId,
            // Oturum id'sini burada üretiriz ki kullanıcı daha cevap gelmeden sohbet kimliğini bilsin.
            OturumId = string.IsNullOrWhiteSpace(oturumId) ? Guid.NewGuid().ToString("N") : oturumId,
            Soru = soru.Trim()
        };

        // Önce takip fişini yaz, SONRA kuyruğa at. Böylece kullanıcı işi hemen sorgulayabilir ve
        // Worker işe başladığında fiş zaten vardır.
        await isler.OlusturAsync(kayit, ct);

        try
        {
            // WaitAsync: gönderim 5 sn içinde bitmezse TimeoutException. (Gönderim arka planda sürse bile
            // sorun değil: fiş "Hata" olacağı için Worker o mesajı alırsa idempotent kapıda atlar.)
            await yayinci.AiIstegiGonderAsync(new AiIstegi(kayit.Id, kayit.KullaniciId, kayit.OturumId, kayit.Soru), ct)
                         .WaitAsync(_zamanAsimi, ct);
        }
        catch (Exception ex)
        {
            // Gönderilemedi (RabbitMQ kapalı, zaman aşımı, istemci vazgeçti...): fişi "Hata" yap ki
            // sonsuza dek "Bekliyor" kalan sahipsiz bir kayıt olmasın. İstemci iptal etmiş olsa bile (ct iptal)
            // fişi kapatmak için CancellationToken.None kullanırız.
            // (Kalan sınır: fişi yazdıktan sonra uygulama tam gönderimden önce ÇÖKERSE fiş yine "Bekliyor" kalır.
            //  Bunun gerçek çözümü "transactional outbox" desenidir.)
            await isler.HataAsync(kayit.Id, "Kuyruğa atılamadı.", CancellationToken.None);

            if (ex is OperationCanceledException) throw;   // istemci vazgeçti: olduğu gibi ilet
            throw new KuyrukHatasi("Soru kuyruğa alınamadı.", ex);
        }

        return kayit;
    }

    public Task<IsKaydi?> GetirAsync(string isId, CancellationToken ct = default)
        => isler.GetirAsync(isId, ct);
}
