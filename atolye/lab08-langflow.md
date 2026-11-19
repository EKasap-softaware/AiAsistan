# Lab 08: LangFlow

**Hedef:** LangFlow arayüzünde (http://localhost:7860) kendi akışını kur, JSON olarak dışa aktar, sırsız olduğunu kanıtla.
**Çıktı dosyası:** `langflow/atolye-akisi.json`  (giriş: kullanıcı/parola `.env`'deki `LANGFLOW_SUPERUSER*`)

Zihinsel model: LangFlow = sürükle-bırak **LLM iş akışı** aracı. Bileşenleri (**node**) kablolarla (**edge**) bağlarsın: Chat Input → Prompt → Language Model → Chat Output.
Akış JSON'a çevrilebilir: **git'e girer, gözden geçirilir, başka ortama yüklenir** (projedeki `langflow/dokuman-asistani.json` ve `scripts/langflow-akisi-yukle.ps1` böyle).

## Görevler

**G1-G4. Akışı kur ve dışa aktar.**
1. Yeni akış (New Flow > Blank). İsim: `Atolye Akisi`.
2. Bileşenler: **Chat Input**, **Prompt** (şablon: `Kısa cevap ver. Soru: {soru}`), **Language Model** (OpenAI bileşeni; Base URL `http://litellm:4000/v1`, model `hizli`), **Chat Output**.
3. Kabloları bağla: Chat Input → Prompt'taki `soru` girişi → Language Model → Chat Output.
4. Playground'da bir soru sor, cevap geldiğini gör.
5. Akış adının yanındaki menü > **Export** > JSON. Dosyayı `langflow/atolye-akisi.json` olarak kaydet.
> Örnek: projenin hazır akışı `langflow/dokuman-asistani.json` — aç, `data.nodes[].data.type` alanlarını gör (ChatInput, Prompt, Directory ...). Doğrulayıcı aynı yapıya bakar.

**G5. Sır sızıntısı yok.** Language Model bileşenindeki API anahtarını elle YAZMA:
Settings > **Global Variables** > Add: ad `LITELLM_KEY`, tip **Credential**, değer = `.env`'deki `LITELLM_MASTER_KEY`; bileşende anahtar alanında bu değişkeni seç. Sonra Export et.
> Neden? JSON git'e girer; anahtar içinde olursa sızar. Doğrulayıcı `.env`'deki değerleri dosyada arar.

## Sık hatalar
- Language Model "connection error": LangFlow konteynerinden `localhost` = konteynerin kendisi. LiteLLM'e `http://litellm:4000/v1` (servis adı) ile git.
- Akış kayboldu: LangFlow verisi volume'dedir; `docker compose down -v` siler.
- Dışa aktarırken "Save with my API keys" benzeri seçenek varsa KAPAT.

## Mülakat soruları
1. LangFlow ne zaman kullanılır, ne zaman kod yazarsın (prototip hızı vs sürüm/test/kontrol)?
2. Akışı koda/CI'a nasıl alırsın (JSON export, API ile import, `/api/v1/run`)?
3. "Context stuffing" ile gerçek RAG farkı (dokümanın hepsini prompta koymak vs parçalayıp vektör aramayla bulmak)?
4. Prompt injection nedir? Kullanıcı girdisi neden şablonda ayrı bir değişken?
5. LLM çağrısı neden doğrudan sağlayıcıya değil LiteLLM üzerinden?
