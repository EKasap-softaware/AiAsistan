using System.Linq.Expressions;
using Asistan.Domain.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Asistan.Infrastructure.Mongo;

public static class MongoSinifHaritalari
{
    private static readonly object _kilit = new();
    private static bool _kayitli;

    // Domain'i MongoDB'ye bağlamadan (attribute kullanmadan) "Id string ama MongoDB'de ObjectId" kuralını koyar.
    public static void Kaydet()
    {
        lock (_kilit)
        {
            if (_kayitli) return;

            // C# özelliği "OturumId" → MongoDB alanı "oturumId" (MongoDB'de camelCase yaygındır).
            // Class map'lerden ÖNCE kaydedilmeli.
            ConventionRegistry.Register(
                "AsistanCamelCase",
                new ConventionPack { new CamelCaseElementNameConvention() },
                t => t.Namespace != null && t.Namespace.StartsWith("Asistan.Domain"));

            Haritala<Mesaj>(m => m.Id);
            Haritala<Hafiza>(h => h.Id);
            Haritala<OturumOzeti>(o => o.Id);

            _kayitli = true;
        }
    }

    private static void Haritala<T>(Expression<Func<T, string>> id)
    {
        BsonClassMap.RegisterClassMap<T>(cm =>
        {
            cm.AutoMap();
            cm.MapIdMember(id)
              .SetSerializer(new StringSerializer(BsonType.ObjectId))
              .SetIdGenerator(StringObjectIdGenerator.Instance);
        });
    }
}

// Uygulama açılırken index'leri oluşturur. CreateOne tekrar çağrılırsa zararsızdır (idempotent).
public class MongoIndexServisi(IMongoDatabase db, ILogger<MongoIndexServisi> log) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            // "Bu oturumun son mesajları" sorgusu için
            await db.GetCollection<Mesaj>(MongoMesajDeposu.KoleksiyonAdi).Indexes.CreateOneAsync(
                new CreateIndexModel<Mesaj>(
                    Builders<Mesaj>.IndexKeys.Ascending(m => m.OturumId).Descending(m => m.Olusturulma)),
                cancellationToken: ct);

            // "Bu kullanıcının hafızaları" sorgusu için
            await db.GetCollection<Hafiza>(MongoHafizaDeposu.KoleksiyonAdi).Indexes.CreateOneAsync(
                new CreateIndexModel<Hafiza>(
                    Builders<Hafiza>.IndexKeys.Ascending(h => h.KullaniciId).Descending(h => h.Olusturulma)),
                cancellationToken: ct);

            // Oturum başına tek özet: unique index, upsert'in çift kayıt üretmesini engeller
            await db.GetCollection<OturumOzeti>(MongoOturumOzetDeposu.KoleksiyonAdi).Indexes.CreateOneAsync(
                new CreateIndexModel<OturumOzeti>(
                    Builders<OturumOzeti>.IndexKeys.Ascending(o => o.OturumId),
                    new CreateIndexOptions { Unique = true }),
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            // MongoDB kapalıysa uygulamanın tamamı açılmasın istemiyoruz (kullanıcı API'si hâlâ çalışabilir).
            log.LogWarning(ex, "MongoDB index'leri oluşturulamadı.");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
