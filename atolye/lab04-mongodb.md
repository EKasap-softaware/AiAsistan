# Lab 04: MongoDB

**Hedef:** Gerçek MongoDB'de (projenin konteyneri) belge ekle, index kur, TTL, aggregation ve atomik güncelleme yap.
**Çalışma yeri:** veritabanı `atolye`, koleksiyon `siparisler`. Veritabanını kendi değerlerin (`.env`'deki kullanıcı) ile açarsın:

```powershell
docker compose exec mongo mongosh -u <MONGO_USER> -p <MONGO_PASSWORD> --authenticationDatabase admin
```
(Kullanıcı/parola `.env` dosyanda. Bu satırı sohbete/commit'e yazma.) Açılınca: `use atolye`.

Zihinsel model: tablo = **koleksiyon**, satır = **belge** (JSON benzeri), şema zorunlu değil, alan adları **büyük/küçük harfe duyarlı**.

## Görevler

**G1. En az 5 sipariş ekle** (`musteri` metin, `tutar` sayı, `durum` metin).
> Örnek (başka koleksiyon, sen `siparisler`'e yazacaksın):
> ```js
> db.urunler.insertMany([
>   { ad: "kalem", fiyat: 5,  stok: 100 },
>   { ad: "defter", fiyat: 20, stok: 40 }
> ])
> db.urunler.find({ fiyat: { $gt: 10 } })      // sorgu
> ```

**G2. `musteri` üzerinde index.** Sorguyu hızlandırır (proje dersinde 30.000 belgede COLLSCAN 22 ms, IXSCAN 3 ms ölçtük).
> Örnek: `db.urunler.createIndex({ ad: 1 })` · kontrol: `db.urunler.getIndexes()` · plan: `db.urunler.find({ad:"kalem"}).explain("executionStats")` (IXSCAN mı COLLSCAN mı?)

**G3. Bileşik index `{ durum: 1, tutar: -1 }`.** Sıra önemli: önce eşitlik alanı, sonra sıralama alanı.
> Örnek: `db.urunler.createIndex({ stok: 1, fiyat: -1 })`

**G4. TTL index.** Belgeye `olusturulma: new Date()` alanı koy (en az birine), index'i 3600 sn ile kur.
> Örnek: `db.urunler.createIndex({ eklenme: 1 }, { expireAfterSeconds: 3600 })` — Mongo arka planda her ~60 sn eski belgeleri siler (oturum/geçici veri için).

**G5. Aggregation + `$out`.** Müşteri başına toplam tutar, sonuç `musteri_toplam` koleksiyonuna.
> Örnek:
> ```js
> db.urunler.aggregate([
>   { $group: { _id: "$stok", toplamFiyat: { $sum: "$fiyat" } } },
>   { $out: "stok_ozet" }
> ])
> ```
> Pipeline = bir boru hattı: her aşama bir öncekinin çıktısını işler (`$match` → `$group` → `$sort`...).

**G6. Atomik güncelleme.** Bir siparişi `durum: "tamamlandi"` yap + `guncellenme: new Date()` ekle.
> Örnek: `db.urunler.updateOne({ ad: "kalem" }, { $set: { stok: 99, guncellenme: new Date() } })`
> Neden önemli: iki işçi aynı belgeye aynı anda yazsa bile `updateOne` tek atomik işlem (projede `IslemeAlAsync` böyle).

## Sık hatalar
- `Unauthorized`: `--authenticationDatabase admin` unutuldu.
- Alan adı `Musteri` ≠ `musteri`.
- TTL çalışmıyor: alan `Date` tipi olmalı (metin değil).

## Mülakat soruları
1. SQL mi MongoDB mi? Ne zaman hangisi? (esnek şema, iç içe belge vs ilişkiler/transaction)
2. Index maliyeti nedir (yazma yavaşlar, bellek)? `explain` ile ne görürsün?
3. TTL index neye yarar? Embed mi reference mi (belge içine gömmek vs ayrı koleksiyon)?
4. `updateOne` vs oku-değiştir-yaz; neden atomik işlem?
5. Replica set / sharding nedir (kısaca)?
