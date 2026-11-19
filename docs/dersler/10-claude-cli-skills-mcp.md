# Ders 10 · Claude CLI, CLAUDE.md, Skills, MCP ve Markdown (ayrıntılı)

Bu ders, kodu **yazarken** Claude'u yardımcı geliştirici olarak kullanmayı anlatır; uygulamanın çalışma anıyla ilgisi yoktur. Dosyalar: `CLAUDE.md`, `.claude/skills/yeni-entity/SKILL.md`, `.mcp.json`, tüm `docs/*.md`.

> **Doğrulama notu:** Bu derste **gerçekten sınadıklarım** (MCP sunucusunu protokolle konuşturmak, skill içindeki komutları çalıştırmak, salt-okunur kullanıcının yazamadığını görmek) ve **belgelere dayanarak anlattıklarım** (Claude Code arayüz komutları) açıkça ayrıldı. GitHub MCP bağlantısını token olmadığı için **denemedim**.

---

## 1. Markdown (`.md`)

Düz metinle biçimlendirme dilidir. Projede her yerde: `README.md`, `CLAUDE.md`, `SKILL.md`, bu dersler, GitHub PR açıklamaları.

| Yazdığın | Sonuç |
|---|---|
| `# Başlık`, `## Alt başlık` | Başlıklar |
| `**kalın**`, `*italik*` | Vurgu |
| `- madde` / `1. madde` | Liste |
| `` `kod` `` | Satır içi kod |
| üç backtick + dil adı | Kod bloğu |
| `[metin](https://...)` | Bağlantı |
| `\| a \| b \|` satırları | Tablo |
| ```` ```mermaid ```` bloğu | **Diyagram** (GitHub çizer): `docs/07-tasarim-ve-mimari.md` |

## 2. Claude CLI (Claude Code)

Terminalde (veya masaüstü uygulamada) çalışan bir **AI kodlama ajanıdır**. Projedeki dosyaları okur ve düzenler, komut çalıştırır (`dotnet test` gibi), Git işlemleri yapar. Bu projeyi yazarken kullandığım araç da budur.

**Verimli kullanımın ilkeleri** (belgelere dayanır):
- Görevi **net** ver: *"`SohbetServisi`'ne boş soru kontrolü ekle, testini yaz, `dotnet test` ile doğrula."* Net görev + doğrulama ölçütü = daha iyi sonuç.
- Büyük işte önce **plan** iste, sonra uygulat; her adımda değişikliği (diff) **incele**.
- Bağımsız her iş için **yeni oturum** aç; uzun konuşma bağlamı şişirir.
- Doğrulatmayı iste: test çalıştır, derle. "Yaptım" ile "çalıştığını gördüm" farklıdır.

Sık komutlar (belgelere dayanır, arayüze göre değişebilir): `/init` (projeyi tarayıp `CLAUDE.md` taslağı), `/clear` (bağlamı sıfırlar), `/compact` (konuşmayı özetleyip yer açar), `/mcp` (bağlı MCP sunucuları), `/model`, plan modu.

## 3. `CLAUDE.md`: projenin hafızası

Claude her oturum başında kökteki `CLAUDE.md`'yi **okur**. Yeni bir ekip arkadaşına verilecek "projeye hoş geldin" notu gibidir. **Kısa ve somut tut**: her satır her oturumda token harcar.

Bizim `CLAUDE.md`'nin bölümleri ve neden:
| Bölüm | Neden |
|---|---|
| Mimari (6 proje, kim kimi bilir) | Claude yeni kodu doğru katmana yazsın |
| Komutlar | Derle/test/çalıştır/migration komutlarını tahmin etmesin |
| Kurallar | "Application → Infrastructure yasak", "hata yutma", Conventional Commits |
| Gizli bilgiler | `.env` ve `appsettings.Local.json` asla koda/commit'e girmesin |
| MCP | Hangi sunucular var, hangi güvenlik sınırıyla |

Alt klasörlere de `CLAUDE.md` konabilir (o klasörde çalışırken okunur). Kişisel tercihler `~/.claude/CLAUDE.md` içinde tüm projelerde geçerlidir.

## 4. Skill: tekrarlayan işin tarifi

**Skill**, tekrar eden bir işi nasıl yapacağını Claude'a anlatan bir **talimat klasörüdür**: `.claude/skills/<ad>/SKILL.md`. Başındaki **`description`** alanı, Claude'un skill'i *ne zaman kullanacağına karar vermesini* sağlar; gövde sadece gerektiğinde yüklenir.

**CLAUDE.md ile farkı:** CLAUDE.md **her zaman** yüklenir (kısa olmalı). Skill **sadece ilgili iş geldiğinde** yüklenir (uzun, ayrıntılı olabilir). Uzun talimatlar skill'e konur.

**Bizim skill'imiz: `yeni-entity`.** Yeni bir veritabanı tablosu eklemenin 11 adımını anlatır: Domain sınıfı → DbContext ayarı → arayüz ve servis → depo → DI kaydı (iki yerde) → migration → (isteğe bağlı) controller → testler → derle+test → rehber → commit. "Projeye X entity'si ekle" dersen Claude skill'i kendisi seçer; ya da doğrudan `/yeni-entity` ile çağırırsın.

**Skill'in kalitesi = içindeki komutların doğruluğu.** Yanlış komut yazan bir skill, onu kullanan herkesi yanıltır. Bu yüzden içindeki migration komutunu **gerçekten çalıştırdık**: geçici bir entity ekledik, `dotnet ef migrations add` bir `CreateTable` üretti, `migrations remove` ile geri aldık, deneme izi kalmadı. Skill'in `description`'ı da kasıtlı olarak "MongoDB için DEĞİL" diyerek sınırını çiziyor; yanlış yerde tetiklenmesin.

## 5. MCP (Model Context Protocol)

**Nedir:** AI uygulamalarını dış araçlara ve veri kaynaklarına bağlayan **açık bir standart**. "USB-C" benzetmesi: bir kez MCP sunucusu yazılır, MCP destekleyen **her istemci** (Claude Code, masaüstü, diğer editörler) onu kullanabilir.

| Taraf | Ne | Bizde |
|---|---|---|
| **MCP sunucusu** | Araçlar (tools), kaynaklar (resources), prompt'lar sunar | `mongodb-mcp-server` |
| **MCP istemcisi** | Sunucuya bağlanır, araçları gerektiğinde çağırır | Claude Code |

**`.mcp.json`:** Projeye konan, Git'e girebilen yapılandırma. Ekipteki herkes aynı sunucuları kullanır.
```json
{ "mcpServers": {
    "mongodb": { "command": "npx", "args": ["-y", "mongodb-mcp-server@3.3.0", "--readOnly"],
                 "env": { "MDB_MCP_CONNECTION_STRING": "${MDB_MCP_CONNECTION_STRING}" } },
    "github":  { "type": "http", "url": "https://api.githubcopilot.com/mcp/",
                 "headers": { "Authorization": "Bearer ${GITHUB_PERSONAL_ACCESS_TOKEN}" } } } }
