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

    public async Task<int> SayAsync(string oturumId, CancellationToken ct = default)
        => (int)await _koleksiyon.CountDocumentsAsync(m => m.OturumId == oturumId, cancellationToken: ct);

    public async Task<IReadOnlyList<Mesaj>> AralikGetirAsync(string oturumId, int atla, int adet, CancellationToken ct = default)
    {
        if (adet <= 0) return [];

        return await _koleksiyon.Find(m => m.OturumId == oturumId)
            .SortBy(m => m.Olusturulma)
            .ThenBy(m => m.Id)
            .Skip(atla)
            .Limit(adet)
            .ToListAsync(ct);
    }
}
