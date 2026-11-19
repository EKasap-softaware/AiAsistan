# Ders 4 · RabbitMQ (ayrıntılı)

Dosyalar: `src/Asistan.Application/Kuyruk/` (sözleşmeler, `IsServisi`), `src/Asistan.Infrastructure/Kuyruk/KuyrukAyarlari.cs`, `src/Asistan.Worker/` (`Program.cs`, `Consumers/`), `docker-compose.yml` (rabbitmq). Bu derste verilen sayılar ve topoloji bu projeden **gerçektir**. (Önceki konuşmadaki panel deneyindeki sayaçlar da burada.)

---

## 1. RabbitMQ nedir?

RabbitMQ bir **mesaj aracısıdır** (message broker). Bir program mesaj **bırakır**, başka bir program onu **sonra** alıp işler. İkisi birbirini beklemez ve birbirini tanımak zorunda kalmaz.

**Benzetme:** Restoranda garson siparişi **askıya asar**; aşçı kendi hızında askıdan fiş alır. Garson mutfağın bitmesini beklemez, bir sonraki müşteriye gider.

## 2. Neden gerekli?

Claude'un cevabı 10-60 saniye sürebilir. API bunu doğrudan beklese:
1. HTTP isteği zaman aşımına uğrar.
2. Aynı anda 1000 istek gelince sunucu ve LLM kotası boğulur.
3. LLM o an çökmüşse istek **kaybolur**.

Kuyrukla: API soruyu bırakıp **0.3 saniyede `202 Accepted`** döner. Worker kendi hızında işler. Worker çökse bile soru kuyrukta bekler. Daha çok yük için ikinci bir Worker başlatmak yeter.

## 3. Kavramlar ve projedeki karşılığı

| Kavram | Anlamı | Bizde |
|---|---|---|
| **Producer** | Mesajı bırakan | API (`MassTransitIsYayinci`) |
| **Queue** | Mesajların sırayla beklediği kuyruk | `ai-istegi`, `sohbet-tamamlandi`, `ai-istegi-hata`, `ai-istegi_error` |
| **Consumer** | Mesajı alıp işleyen | `AiIstegiConsumer` vb. (Worker) |
| **Exchange** | Producer'ın mesajı attığı yer; kurallara göre kuyruklara dağıtır | MassTransit'in kurduğu 8 exchange |
| **Binding** | Exchange ile kuyruk arasındaki kural | 8 binding |
| **Routing key** | Mesajın etiketi (dağıtımda kullanılır) | Fanout'ta kullanılmaz |
| **Ack** | Consumer'ın "işledim, silebilirsin" onayı | MassTransit hatasız bitince otomatik verir |
| **Dead-letter / error queue** | Defalarca başarısız olan mesajın gittiği kuyruk | `ai-istegi_error` |

**Exchange türleri:** `direct` (routing key birebir eşleşen kuyruğa), `fanout` (bağlı **tüm** kuyruklara kopyala), `topic` (desen eşleşmesi: `siparis.*.olusturuldu`), `headers` (başlığa göre, nadir). MassTransit hepsini **fanout** kurar; yönlendirme kuyruk adıyla yapılır.

## 4. Bu projenin gerçek topolojisi

`rabbitmqctl` ile RabbitMQ'ya sorduk (Worker kapalıyken bile kuyruklar durur, **durable**):

| Kuyruk | Durable | Ne için |
|---|---|---|
| `ai-istegi` | evet | API'nin bıraktığı sorular |
| `sohbet-tamamlandi` | evet | Cevap sonrası arka plan işi (özetleme/hafıza) |
| `ai-istegi-hata` | evet | `Fault` mesajlarını alan hata consumer'ı |
| `ai-istegi_error` | evet | Tüm denemeler tükenen mesajlar (silinmez, incelenir) |

**Exchange'ler** (hepsi `fanout`, `durable`): her mesaj türü için bir tane (`Asistan.Application.Kuyruk:AiIstegi`, `...:SohbetTamamlandi`), her kuyruk için aynı adlı bir tane (`ai-istegi`, `sohbet-tamamlandi`, `ai-istegi-hata`, `ai-istegi_error`) ve hata olayları için (`MassTransit:Fault`, `MassTransit:Fault--...AiIstegi--`).

**Bağlar (kaynak → hedef):** `Asistan...:AiIstegi → ai-istegi`, `Asistan...:SohbetTamamlandi → sohbet-tamamlandi`, `ai-istegi → ai-istegi` (ve diğer kuyruklar için aynısı), `MassTransit:Fault--...AiIstegi-- → ai-istegi-hata` ve `→ MassTransit:Fault`. Yani: mesaj türü exchange'i → kuyruğun exchange'i/kuyruğu; hata olayı → hata consumer'ının kuyruğu.

