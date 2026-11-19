# Lab 10: Claude CLI / Skills / MCP / Markdown

**Hedef:** Claude Code'u projeye öğretecek 4 şeyi kendin yaz: bir **skill**, bir **slash komutu**, **CLAUDE.md kuralı**, bir **MCP sunucusu**.

Zihinsel model: hepsi **Markdown/JSON dosyaları**. Claude Code projeyi açınca bunları okur:
- `CLAUDE.md`: projenin kalıcı "kuralları/hafızası" (her oturumda yüklenir; kısa ve emir kipinde olsun).
- **Skill** (`.claude/skills/<ad>/SKILL.md`): belirli bir iş için adım adım talimat; Claude `description`'a bakıp **kendisi** seçer.
- **Slash komutu** (`.claude/commands/<ad>.md`): senin yazıp `/ad argüman` ile çağırdığın hazır prompt; `$ARGUMENTS` yerine argüman girer.
- **MCP sunucusu** (`.mcp.json`): Claude'a **dış araç/veri** bağlar (veritabanı, GitHub, dosya...). Güvenlik: salt-okunur başla, sırrı dosyaya yazma.

## Görevler

**G1-G3. Skill.** `.claude/skills/test-yaz/SKILL.md` (klasör adı = `name`). Başka ad da olur, `yeni-entity` olmasın.
> Örnek (gerçek, projede): `.claude/skills/yeni-entity/SKILL.md`. Kalıp:
> ```markdown
> ---
> name: test-yaz
> description: Yeni bir servis sınıfı için unit test yazarken kullan. xUnit, Moq ve FluentAssertions ile AAA düzeninde test üretir.
> ---
> 1. Servisin bağımlılıklarını (arayüzleri) bul.
> 2. Her bağımlılığı Moq ile sahtele.
> 3. Geçerli durum, boş girdi ve sınır değer için ayrı test yaz.
> 4. Hata durumunda "kaydedilmedi" doğrulaması (Times.Never) ekle.
> ```
> İyi `description` = NE yapar + NE ZAMAN kullanılır (en az 60 karakter). Belirsizse Claude skill'i hiç seçmez.

**G4. Slash komutu.** `.claude/commands/ozet.md`:
> Örnek:
> ```markdown
> Şu dosyayı 5 maddede özetle ve riskli yerleri belirt: $ARGUMENTS
> ```
> Kullanım: `/ozet src/Asistan.Application/Sohbet/SohbetServisi.cs`

**G5. CLAUDE.md.** Dosyaya `## Test` başlığı ve en az 3 madde ekle.
> Örnek: `## Test` / `- Her yeni servise unit test yaz.` / `- Dış sistemleri Moq ile sahtele.` / `- Hata yolunda Times.Never ile kaydı doğrula.`

**G6-G8. MCP.** `.mcp.json` içine yeni sunucu ekle (eskileri BOZMA). Örnek, docs klasörünü okuyan dosya sunucusu:
> ```json
> "dosya-okuyucu": {
>   "command": "npx",
>   "args": ["-y", "@modelcontextprotocol/server-filesystem", "./docs"]
> }
> ```
> Sır gerekiyorsa: `"env": { "TOKEN": "${GITHUB_TOKEN}" }`, değeri ortam değişkeninden okunur, dosyaya yazılmaz.
> Gerçek örnekler dosyada: `mongodb` (salt-okunur kullanıcı + `--readOnly`) ve `github`.

## Sık hatalar
- Skill seçilmiyor: `description` belirsiz ya da `name` klasör adıyla uyuşmuyor.
- `.mcp.json` bozuk JSON (son virgül!): `ConvertFrom-Json` hata verir.
- MCP aracı yazma yetkili: üretim verisine bağlama; salt-okunur kullanıcı + `--readOnly`.

## Mülakat soruları
1. CLAUDE.md, skill, slash komutu, hook, MCP: hangisi ne zaman?
2. MCP nedir, hangi sorunu çözer (her araç için ayrı entegrasyon yerine tek protokol)?
3. MCP güvenliği: en az yetki, sırların yönetimi, prompt injection (araç çıktısı da güvenilmez girdidir).
4. İyi bir skill description'ı nasıl yazılır?
5. Claude'a büyük bir görevi nasıl verirsin (plan modu, küçük adımlar, test ile doğrulatma)?
