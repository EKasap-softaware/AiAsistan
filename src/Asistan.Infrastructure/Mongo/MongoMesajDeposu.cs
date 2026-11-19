using Asistan.Application.Sohbet;
using Asistan.Domain.Entities;
using MongoDB.Driver;

namespace Asistan.Infrastructure.Mongo;

public class MongoMesajDeposu(IMongoDatabase db) : IMesajDeposu
{
    public const string KoleksiyonAdi = "mesajlar";

    private readonly IMongoCollection<Mesaj> _koleksiyon = db.GetCollection<Mesaj>(KoleksiyonAdi);

    public Task KaydetAsync(IEnumerable<Mesaj> mesajlar, CancellationToken ct = default)
        => _koleksiyon.InsertManyAsync(mesajlar, cancellationToken: ct);

    public async Task<IReadOnlyList<Mesaj>> SonMesajlarAsync(string oturumId, int adet, CancellationToken ct = default)
    {
        // Aynı milisaniyede yazılan soru ve cevabın sırası şaşmasın diye ikinci sıralama anahtarı: Id
        // (ObjectId'ler yazılma sırasına göre artar).
        var liste = await _koleksiyon.Find(m => m.OturumId == oturumId)
            .SortByDescending(m => m.Olusturulma)
            .ThenByDescending(m => m.Id)
            .Limit(adet)
            .ToListAsync(ct);

        liste.Reverse(); // eskiden yeniye
        return liste;
    }
}