## 5. `Send` mi `Publish` mı? (en önemli tasarım kararı)

Panelde bunu bizzat gördün: `(AMQP default)` exchange'ine olmayan bir kuyruk adıyla mesaj atınca **"Message published, but not routed"**: mesaj RabbitMQ'ya girdi ama gidecek kuyruk olmadığı için **çöpe gitti**.

| | `Publish` | `Send` |
|---|---|---|
| Nereye | Bir **exchange**'e; ilgilenen kuyruklara dağılır | Doğrudan bir **kuyruğa** |
| Kuyruk yoksa | Mesaj **sessizce kaybolur** | Kuyruk **oluşturulur** |

API soruyu `Send` ile `queue:ai-istegi`'ne gönderir. **Worker hiç çalışmamış olsa bile soru kaybolmaz.** Gerçek deneyde Worker kapalıyken gönderdik: kuyrukta 1 mesaj, 0 tüketici; Worker açılınca ~5 sn'de cevap geldi.

## 6. Ack, Nack ve at-least-once (panel deneyinin sayıları)

`deneme` kuyruğuna 3 mesaj gönderip panelden aldık:

| Adım | Sunucu sayaçları | Anlamı |
|---|---|---|
| 3 mesaj gönderildi | `publish=3`, Ready 3 | |
| `Nack, requeue=true` ile 1 mesaj alındı | `get=1`, `ack=0`, **Ready 3** | Mesaj ödünç verildi, "işleyemedim, geri koy" denince kuyruğa döndü |
| `Automatic ack` ile 1 mesaj alındı | `get_no_ack=1`, **Ready 2**, `redeliver=1` | Onay otomatik: mesaj silindi. `redeliver=1`: geri koyduğumuz mesaj **tekrar teslim** edildi |

**Sonuç:** Consumer mesajı alıp **onay vermeden çökerse** RabbitMQ mesajı kuyruğa geri koyar: **soru kaybolmaz**. Bedeli: aynı mesaj **iki kez** gelebilir. Buna **at-least-once (en az bir kez)** teslimat denir; "tam bir kez" garantisi yoktur. Çözüm consumer'ı **idempotent** yazmaktır: aynı mesaj iki kez gelse sonuç değişmesin.

Bizde: `IslemeAlAsync` atomik olarak "bu iş hâlâ Bekliyor/Isleniyor mu?" der; `Tamamlandi` ise **atlar** (ve mesaj onaylanıp silinir). Bunu `WorkerConsumerTests`'te sınadık.

## 7. İş akışı: üç kuyruk, üç consumer

1. **`ai-istegi` → `AiIstegiConsumer`:** işi atomik al → `mod`'a göre `SohbetServisi` veya `DokumanServisi` → cevabı takip fişine yaz → (sohbet modunda) `SohbetTamamlandi` gönder.
2. **`sohbet-tamamlandi` → `SohbetTamamlandiConsumer`:** özetleme + kalıcı hafıza çıkarma (kullanıcı beklemez).
3. **`ai-istegi-hata` → `AiIstegiHataConsumer`:** tüm denemeler bitince fişi `Hata` yapar; gerçek neden sadece loga, kullanıcıya genel mesaj.

**Eşzamanlılık:** `ConcurrentMessageLimit = 4`: aynı anda en fazla 4 soru işlenir (LLM kotasını boğmamak için).

## 8. Yeniden deneme (retry) ve hata kuyruğu: gerçek ölçüm

Ayar (`Worker/Program.cs`): `Exponential(5, 1 sn, 1 dk, 5 sn)` ve `ArgumentException` **denenmez** (mesaj hatalıysa tekrar denemek boşuna).

LiteLLM'i durdurup soru gönderdik. Fişteki `denemeSayisi`:

