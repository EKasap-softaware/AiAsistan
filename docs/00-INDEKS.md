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

Şu an (Aşama 6) çalışan kısım:

```
Kullanıcı ─► API ─► SohbetServisi ─► LiteLLM ─► Claude        (senkron, kuyruk yok)
             └────► KullaniciServisi ─► PostgreSQL
```

Kuyruk, Worker, MongoDB ve hafıza **henüz yok**: Aşama 7-11'de gelecek.

## Aşama durumu

| Aşama | Konu | Durum |
|---|---|---|
| 1 | Solution iskeleti | Bitti |
| 2 | Docker: Postgres, MongoDB, RabbitMQ | Bitti |
| 3 | EF Core + `/api/kullanicilar` | Bitti |
| 4 | Unit testler | Bitti (13 test) |
| 5 | CI (GitHub Actions) | Dosya hazır, Actions sekmesinden doğrulanmalı |
| 6 | LiteLLM + `/api/sohbet` | Bitti (Claude'dan gerçek cevap alındı) |
| 7 | MongoDB sohbet geçmişi | Yok |
| 8 | AI Memory | Yok |
| 9 | RabbitMQ + Worker | Yok |
| 10 | LangFlow | Yok |
| 11 | CD + Skills + MCP | Yok |
