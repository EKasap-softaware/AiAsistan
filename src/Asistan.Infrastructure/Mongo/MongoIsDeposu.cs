using Asistan.Application.Kuyruk;
using Asistan.Domain.Entities;
using MongoDB.Driver;

namespace Asistan.Infrastructure.Mongo;

// İş takip fişlerinin MongoDB hali ("isler" koleksiyonu).
public class MongoIsDeposu(IMongoDatabase db) : IIsDeposu
{
    public const string KoleksiyonAdi = "isler";

    private readonly IMongoCollection<IsKaydi> _koleksiyon = db.GetCollection<IsKaydi>(KoleksiyonAdi);

    public Task OlusturAsync(IsKaydi kayit, CancellationToken ct = default)
        => _koleksiyon.InsertOneAsync(kayit, cancellationToken: ct);

    public async Task<IsKaydi?> GetirAsync(string isId, CancellationToken ct = default)
        => await _koleksiyon.Find(i => i.Id == isId).FirstOrDefaultAsync(ct);

    // ATOMİK "al": filtre ve güncelleme MongoDB'de TEK işlemdir. İki Worker aynı anda aynı işi almaya
    // çalışsa bile biri kazanır. İş Tamamlandi/Hata ise filtre eşleşmez, ModifiedCount 0 olur → false.
    public async Task<bool> IslemeAlAsync(string isId, CancellationToken ct = default)
    {
        var filtre = Builders<IsKaydi>.Filter.And(
            Builders<IsKaydi>.Filter.Eq(i => i.Id, isId),
            Builders<IsKaydi>.Filter.In(i => i.Durum, [IsDurumu.Bekliyor, IsDurumu.Isleniyor]));

        var guncelle = Builders<IsKaydi>.Update
            .Set(i => i.Durum, IsDurumu.Isleniyor)
            .Inc(i => i.DenemeSayisi, 1)
            .Set(i => i.Guncellenme, DateTime.UtcNow);

        var sonuc = await _koleksiyon.UpdateOneAsync(filtre, guncelle, cancellationToken: ct);
        return sonuc.ModifiedCount > 0;
    }

    public Task TamamlaAsync(string isId, string cevap, CancellationToken ct = default)
        => _koleksiyon.UpdateOneAsync(
            i => i.Id == isId,
            Builders<IsKaydi>.Update
                .Set(i => i.Durum, IsDurumu.Tamamlandi)
                .Set(i => i.Cevap, cevap)
                .Set(i => i.Hata, null)
                .Set(i => i.Guncellenme, DateTime.UtcNow),
            cancellationToken: ct);

    // Tamamlanmış bir işi sonradan "Hata"ya çevirmeyiz (filtrede Durum != Tamamlandi).
    public Task HataAsync(string isId, string hata, CancellationToken ct = default)
        => _koleksiyon.UpdateOneAsync(
            i => i.Id == isId && i.Durum != IsDurumu.Tamamlandi,
            Builders<IsKaydi>.Update
                .Set(i => i.Durum, IsDurumu.Hata)
                .Set(i => i.Hata, hata)
                .Set(i => i.Guncellenme, DateTime.UtcNow),
            cancellationToken: ct);
}
