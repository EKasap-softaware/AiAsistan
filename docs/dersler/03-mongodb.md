# Ders 3 · MongoDB (ayrıntılı)

Dosyalar: `src/Asistan.Infrastructure/Mongo/` (`MongoMesajDeposu.cs`, `MongoHafizaDeposu.cs`, `MongoOturumOzetDeposu.cs`, `MongoIsDeposu.cs`, `MongoAyarlari.cs`), `src/Asistan.Domain/Entities/` (`Mesaj`, `Hafiza`, `OturumOzeti`, `IsKaydi`), `docker-compose.yml` (mongo servisi). Sayılar bu projede **ölçülmüştür**.

---

## 1. MongoDB nedir?

MongoDB bir **belge (document) veritabanıdır**: veriyi tablo satırları yerine JSON benzeri **belgeler** (BSON) olarak saklar. Şema esnektir: aynı koleksiyondaki iki belge farklı alanlara sahip olabilir.

| SQL (PostgreSQL / EF Core) | MongoDB |
|---|---|
| Veritabanı | Veritabanı |
| Tablo | **Koleksiyon** (collection) |
| Satır | **Belge** (document) |
| Kolon | **Alan** (field) |
| Primary key | `_id` (otomatik `ObjectId`) |
| JOIN | İç içe belge (embed) veya `$lookup` |
| Sabit şema + migration | Esnek şema |

**ObjectId** MongoDB'nin ürettiği 12 baytlık benzersiz kimliktir, **zamana göre artar**. Bu yüzden "aynı milisaniyedeki iki belgenin sırası" gibi durumlarda ikinci bir sıralama anahtarı (`_id`) işe yarar (bizde `ThenByDescending(m => m.Id)`).

## 2. Neden iki veritabanı (PostgreSQL + MongoDB)?

| Veri | Nerede | Neden |
|---|---|---|
| Kullanıcı, abonelik | PostgreSQL | İlişkili, tutarlılığı kritik (aynı e-posta iki kez olmasın), şeması sabit |
| Sohbet mesajları, hafıza, özetler, iş fişleri | MongoDB | Çok sayıda, yapısı değişebilir, genelde tek seferde okunur |

**Embed mi referans mı?** Birlikte okunan ve **sınırlı** veriyi iç içe koy (bir siparişin satırları). **Sınırsız büyüyen** veriyi ayrı koleksiyona koy ve kimlikle bağla. Bir sohbetin mesajı sınırsız büyür, o yüzden `mesajlar` ayrı bir koleksiyon (her mesaj ayrı belge, `oturumId` ile bağlı). Bir belge en fazla **16 MB** olabilir.

## 3. Projedeki 4 koleksiyon

| Koleksiyon | Ne tutar | Bağlantı anahtarı | Örnek alanlar |
|---|---|---|---|
| `mesajlar` | Her soru ve cevap | `oturumId`, `kullaniciId` | `rol`, `icerik`, `olusturulma` |
| `oturumOzetleri` | Oturumun eski kısmının özeti | `oturumId` (tek) | `ozet`, `ozetlenenMesajSayisi` |
| `hafizalar` | Kullanıcı hakkında kalıcı bilgi | `kullaniciId` | `icerik` |
| `isler` | Sorunun takip fişi | `_id` = `isId` | `durum`, `cevap`, `denemeSayisi`, `mod` |

Gerçek bir `mesajlar` belgesi (deney verisi):
```json
{ "_id": "6afef5c3735e8e5654ea211f", "oturumId": "oturum-5", "kullaniciId": "k5",
  "rol": "user", "icerik": "ornek mesaj 0", "olusturulma": "2026-11-19T10:00:00.000Z" }
```

## 4. .NET'ten kullanım: sürücü ve Clean Architecture

- **`MongoClient` Singleton** (içinde bağlantı havuzu tutar, thread-safe). `IMongoDatabase` Singleton. Depolar `Scoped`.
- **Domain MongoDB'yi bilmez.** `Mesaj.Id` düz bir `string`. "Bu alan MongoDB'de ObjectId olarak saklansın, alan adları camelCase olsun, enum yazı olsun" kuralları **Infrastructure'daki `MongoSinifHaritalari`'nda** (class map + convention) tanımlıdır. Domain'e MongoDB attribute'ü koysaydık, katman kuralı bozulurdu.
- **Alan adı büyük/küçük harf duyarlıdır.** İlk denemede C# özellik adı aynen yazıldı: `OturumId` (büyük O). Ama sorgu `oturumId` arıyordu: `countDocuments` **0** döndü, kayıtlar duruyordu. `CamelCaseElementNameConvention` ile düzeldi. **Ders:** alan adı biçimini baştan sabitle; sonradan değiştirirsen eski belgeler eşleşmez.
- **Enum'lar yazı olarak** saklanır (`durum: "Tamamlandi"`): okunur ve sorgulanır.

