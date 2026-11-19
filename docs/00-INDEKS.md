# AI Asistan: Öğrenme Rehberi (içindekiler)

Bu klasör, projede **ne var, nerede duruyor, ne işe yarıyor ve neden öyle yaptık** sorularının cevabını tutar.
Hiçbir şeyi ezberlemen gerekmez: takıldığında buraya dön.

> Gizli değerler (şifre, API anahtarı) bu dosyalara bilerek yazılmadı. Onlar `.env` ve `user-secrets` içinde.

## Hangi soru için hangi dosya?

| Sorun | Dosya |
|---|---|
| "Şu dosya nedir, ne işe yarar?" | [01-proje-haritasi.md](01-proje-haritasi.md) |
| "DI, migration, async, Docker tam olarak ne?" | [02-kavramlar.md](02-kavramlar.md) |
| "Bir istek baştan sona nasıl ilerliyor? Hangi aşamada ne yaptık?" | [03-bir-istegin-yolculugu.md](03-bir-istegin-yolculugu.md) |
| "Hangi komutu yazmalıyım?" | [04-komut-rehberi.md](04-komut-rehberi.md) |
| "Hata aldım, ne yapayım?" | [05-sorun-giderme.md](05-sorun-giderme.md) |
| "Bu kelime ne demek?" | [06-sozluk.md](06-sozluk.md) |

## Önerilen okuma sırası (ilk kez)
1. Bu dosyanın altındaki "Büyük resim".
2. `03` → bir isteğin yolculuğu (hikâye gibi okunur).
3. `01` → dosya haritası (yanında Visual Studio'yu açıp bak).
4. `02` → kavramlar (takıldıkça).
5. `04`, `05`, `06` → ihtiyaç oldukça.

## Büyük resim

Bu proje, **bir AI asistan API'si**. Hedef mimari:

```
Kullanıcı ─► API ─► RabbitMQ kuyruğu ─► Worker ─► LiteLLM ─► Claude
                                          │
                                          ├─ MongoDB  (sohbet geçmişi, hafıza)
                                          └─ PostgreSQL (kullanıcılar, abonelikler)
```

Şu an (Aşama 9) çalışan kısım:

```
POST /api/sohbet ─► API ─► takip fişi (MongoDB "isler") + kuyruğa bırak ─► 202 + isId   (<0.3 sn, cevap beklemez)
                                  │
                           RabbitMQ "ai-istegi"
                                  │
                                Worker ─► bağlamı kur (MongoDB: hafıza, özet, mesajlar)
                                  │       ─► LiteLLM ─► Claude
                                  │       ─► soru + cevabı MongoDB'ye yaz, fişi "Tamamlandi" yap
                                  └─► RabbitMQ "sohbet-tamamlandi" ─► Worker ─► özetleme + kalıcı hafıza ("hizli" model)

GET /api/isler/{isId} ─► fişin durumu ve cevap       (kullanıcı cevabı buradan alır)
POST /api/kullanicilar ─► KullaniciServisi ─► PostgreSQL
```

LangFlow, CD, Skills ve MCP **henüz yok**: Aşama 10-11'de gelecek. (Kuyruk ve Worker Aşama 9'da eklendi.)

## Aşama durumu

| Aşama | Konu | Durum |
|---|---|---|
| 1 | Solution iskeleti | Bitti |
| 2 | Docker: Postgres, MongoDB, RabbitMQ | Bitti |
| 3 | EF Core + `/api/kullanicilar` | Bitti |
| 4 | Unit testler | Bitti (şu an 60 test) |
| 5 | CI (GitHub Actions) | Dosya hazır, Actions sekmesinden doğrulanmalı |
| 6 | LiteLLM + `/api/sohbet` | Bitti (Claude'dan gerçek cevap alındı) |
| 7 | MongoDB sohbet geçmişi | Bitti (18 test, gerçek MongoDB'de doğrulandı) |
| 8 | AI Memory | Bitti (kısa + uzun süreli hafıza, gerçek Claude ile doğrulandı) |
| 9 | RabbitMQ + Worker | Bitti (API 202 döner, Worker işler; Worker/model/RabbitMQ çökme senaryoları gerçek sistemde denendi) |
| 10 | LangFlow | Yok |
| 11 | CD + Skills + MCP | Yok |
