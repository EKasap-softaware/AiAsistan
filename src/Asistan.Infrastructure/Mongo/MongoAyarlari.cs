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

    // Domain'i MongoDB'ye bağlamadan (attribute kullanmadan) "Id string ama MongoDB'de ObjectId" kuralını koyar.
    public static void Kaydet()
    {
        lock (_kilit)
        {
            if (BsonClassMap.IsClassMapRegistered(typeof(Mesaj))) return;

            // C# özelliği "OturumId" → MongoDB alanı "oturumId" (MongoDB'de camelCase yaygındır).
            // Class map'ten ÖNCE kaydedilmeli.
            ConventionRegistry.Register(
                "AsistanCamelCase",
                new ConventionPack { new CamelCaseElementNameConvention() },
                t => t.Namespace != null && t.Namespace.StartsWith("Asistan.Domain"));

            BsonClassMap.RegisterClassMap<Mesaj>(cm =>
            {
                cm.AutoMap();
                cm.MapIdMember(m => m.Id)
                  .SetSerializer(new StringSerializer(BsonType.ObjectId))
                  .SetIdGenerator(StringObjectIdGenerator.Instance);
            });
        }
    }
}

// Uygulama açılırken index'leri oluşturur. CreateOne tekrar çağrılırsa zararsızdır (idempotent).
public class MongoIndexServisi(IMongoDatabase db, ILogger<MongoIndexServisi> log) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            var koleksiyon = db.GetCollection<Mesaj>(MongoMesajDeposu.KoleksiyonAdi);
            var anahtar = Builders<Mesaj>.IndexKeys.Ascending(m => m.OturumId).Descending(m => m.Olusturulma);
            await koleksiyon.Indexes.CreateOneAsync(new CreateIndexModel<Mesaj>(anahtar), cancellationToken: ct);
        }
        catch (Exception ex)
        {
            // MongoDB kapalıysa uygulamanın tamamı açılmasın istemiyoruz (kullanıcı API'si hâlâ çalışabilir).
            log.LogWarning(ex, "MongoDB index'leri oluşturulamadı.");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