## 5. Sorgular ve işlemler (kod → ne yapar)

| Kodda | Anlamı |
|---|---|
| `Find(m => m.OturumId == id)` | `find({oturumId: ...})` |
| `.SortByDescending(...).ThenByDescending(m => m.Id).Limit(n)` | Sırala (aynı milisaniyede ObjectId ile), n tane al |
| `.Skip(atla).Limit(adet)` | `AralikGetirAsync`: ilk `atla` belgeyi geç (özetleme aralığı) |
| `CountDocumentsAsync(...)` | Mesaj sayısı |
| `InsertManyAsync(...)` | Soru ve cevabı **tek istekte** yazar (ama **transaction değil**) |
| `UpdateOneAsync(filtre, güncelle)` + `ModifiedCount` | **Atomik** "al": aşağıda |
| `UpdateOne(..., IsUpsert = true)` | Varsa güncelle, yoksa oluştur (özet kaydı) |

**Atomik güncelleme (`IslemeAlAsync`):** filtre ("bu `_id` VE durum `Bekliyor` ya da `Isleniyor`") ile güncelleme ("durum `Isleniyor`, `denemeSayisi` + 1") **MongoDB'de tek işlemdir**. İki Worker aynı işi aynı anda almaya çalışsa biri kazanır; kaybeden `ModifiedCount = 0` görür. Önce okuyup sonra yazsaydık (iki ayrı işlem) ikisi de aynı işi alabilirdi. Bu, RabbitMQ'daki "aynı mesaj iki kez gelebilir" sorununa karşı **idempotency kapısı**dır.

**Upsert neden alan alan?** Belgeyi komple değiştirseydik yeni kayıtta `_id` boş (null) gider; alan alan güncelleyince yeni kayıtta `_id`'yi MongoDB üretir.

## 6. Index: gerçek ölçüm

Index, MongoDB'nin tüm koleksiyonu taramak yerine hedefe doğrudan gitmesini sağlar. Geçici bir koleksiyona **30 000 mesaj** (300 oturum × 100 mesaj) koyup aynı sorguyu çalıştırdık: *"oturum-150'nin son 10 mesajı"*.

| | Plan | İncelenen belge | Süre |
|---|---|---|---|
| **Index yok** | `SORT > COLLSCAN` | **30 000** | 22 ms |
| **Index var** `{oturumId:1, olusturulma:-1}` | `LIMIT > FETCH > IXSCAN` | **10** | 3 ms |

- İndexsiz: tüm koleksiyon taranır ve sonra sıralanır. İndexli: indeks zaten sıralı, ilk 10 kayıt okunur.
- **Maliyeti:** index'in boyutu **536 KB**, koleksiyon verisi 3 839 KB (index veriye yaklaşık %14 ek yer). Index her yazmada da güncellenir: gereksiz index yazmayı yavaşlatır.
- **Alan sırası önemli:** `{oturumId, olusturulma}` index'i `oturumId` ile başlayan sorgulara yarar. **`kullaniciId`'ye göre** sorgu bu index'e uymaz: plan yine `COLLSCAN`. (Soruda hangi alanla filtreleyeceksen index o alanla başlamalı.)

Projedeki gerçek index'ler:

| Koleksiyon | Index | Ne için |
|---|---|---|
| `mesajlar` | `{oturumId:1, olusturulma:-1}` | Bir oturumun son mesajları |
| `hafizalar` | `{kullaniciId:1, olusturulma:-1}` | Bir kullanıcının hafızaları |
| `oturumOzetleri` | `{oturumId:1}` **unique** | Oturum başına tek özet (upsert çift kayıt üretmesin) |
| `isler` | `{olusturulma:1}` **TTL 7 gün** | Takip fişleri 7 gün sonra kendiliğinden silinir |

**TTL index:** Belirli süre sonra belgeleri otomatik siler (koleksiyon sonsuza dek şişmesin). MongoDB silmeyi arka planda **yaklaşık dakikada bir** kontrol eder, yani süre dolunca silme anlık olmaz.

