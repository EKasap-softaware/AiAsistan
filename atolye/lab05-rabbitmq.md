# Lab 05: RabbitMQ

**Hedef:** Panelden (http://localhost:15672, kullanıcı/parola `.env`'de) kuyruk, exchange, dead-letter akışı kur; mesaj yayınla, tüket, reddet.

Zihinsel model: **producer** mesajı bir **exchange**'e yollar → exchange **binding** kurallarına göre **queue**'ya dağıtır → **consumer** alır ve **ack** der.
Mesaj kuyruğa yönlenmezse sessizce kaybolur ("Message published, but not routed", bunu daha önce yaşadın).
**Dead-letter:** reddedilen/süresi dolan mesaj çöpe gitmesin diye başka bir exchange'e aktarılır.

## Görevler

**G1. Dead-letter'lı kuyruk.** Panel > Queues > Add: Name `atolye.kuyruk`, Durable, Arguments: `x-dead-letter-exchange` = `atolye.olu` (String).
> Örnek (başka adlarla): kuyruk `ornek.kuyruk`, argüman `x-dead-letter-exchange` = `ornek.olu`.

**G2. Çöp exchange'i + kuyruğu + bağ.** Exchanges > Add: `atolye.olu`, tip **fanout**. Queues > Add: `atolye.olu-kuyruk`.
Exchange sayfası > Bindings > To queue: `atolye.olu-kuyruk`.
> Örnek: fanout = gelen mesajı bağlı TÜM kuyruklara kopyalar; direct = routing key birebir eşleşene; topic = desenle (`siparis.*`).

**G3. En az 3 mesaj yayınla.** Exchanges > `(AMQP default)` > Publish message: Routing key = `atolye.kuyruk`, Payload yaz. 3 kez.
> Örnek: default exchange her kuyruğa kendi adıyla bağlıdır; routing key = kuyruk adı olunca mesaj o kuyruğa gider. Yanlış ad yazarsan not routed olur.

**G4. Birini tüket ve ACK et.** Queues > `atolye.kuyruk` > Get messages: Ack Mode **Ack message requeue false**, Messages: 1 > Get Message(s).
> Örnek: ack = "aldım, işledim, kuyruktan sil". Ack gelmezse mesaj tekrar teslim edilir (at-least-once → işlemler **idempotent** olmalı).

**G5. Birini REDDET.** Aynı yerde Ack Mode **Reject requeue false**. Mesaj `atolye.olu` üzerinden `atolye.olu-kuyruk`'a düşmeli (kuyrukta 1 mesaj görürsün).
> Örnek: proje akışı = retry (üstel bekleme) bitince mesaj `_error` kuyruğuna gider; aynı mantık.

Doğrulayıcı yönetim API'sini okur. Panel sayaçları 5-10 sn gecikir; kırmızıysa biraz bekle, tekrar çalıştır.

## Sık hatalar
- Argüman tipi: `x-dead-letter-exchange` **String** olmalı.
- Kuyruk argümanı sonradan değişmez: yanlışsa kuyruğu sil, yeniden oluştur.
- `Message published, but not routed`: exchange'in o routing key için binding'i yok.

## Mülakat soruları
1. Exchange tipleri (direct, fanout, topic, headers) farkı?
2. At-most-once / at-least-once / exactly-once? Neden idempotency?
3. Dead-letter queue nedir, ne zaman kullanılır? Poison message nedir?
4. Send ile Publish farkı (MassTransit)? Neden queue:ai-istegi'ne Send?
5. RabbitMQ ile Kafka farkı (kısaca)?
