# 05 · Sorun giderme

Önce genel yöntem, sonra **bu projede gerçekten yaşadığımız** hatalar.

## Genel yöntem (3 adım)
1. **Hata mesajını oku.** Çoğu zaman cevabı içinde yazar. Son satırdan değil, `error` geçen satırdan başla.
2. **Hangi katmanda?** Derleme hatası mı (kod), çalışma hatası mı (API), altyapı mı (Docker/LiteLLM)?
3. **Zinciri kısalt.** API → LiteLLM → Anthropic zincirinde, LiteLLM'e doğrudan istek atıp sorunun hangi halkada olduğunu bul.

```
Sorun hangi halkada?
  Derleme   ──► dotnet build
  Testler   ──► dotnet test
  Docker    ──► docker compose ps  /  docker compose logs <servis>
  LiteLLM   ──► LiteLLM'e doğrudan istek (bkz. 04-komut-rehberi.md)
  API       ──► Scalar (/scalar/v1) veya Asistan.Api.http
```

---

## Yaşadığımız hatalar

### 1. `Run "dotnet tool restore" to make the "dotnet-ef" command available`
**Neden:** `dotnet-ef` aracı bu bilgisayarda global kurulu değildi.
**Çözüm:** Projeye özel olarak kuruldu (`.config/dotnet-tools.json`). Yeni bir yerde: `dotnet tool restore`.

### 2. `Package 'Scalar.AspNetCore' is incompatible with 'all' frameworks`
**Neden:** En yeni Scalar sürümü .NET 11 istiyor, biz .NET 9'dayız.
**Çözüm:** Eski sürüm sabitlendi: `Scalar.AspNetCore` **2.12.\***.
**Ders:** Paket sürümleri projenin .NET sürümüyle uyumlu olmalı. `*` kullanırsan dikkat, yeni sürüm gelince kırılabilir.

### 3. `'IServiceCollection' does not contain a definition for 'AddHttpClient'`
**Neden:** `AddHttpClient` `Microsoft.Extensions.Http` paketindedir. ASP.NET projeleri bunu hazır getirir, **class library projeleri getirmez**.
**Çözüm:** Paket Infrastructure'a eklendi.

### 4. `Missing required parameter: 'messages'` (curl ile JSON gönderirken)
**Neden:** Git Bash/Windows'ta komut satırına yazılan JSON'daki tırnaklar ve Türkçe karakterler bozulabiliyor.
**Çözüm:** JSON'u dosyaya yazıp `-d @dosya.json` ile gönder, veya PowerShell'de `Invoke-RestMethod` kullan. Kolayı: Scalar / `.http` dosyası.

### 5. `.env` değişti ama LiteLLM yeni değeri görmüyor (`Missing Anthropic API Key`)
**Neden 1:** Değer dosyaya gerçekten yazılmamıştı (editörde kaydedilmemiş ya da başka dosya düzenlenmiş). Kontrol (değeri göstermeden):
```powershell
(Select-String -Path .env -Pattern '^ANTHROPIC_API_KEY=(.*)$').Matches[0].Groups[1].Value.Length
```
0 ise değer boş. Dosyanın değişiklik saatine de bak: `(Get-Item .env).LastWriteTime`.
**Neden 2:** Ortam değişkenleri container **yaratılırken** okunur. Çözüm: `docker compose up -d --force-recreate litellm` (`restart` yetmez).

### 6. Anahtarın sonunda garip bir karakter (`ü`)
**Neden:** Klavyeden fazladan bir tuş kaçtı. Anahtarlar sadece ASCII karakter içerir.
**Çözüm:** Yazdırılamayan/ASCII dışı karakterleri temizle. Anahtarı elle yazmak yerine konsoldan kopyala-yapıştır.

### 7. `Your credit balance is too low to access the Anthropic API`
**Neden:** Anthropic API'si ücretlidir ve Claude.ai aboneliğinden **ayrıdır**. Anahtar geçerli (kimlik doğrulama geçti) ama hesapta kredi yok.
**Çözüm:** Anthropic konsolu → **Plans & Billing** → küçük bir kredi yükle. Harcama limiti koy.
**Not:** Bu hata aslında iyi haber: kimlik doğrulama geçti, zincir çalışıyordu. Kredi yüklenince sorun kalmadı (yaşadığımız çözüm buydu).

### 8. Anahtarı sohbete yapıştırmak
**Neden:** Gizli anahtar bir konuşma kaydına girdi.
**Çözüm:** İşin sonunda konsoldan o anahtarı **sil**, yenisini üret. Bundan sonra anahtarı sadece `.env`'ye yaz.

### 9. `warning: LF will be replaced by CRLF`
**Anlamı:** Windows satır sonu dönüşümü hakkında bilgi. **Zararsız**, yok sayılabilir.

---

## Olası ama henüz yaşanmamış hatalar

| Belirti | Olası neden | Çözüm |
|---|---|---|
| `Unable to resolve service for type '...'` | Servis DI'a kaydedilmemiş | `Program.cs` veya `DependencyInjection.cs`'e kayıt ekle |
| `Connection refused` (5432/27017/5672) | Docker servisi kapalı | `docker compose up -d`, `docker compose ps` |
| `password authentication failed` (Postgres) | user-secrets'taki şifre `.env`'dekiyle uyuşmuyor | İkisini eşitle. Not: volume varsa Postgres **ilk açılıştaki** şifreyi tutar, `.env`'yi sonradan değiştirmek yetmez (veriyi silmek istemezsen şifreyi Postgres içinden değiştir; test verisi önemsizse `docker compose down -v`) |
| `502` yanıtı `/api/sohbet`'ten | LiteLLM kapalı/yanlış anahtar/kredi yok | `docker compose logs litellm`, LiteLLM'e doğrudan istek |
| LiteLLM `401` | `LiteLlm:Key` ile `LITELLM_MASTER_KEY` farklı | İkisini eşitle, API'yi yeniden başlat |
| `Address already in use` (5254) | Önceki `dotnet run` hâlâ çalışıyor | Eski pencereyi kapat veya portu kullanan işlemi bul |
| `Database ... does not exist` / tablo yok | Migration uygulanmamış | `dotnet ef database update ...` |
| Migration `Build failed` | Kodda derleme hatası var | Önce `dotnet build` ile düzelt |
| CI kırmızı ama yerelde yeşil | Ortam farkı veya push edilmemiş dosya | Actions logunu oku; `git status` ile eksik dosya var mı bak |
| Test kırmızı | Kod ya da test bozuldu | Hangi testin, hangi satırda başarısız olduğunu oku; son değişikliği `git diff` ile incele |

## Hiçbir şey çalışmıyorsa sıfırlama (veri silinir!)

```powershell
docker compose down -v          # tüm container ve verileri siler (kullanıcı tablosu dahil)
docker compose up -d
dotnet ef database update -p src/Asistan.Infrastructure -s src/Asistan.Api   # tabloları yeniden kur
```
Bu, test verilerini (ör. `emre@test.com`) siler; önemli verin varsa yapma.