Index'leri uygulama açılırken `MongoIndexServisi` (`IHostedService`) kurar; `CreateIndex` tekrar çağrılırsa zararsızdır. MongoDB kapalıysa uygulamayı düşürmez, uyarı loglar.

## 7. Plan nasıl okunur? (`explain`)

```javascript
db.mesajlar.find({ oturumId: "oturum-150" }).sort({ olusturulma: -1 }).limit(10).explain("executionStats")
```
Bakılacak alanlar: **plan aşamaları** (`COLLSCAN` kötü işaret, `IXSCAN` iyi), `totalDocsExamined` (kaç belge okundu), `nReturned` (kaç döndü). İncelenen/döndürülen oranı büyükse index eksiktir.

## 8. Aggregation pipeline

SQL'deki `GROUP BY`'ın karşılığıdır; veri aşama aşama işlenir. Stage 7 testinde oturum başına mesaj sayısını şöyle aldık:
```javascript
db.mesajlar.aggregate([{ $group: { _id: "$oturumId", adet: { $sum: 1 }, kullanici: { $first: "$kullaniciId" } } }])
```

## 9. Yetki ve güvenlik

- Docker'da root kullanıcı `.env`'den (`MONGO_USER`/`MONGO_PASSWORD`) gelir; **bağlantı cümlesinde `?authSource=admin`** şarttır (kullanıcı `admin` veritabanında tanımlı).
- **Salt-okunur kullanıcı:** MCP için `mcp_okuyucu` yarattık (sadece `asistan` veritabanında `read` rolü). **Kanıt:** bu kullanıcıyla `insertOne` denedik, MongoDB **`Unauthorized`** döndü. Kural: her araca/uygulamaya **gereken en az yetkiyi** ver.

## 10. Komutlar (`mongosh`)

```powershell
docker compose exec mongo mongosh -u asistan -p <MONGO_PASSWORD> --authenticationDatabase admin asistan
```
```javascript
db.mesajlar.countDocuments({})
db.mesajlar.find({ oturumId: "<ID>" }).sort({ olusturulma: 1, _id: 1 })
db.hafizalar.find({ kullaniciId: "1" })
db.isler.find().sort({ olusturulma: -1 }).limit(5)
db.mesajlar.getIndexes()
db.mesajlar.drop()          // DİKKAT: koleksiyonu siler
```
Grafik arayüz: **MongoDB Compass** (ücretsiz), `mongodb://localhost:27017` + kullanıcı/şifre + `authSource=admin`.

## 11. Tuzaklar

| Tuzak | Çözüm |
|---|---|
| Alan adı büyük/küçük harf (`OturumId` ≠ `oturumId`) | Convention ile sabitle |
| `InsertMany` transaction sanılır | Değildir: yarıda kalırsa bir kısmı yazılmış olabilir |
| Index eksik, sorgu yavaş | `explain` ile `COLLSCAN` ara |
| Gereksiz index | Yazmayı yavaşlatır, yer kaplar |
| `docker compose down -v` | Tüm veri silinir |
| Alan şeması "esnek" diye düzensiz bırakılır | Domain sınıfı şemayı kodda sabitler; sürücü tanımadığı alanda hata verir |

## 12. Bilmen gerekenler

1. MongoDB = belge veritabanı; koleksiyon/belge/alan. `_id` = ObjectId (zamana göre artar).
2. SQL'e karşı: esnek ve çok veri için MongoDB, ilişkili ve kritik veri için SQL. İkisi birlikte kullanılabilir.
3. Sınırsız büyüyen veriyi ayrı koleksiyonda tut (embed etme); belge sınırı 16 MB.
4. Alan adları büyük/küçük harf duyarlıdır: biçimi baştan sabitle.
5. Domain'i MongoDB'den temiz tut: ObjectId/camelCase/enum kurallarını Infrastructure'da (class map) ver.
6. Index'siz sorgu tüm koleksiyonu tarar (ölçtük: 30 000 belge vs 10). İlk alan sorgunun filtresiyle başlamalı.
7. Atomik güncelleme (filtre+güncelleme tek işlem) yarışı önler; `InsertMany` transaction değildir.
8. TTL index eski kayıtları kendiliğinden siler (silme dakika civarı gecikmeli).
9. Her kullanıcıya gereken en az yetkiyi ver (salt-okunur kullanıcı yazamadı).
