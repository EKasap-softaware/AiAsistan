using Asistan.Application.Bellek;
using Asistan.Domain.Entities;
using MongoDB.Driver;

namespace Asistan.Infrastructure.Mongo;

public class MongoOturumOzetDeposu(IMongoDatabase db) : IOturumOzetDeposu
{
    public const string KoleksiyonAdi = "oturumOzetleri";

    private readonly IMongoCollection<OturumOzeti> _koleksiyon = db.GetCollection<OturumOzeti>(KoleksiyonAdi);

    public async Task<OturumOzeti?> GetirAsync(string oturumId, CancellationToken ct = default)
        => await _koleksiyon.Find(o => o.OturumId == oturumId).FirstOrDefaultAsync(ct);

    // Upsert: kayıt varsa günceller, yoksa oluşturur. (Belgeyi komple değiştirmek yerine alan alan
    // güncelliyoruz; böylece yeni kayıtta _id'yi MongoDB üretir.)
    public Task KaydetAsync(string oturumId, string ozet, int ozetlenenMesajSayisi, CancellationToken ct = default)
        => _koleksiyon.UpdateOneAsync(
            o => o.OturumId == oturumId,
            Builders<OturumOzeti>.Update
                .Set(o => o.Ozet, ozet)
                .Set(o => o.OzetlenenMesajSayisi, ozetlenenMesajSayisi)
                .Set(o => o.Guncellenme, DateTime.UtcNow),
            new UpdateOptions { IsUpsert = true },
            ct);
}