| Deneme | Geçen süre | Denemeler arası |
|---|---|---|
| 1 | 0 sn | |
| 2 | ~11 sn | ~11 sn |
| 3 | ~28 sn | ~17 sn |
| 4 | ~51 sn | ~22 sn |
| 5 | ~85 sn | ~34 sn |
| 6 (son) | ~142 sn (~2.4 dk) | ~56 sn (üst sınır 1 dk'ya yaklaştı) |

Bekleme süresi **üstel artar** (üstel geri çekilme): çöken sunucuya saniyede yüzlerce istek atıp onu daha da boğma. 6. denemeden sonra: iş `Hata` oldu, asıl mesaj **`ai-istegi_error`'a taşındı** (silinmedi), `Fault` olayı hata consumer'ını tetikledi.

**Önemli kural:** Consumer hatayı **fırlatmalı**. Yutarsa MassTransit "başarılı" sanıp mesajı siler, yeniden deneme olmaz. (`AiIstegi_ModelHataVerirse_HatayiFirlatir...` testi bunu doğrular.)

## 9. Üç gerçek senaryo

| Senaryo | Sonuç |
|---|---|
| **Worker kapalı** | API yine 202 (0.25 sn). Soru kuyrukta bekledi. Worker açılınca cevap geldi |
| **LiteLLM kapalı** | 6 deneme, ~2.4 dk, `Hata`, mesaj `_error` kuyruğunda |
| **RabbitMQ kapalı** | **İlk kodda API 70+ sn asılı kaldı ve fiş `Bekliyor`'da sahipsiz kaldı** (kusur). Düzeltme: gönderime 5 sn zaman aşımı, fiş `Hata`, API **503**. RabbitMQ geri gelince API ve Worker kendiliğinden yeniden bağlandı |

**Ders:** Dış sisteme giden her çağrının zaman sınırı olmalı; bunu birim testler değil, sistemi bilerek bozmak buldu.

## 10. Hâlâ var olan sınırlar (bilerek)

- **Outbox yok:** Fişi yazmak ve kuyruğa göndermek **iki ayrı iş**; ikisi arasında uygulama çökerse fiş `Bekliyor` kalır. Gerçek çözüm "transactional outbox" desenidir.
- **Polling:** Kullanıcı cevabı kendisi sorar (`GET /api/isler/{isId}`); anlık itme için SignalR/webhook gerekir.
- **`_error` kuyruğunu yeniden işleme aracı yok;** panelden elle yapılır.
- Aynı oturuma aynı anda iki soru: sıra garantisi yok.

## 11. Yönetim ve komutlar

Panel: http://localhost:15672 (kullanıcı/şifre `.env`'de). **Sayaçlar birkaç saniye gecikmeli yenilenir**: "0" görürsen sayfayı yenile ya da sunucuya sor:
```powershell
docker compose exec rabbitmq rabbitmqctl list_queues name durable messages consumers
docker compose exec rabbitmq rabbitmqctl list_exchanges name type durable
docker compose exec rabbitmq rabbitmqctl list_bindings source_name destination_name routing_key
```
Daha çok yük için: ikinci Worker. Dikkat: **IDE'deki Worker ve container'daki Worker aynı kuyruğu paylaşır**, ikisi de soru alır; birini kapat.

## 12. Tuzaklar

| Belirti | Neden |
|---|---|
| Sorular `Bekliyor` kalıyor | Worker çalışmıyor (kuyrukta `tüketici 0`) |
| Mesaj "not routed" | `Publish` ile kuyruğu olmayan exchange'e atıldı; `Send` kullan |
| `PRECONDITION_FAILED` | Aynı adlı kuyruk farklı ayarlarla zaten var; kuyruğu silip yeniden başlat |
| Consumer hata fırlatmıyor, mesaj kayboluyor | Hata yutuldu |
| Aynı soru iki kez işlendi | Idempotency kapısı yok |
| Lisans | MassTransit **8.x** Apache-2.0 (ücretsiz; paket künyesinden doğruladık: 8.5.12); **9 ve sonrası ticari**. Alternatifler: Wolverine, Rebus |

## 13. Bilmen gerekenler

1. Kuyruk = sipariş askısı: üretici bırakır, tüketici kendi hızında alır. Kullanıcıyı beklemekten, sistemi yükten korur.
2. `Send` kuyruğa doğrudan gider ve kuyruğu oluşturur; `Publish` kuyruk yoksa mesajı kaybedebilir.
3. Ack gelmezse mesaj geri döner: kayıp yok, ama **iki kez gelebilir**: consumer idempotent olmalı.
4. Hatayı yutma: fırlat ki yeniden denensin. Yeniden deneme üstel artan beklemeyle yapılır.
5. Tüm denemeler bitince mesaj `_error` kuyruğunda saklanır; olay (`Fault`) ile fiş `Hata` yapılır.
6. Takip fişi ile kuyruk mesajı farklı şeylerdir: kuyruk iletişim, fiş "siparişim nerede?" cevabıdır.
7. Dış sisteme giden her çağrıya zaman sınırı koy ve sistemi bilerek bozup dene.
8. Panel sayaçları gecikmelidir; şüphede sunucuya (`rabbitmqctl`) sor.
