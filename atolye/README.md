# Atölye: yaparak öğren (10 seviye)

Burası, projeyi **okuyarak değil, kendi ellerinle yaparak** öğrenmen için. 10 lab var, her biri bir seviye.
Bir seviyeyi geçince bir sonraki **otomatik açılır**; kilitli lab'a atlayamazsın.

## Öğretim sayfası (önerilen yol, hiçbir şey kurmadan)

`atolye/baslat.cmd` dosyasına çift tıkla (ya da `python atolye/sunucu.py`), tarayıcıda **http://localhost:5300** açılır.
Her seviyede: şema, anlatım, **örnek**, **görev** ve cevabını yazdığın **editör**. "Kontrol et" cevabın yapısını anında denetler
(✔/✘ + ipucu); 2 başarısız denemeden sonra çözüm gösterilir. Tüm adımları geçince sonraki seviye açılır.
48 alıştırma + 51 mülakat sorusu; ilerleme tarayıcıda (localStorage) tutulur. Kurulum, Docker, veritabanı gerekmez.

Sayfa cevabın **yapısını** denetler (doğru komut, doğru kod parçaları); C#/Docker/Mongo'yu gerçekten çalıştırmaz.
Gerçek projede çalıştırarak doğrulamak istersen aşağıdaki komut satırı doğrulayıcısı (isteğe bağlı) bunu yapar.

## Komut satırından oynamak istersen

```powershell
cd AiAsistan
./atolye/atolye.ps1            # o anki seviyeyi doğrula; geçersen sonrakine atlarsın
```

| Komut | Ne yapar |
|---|---|
| `./atolye/atolye.ps1` | Şu anki leveli kontrol eder (✔/✘ + ipucu). Hepsi ✔ ise seviye atlar |
| `./atolye/atolye.ps1 durum` | İlerleme çubuğu: ✔ bitti, ▶ sıradaki, 🔒 kilitli |
| `./atolye/atolye.ps1 oku` | Şu anki labın görevlerini ve ÖRNEKLERİ ekrana basar |
| `./atolye/atolye.ps1 cozum` | Referans çözümün klasörünü gösterir (önce kendin dene) |
| `./atolye/atolye.ps1 sifirla` | İlerlemeyi sıfırlar |

## Nasıl çalışmalı
1. `oku` ile görevleri oku. **Her görevin yanında benzer bir örnek var**: örneği kendi işine uyarla.
2. Kodu/komutu SEN yaz. Sonra `./atolye/atolye.ps1`. Kırmızı satırın altındaki **ipucu** ne eksik olduğunu söyler.
3. Geçince seviye atlarsın. Labın sonundaki **mülakat sorularını** yüksek sesle cevapla.
4. Lab 01 için ayrı dal aç: `git checkout -b lab-01-etiket` (ana dal temiz kalsın).

## Seviyeler
| Seviye | Konu | Ne yaparsın |
|---|---|---|
| 01 | .NET + EF Core + Solution + Unit Test | Projeye `Etiket` tablosu: entity, servis, DbContext, migration, test |
| 02 | Git / GitHub | Dal, `--no-ff` merge, çakışma çözme, revert, .gitignore, tag (`atolye/kum/lab02`) |
| 03 | Docker | Dockerfile, root olmayan kullanıcı, .dockerignore, compose (`atolye/kum/lab03`) |
| 04 | MongoDB | Gerçek Mongo'da belge, index, TTL, aggregation, atomik güncelleme |
| 05 | RabbitMQ | Kuyruk, exchange, dead-letter, yayınla, ack, reject (panelden) |
| 06 | LiteLLM | `litellm/config.yaml`'a kendi model takma adın, sınırlar, gerçek istek |
| 07 | AI Memory | Sliding window, özet eşiği, hafıza filtresi: TDD ile (gizli testler) |
| 08 | LangFlow | Kendi akışını kur, JSON export et, sır sızıntısı yok |
| 09 | CI/CD | `.github/workflows/atolye-ci.yml` sıfırdan |
| 10 | Claude CLI / Skills / MCP | Skill, slash komutu, CLAUDE.md kuralı, MCP sunucusu |

## Ön koşullar
- Docker Desktop açık, `docker compose up -d` ile servisler ayakta (Lab 03-06, 08).
- `.env` dosyası dolu (`.env.example`'dan). Doğrulayıcı sırları SADECE okur, ekrana yazmaz.
- **Visual Studio'da API/Worker çalışıyorsa DURDUR** (Lab 01 ve 07 build ederken DLL kilitlenir).

## Dosya düzeni
- `labNN-*.md`: görevler + örnekler + mülakat soruları
- `labs/LabNN.ps1`: doğrulayıcılar (nasıl kontrol edildiğini merak edersen oku)
- `baslangic/`: başlangıç dosyaları (otomatik `kum/` altına kopyalanır)
- `kum/`: deneme alanın (git'e girmez) · `.ilerleme.json`: kişisel ilerleme (git'e girmez)
- `cozumler/`: referans çözümler (`.txt`, derlemeye girmesin diye) · `gizli/`: Lab 07'nin gizli testleri

## Sınırlar (dürüst not)
- Lab 09 sadece YAML'ın doğruluğunu denetler; gerçek çalıştırma için GitHub'a push gerekir.
- Lab 08'in kontrolü dışa aktarılan JSON üzerinedir (akışı çalıştırmayı denetlemez).
- Lab 06 G5 gerçek bir (çok ucuz, ~0.00001 $) Claude isteği atar.
