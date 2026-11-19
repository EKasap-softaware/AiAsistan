using Asistan.Application.Bellek;
using Asistan.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Asistan.Infrastructure.Mongo;

public class MongoHafizaDeposu(IMongoDatabase db) : IHafizaDeposu
{
    public const string KoleksiyonAdi = "hafizalar";

    private readonly IMongoCollection<Hafiza> _koleksiyon = db.GetCollection<Hafiza>(KoleksiyonAdi);

    public Task EkleAsync(Hafiza hafiza, CancellationToken ct = default)
        => _koleksiyon.InsertOneAsync(hafiza, cancellationToken: ct);

    public async Task<IReadOnlyList<Hafiza>> KullaniciHafizalariAsync(string kullaniciId, int adet, CancellationToken ct = default)
        => await _koleksiyon.Find(h => h.KullaniciId == kullaniciId)
            .SortByDescending(h => h.Olusturulma)
            .ThenByDescending(h => h.Id)
            .Limit(adet)
            .ToListAsync(ct);

    public async Task<bool> SilAsync(string kullaniciId, string hafizaId, CancellationToken ct = default)
    {
        // Geçersiz bir id gelirse serileştirme hatası yerine "bulunamadı" deriz.
        if (!ObjectId.TryParse(hafizaId, out _)) return false;

        // kullaniciId de filtrede: bir kullanıcı başkasının hafızasını silemesin.
        var sonuc = await _koleksiyon.DeleteOneAsync(h => h.Id == hafizaId && h.KullaniciId == kullaniciId, ct);
        return sonuc.DeletedCount > 0;
    }

    public async Task<long> TumunuSilAsync(string kullaniciId, CancellationToken ct = default)
        => (await _koleksiyon.DeleteManyAsync(h => h.KullaniciId == kullaniciId, ct)).DeletedCount;
}