```
- `${...}`: değer dosyada değil **ortam değişkeninde** durur: gizli bilgi Git'e girmez.
- Sürüm **sabit** (`@3.3.0`), `@latest` değil: bir gün davranışı değişen yeni sürüm sessizce gelmesin.

### Güvenlik (MCP'nin en önemli konusu)

MCP sunucuları **gerçek veriye ve yetkiye** erişir. Üç önlem aldık:
1. **Salt-okunur kullanıcı:** MongoDB'de yalnızca `asistan` veritabanında `read` yetkili ayrı bir kullanıcı (`mcp_okuyucu`) yarattık; root kullanıcıyı vermedik. **Kanıt:** bu kullanıcıyla yazmayı denedik → `Unauthorized`.
2. **Sunucu bayrağı `--readOnly`:** yazma araçları hiç sunulmaz.
3. **Yerel kopya:** üretim veritabanına değil, yerel Docker'daki veritabanına bağlı.
İki bağımsız katman (kullanıcı yetkisi + sunucu bayrağı) = biri unutulsa diğeri korur.

> Sadece **güvendiğin** sunucuları ekle. Bir MCP aracının döndürdüğü metin de, bir web sayfası gibi, içinde **talimat gibi görünen kötü niyetli metin** (prompt injection) taşıyabilir. Araç sonuçlarını talimat değil **veri** olarak ele al.

### Gerçekten konuştuk: MCP protokolü

MongoDB MCP sunucusunu stdio üzerinden, Claude Code'un yaptığı gibi konuşturduk:

| Adım | İstek | Cevap |
|---|---|---|
| 1 | `initialize` | `MongoDB MCP Server 3.3.0`, protokol `2025-03-26` |
| 2 | `tools/list` | **20 araç**: `find`, `aggregate`, `count`, `list-collections`, `collection-schema`, `explain`... Yazma aracı **yok** |
| 3 | `list-connections` | `preconfigured` bağlantı (`MDB_MCP_CONNECTION_STRING`'den) |
| 4 | `list-collections` (veritabanı `asistan`) | 4 koleksiyon bulundu |
| 5 | `count` (`mesajlar`) | Çalıştı |
| 6 | `insert-one` (yazma denemesi) | **`Tool insert-one not found`**: salt-okunur modda yazma aracı yok |

Araçların `connectionId` istediğini ilk denemede öğrendik (bu sürümün bağlantı modeli). Claude Code içinde bu araçlar şöyle kullanılır: *"mesajlar koleksiyonunda son 5 kaydı göster"* dersin, Claude `find` aracını kendisi çağırır.

### GitHub MCP sunucusu

`.mcp.json`'da tanımlı (uzak HTTP sunucu, `GITHUB_PERSONAL_ACCESS_TOKEN` ile). **Denemedim**, çünkü senin adına bir GitHub token'ı oluşturamam. Kullanmak istersen: GitHub → Settings → Developer settings → **Fine-grained token**, sadece bu repoya ve **salt-okunur** yetkilerle; sonra token'ı `GITHUB_PERSONAL_ACCESS_TOKEN` ortam değişkenine koy (PowerShell: `[Environment]::SetEnvironmentVariable("GITHUB_PERSONAL_ACCESS_TOKEN","<token>","User")`, Claude Code'u yeniden aç). Token'ı sohbete yazma.

### Aktif etmek için

`.env`'deki `MDB_MCP_CONNECTION_STRING` değerini Claude Code'un görebileceği bir **ortam değişkenine** de koymalısın (Claude Code `.env` dosyasını okumaz). Sonra Claude Code'da `/mcp` ile sunucuların bağlandığını gör.

### Kendi MCP sunucunu yazmak

.NET için resmî `ModelContextProtocol` C# SDK'sı var. Örneğin asistanın hafızasını MCP aracı olarak açarsan, Claude geliştirme sırasında kullanıcı hafızalarını doğrudan sorgulayabilir. **Bu projede yapılmadı**, fikir olarak duruyor.

## 6. Üçü birlikte: ne zaman hangisi?

| İhtiyaç | Araç |
|---|---|
| "Bu projeyi tanıyan biri olsun, kuralları bilsin" | `CLAUDE.md` (her zaman yüklü, kısa) |
| "Bu tekrarlayan işi hep aynı şekilde yap" | Skill (gerektiğinde yüklenir, uzun olabilir) |
| "Dış sisteme (veritabanı, GitHub) eriş" | MCP (araç sağlar) |
| "Bunu yazılı ve okunur tut" | Markdown (+ Mermaid diyagramı) |

## 7. Bilmen gerekenler

1. Claude CLI kodlama ajanıdır: net görev ver, plan iste, diff'i incele, doğrulat.
2. `CLAUDE.md` her oturumda yüklenir: **kısa ve somut** tut.
3. Skill = tekrarlayan işin tarifi; `description` ne zaman kullanılacağını söyler; **içindeki komutları mutlaka çalıştırıp sına**.
4. MCP = dış araçlara standart bağlantı; sunucu araç sunar, istemci çağırır.
5. MCP güvenliği: salt-okunur kullanıcı + `--readOnly` + yerel kopya; sürüm sabitle; sır ortam değişkeninde.
6. MCP araç sonuçları **veridir, talimat değil** (prompt injection).
7. `.mcp.json` ve `.claude/skills/` Git'e girer; sırlar girmez.
