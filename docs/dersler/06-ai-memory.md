# Ders 6 · AI Memory: kısa ve uzun süreli hafıza (ayrıntılı)

Dosyalar: `src/Asistan.Application/Bellek/HafizaServisi.cs`, `HafizaAyarlari.cs`, `HafizaArayuzleri.cs`, `src/Asistan.Worker/Consumers/SohbetTamamlandiConsumer.cs`, `src/Asistan.Api/Controllers/HafizaController.cs`, `tests/Asistan.Tests/HafizaServisiTests.cs`. Rakamlar bu projede **ölçülmüştür**.

---

## 1. Temel gerçek: model hatırlamaz

Büyük dil modelleri **durumsuzdur (stateless)**: her istek sıfırdan başlar, önceki konuşmayı bilmez. "Hafıza", aslında **senin her istekte modele gönderdiğin metindir**. Hafıza yönetimi, sınırlı bağlam penceresine en faydalı bilgiyi **en az token'la** sığdırma sanatıdır.

**Neden yönetmek gerekir?**
1. **Pencere sınırlı:** model tek seferde belli sayıda token okuyabilir.
2. **Maliyet:** gönderilen her token ücretlidir ve **her istekte yeniden ödenir.**
3. **Kalite:** gereksiz uzun bağlam modelin dikkatini dağıtır.

**Ölçtük (LiteLLM'e aynı soruyu farklı geçmişle gönderdik):**

| Önceki mesaj sayısı | `prompt_tokens` |
|---|---|
| 0 | 56 |
| 4 | 216 |
| 12 | 536 |

Her ek mesaj yaklaşık **40 token** ekliyor ve her soruda yeniden gönderiliyor. 200 mesajlık bir geçmişi her soruda göndermek hem pahalı hem yavaş. Çözüm: eski mesajları **özete** çevirmek, kullanıcı hakkındaki kalıcı bilgileri ayrıca tutmak.

## 2. Hafıza katmanları ve projedeki karşılığı

| Katman | Ne tutar | Nerede | Prompt'a nasıl girer |
|---|---|---|---|
| Çalışma belleği | O anki istek | RAM | Zaten gönderilen mesaj listesi |
| **Kısa süreli** | Bu oturumun konuşması | MongoDB `mesajlar` + `oturumOzetleri` | **Özet + özetlenmemiş ham mesajlar** |
| **Uzun süreli** | Kullanıcı hakkında kalıcı gerçekler | MongoDB `hafizalar` | System prompt'a "Kullanıcı hakkında bildiklerin: ..." |

Kısa süreli hafıza **oturuma** bağlıdır (yeni sohbet = sıfır). Uzun süreli hafıza **kullanıcıya** bağlıdır (yeni sohbette de hatırlanır).

## 3. Modele giden mesaj listesi nasıl kurulur? (`BaglamKurAsync`)

```
[ system  ] "Sen bir .NET eğitmenisin. Türkçe, kısa ve net cevap ver."
            + "Kullanıcı hakkında bildiklerin:\n- Kullanıcının adı Emre; ..."     (uzun süreli, en yeni 20)
            + "Bu konuşmanın önceki kısmının özeti: ..."                          (özet, varsa)
[ user/assistant ] özetlenmemiş HAM mesajlar (kronolojik)
[ user    ] yeni soru
```

Üç şey MongoDB'den okunur: `hafizalar` (kullanıcının en yeni `MaksHafiza` kadarı), `oturumOzetleri` (özet + kaç mesaj özetlendi), `mesajlar` (özete girmemiş ham mesajlar). **Özet + ham mesajlar oturumun tamamını kapsar: arada hiçbir mesaj kaybolmaz.**

Ayarlar (`appsettings.json` → `Hafiza`):

| Ayar | Varsayılan | Anlamı |
|---|---|---|
| `PencereBoyutu` | 10 | Özetlemeden sonra prompt'ta ham kalacak mesaj sayısı |
| `OzetEsigi` | 20 | Özetlenmemiş mesaj sayısı bunu aşınca özetleme başlar |
| `MaksHafiza` | 20 | Prompt'a en fazla kaç uzun süreli hafıza girer |
| `HizliModel` | `hizli` | Özetleme ve hafıza çıkarma için ucuz model |

## 4. Kısa süreli: kayan pencere + özetleme (adım adım)

Değişkenler: **M** = oturumdaki toplam mesaj, **S** = şimdiye kadar özete girmiş mesaj sayısı (`ozetlenenMesajSayisi`), **U = M − S** = özetlenmemiş (ham) mesaj.

**Kural:** `U > OzetEsigi` ise özetle: **en yeni `PencereBoyutu` mesaj hariç** `[S, M − PencereBoyutu)` aralığı özete girer; yeni `S = M − PencereBoyutu`.

**Varsayılanlarla yürüyüş (pencere 10, eşik 20):**

| Olay | M | S | U | Ne olur |
|---|---|---|---|---|
| 10 soru-cevap (20 mesaj) | 20 | 0 | 20 | `U > 20` değil → özetleme **yok**, 20 ham mesaj gider |
| 11. soru-cevap | 22 | 0 | 22 | **Özetle:** `[0, 12)` özete girer, **S = 12**; ham: 10 mesaj |
| Sonraki sorular | 24 … | 12 | 12 … | Prompt = özet + ham mesajlar |
| M = 34 | 34 | 12 | 22 | **Yeniden özetle:** önceki özet + `[12, 24)` → yeni özet, **S = 24** |

Özet **yuvarlanır**: her seferinde "önceki özet + yeni eski mesajlar" ucuz modele verilir. Neden son 10'u ham bırakıyoruz? Yakın geçmişteki ayrıntılar ("Peki ya Transient?" gibi atıflar) özette kaybolur; model onlara ham halleriyle ihtiyaç duyar.

**Gerçek deney** (eşiği küçülttük: pencere 2, eşik 4; 10 mesajlık oturum): MongoDB'de oluşan özet kaydı: `ozetlenenMesajSayisi: 8` (= 10 − 2) ve özet metni (Haiku yazdı):

> *"Kullanıcı Emre, C#'ı yeni öğrenen biri olup hedefi web API geliştirmek; konuşmada Dependency Injection'ın bağımlılıkların dışarıdan constructor ile alınması ... açıklandı. Ardından Scoped ömrün ... Transient ömrün ise ... belirtildi."*

Sonra "ilk mesajımda adımı ve hedefimi söylemiştim, hatırlıyor musun?" diye sorduk: *"Evet, adın Emre, C#'ı yeni öğreniyorsun ve hedefin web API yazmak."* Bilgi, ham mesajlar prompt'ta olmadığı halde özet ve uzun süreli hafıza sayesinde yaşadı.

## 5. Uzun süreli hafıza: kalıcı bilgi çıkarma

Her sohbetten sonra `hizli` modele sorulur: *"Bu konuşmadan, kullanıcı hakkında GELECEK konuşmalarda da işe yarayacak kalıcı bir bilgi çıkar. Varsa tek kısa cümle yaz, yoksa YOK yaz."* Prompt'a **zaten bilinen hafızalar** da konur ki model aynı bilgiyi tekrar yazmasın.

Model çıktısı **doğrudan kaydedilmez**, `Temizle` süzgecinden geçer:

| Kural | Neden |
|---|---|
| `YOK` (büyük/küçük harf, noktalı) → kaydetme | Yeni bilgi yok |
| 200 karakterden uzun → kaydetme | Hafıza sonradan **system prompt'a** girer; uzun metin saldırı ve gürültü demektir |
| Satır sonları tek satıra çevrilir, `-` işareti atılır | Prompt'a satır enjeksiyonu engellenir |
| Aynı bilgi (büyük/küçük harf farkıyla) zaten varsa kaydetme | Tekrar etmesin |

**Gerçek çıktılar:** *"Ben Emre, C# öğrenmeye yeni başladım, hedefim web API yazmak"* → hafızaya: *"Kullanıcının adı Emre; C# öğrenmeye yeni başladı ve hedefi web API yazmak."* Başka bir denemede *"Ben Ayşe, Python biliyorum, C# öğreniyorum"* → sadece *"Kullanıcının adı Ayşe."* (model tek cümle verdi, Python bilgisi **kaybolmuş olabilir**: bunu sınırlar bölümünde sayıyoruz).

**Yeni oturumda doğrulama:** Aynı kullanıcı yeni bir oturumda *"Adımı ve ne öğrendiğimi hatırlıyor musun?"* dedi → *"Evet, adın Emre; C# öğreniyorsun..."* **Başka bir kullanıcı** aynı soruyu sorunca *"Hayır, hatırlayamıyorum"*: hafıza kullanıcılar arası **sızmadı**.

## 6. Neden `hizli` model ve neden arka planda?

- **Maliyet:** `hizli` bu işlerde `akilli`'nın yaklaşık **20 katı ucuz** (LiteLLM'in fiyat tablosuna göre: 0.0000062 $ ve 0.000124 $, aynı küçük istek için). Özetleme ve çıkarma her soruda 1-2 ekstra çağrı demektir; bunları pahalı modelle yapmak anlamsız.
- **Arka plan:** Aşama 8'de bu işler HTTP isteğinin **içindeydi** ve kullanıcıyı bekletiyordu. Aşama 9'dan beri cevap hazır olunca ayrı bir kuyruk mesajıyla (`SohbetTamamlandi`) Worker'da çalışır; kullanıcı beklemez. Doküman modunda bu iş hiç sıraya konmaz (doküman soruları kullanıcı hakkında kalıcı bilgi taşımaz).
- **Hata yalıtımı:** `SohbetSonrasiAsync` **asla hata fırlatmaz**: özetleme patlasa bile hafıza çıkarma denenir ve kullanıcının cevabı bozulmaz. İkisi `try/catch` içinde, hata loglanır (`HafizaServisiTests`'te sınandı).

## 7. Gizlilik ve güvenlik

- **Görme ve silme hakkı** (KVKK/GDPR açısından önemli): `GET /api/hafiza/{kullaniciId}`, `DELETE /api/hafiza/{kullaniciId}/{id}`, `DELETE /api/hafiza/{kullaniciId}`. Silme filtresi `kullaniciId`'yi de içerir: **başka kullanıcının hafızası silinemez** (gerçek MongoDB'de sınadık: başka kullanıcı silmeyi denedi → 404, hafıza yerinde kaldı).
- **Prompt injection:** Hafıza metni sistem prompt'una girer. Kötü niyetli bir kullanıcı hafızasına talimat yerleştirmeye çalışsa bile 200 karakter ve tek satır sınırıyla etkisi dar kalır ve **sadece kendi hafızasını** etkiler.
- **Henüz kimlik doğrulama yok:** `kullaniciId`'yi bilen o kullanıcının hafızasını okuyup silebilir.

## 8. Sınırlar (bilerek basit tutulanlar)

| Sınır | Sonuç / çözüm |
|---|---|
| Hafıza **anlamsal seçilmiyor** | Kullanıcının en yeni 20 hafızası hep gider. Gerçek sistemlerde **embedding + vektör arama** ile soruyla ilgili olanlar seçilir (Anthropic'te embedding modeli yok, ayrı sağlayıcı gerekir) |
| **Çelişen bilgi** | "Artık Java öğreniyorum" eskisini güncellemez; ikisi de durur, kullanıcı siler |
| Model **bazı bilgiyi atlayabilir** | Ucuz modelin kalitesine bağlı (Ayşe örneğinde Python bilgisi yazılmadı) |
| Özet kalitesi | `hizli` modelin yazdığı kadar; önemli ayrıntı özette düşebilir |
| **Eş zamanlı iki soru** (aynı oturum) | Özetleme yarışabilir |
| Her soruda 1-2 ekstra çağrı | Maliyet (ucuz model ve arka planda olsa da) |

## 9. Testler neyi kanıtlıyor? (`HafizaServisiTests`, 18 test)

Küçük sayılarla (pencere 2, eşik 4) sınanır:
- **Bağlam:** hafıza/özet yoksa system prompt sadece temel metin; varsa hafıza ve özet eklenir; özetlenmiş mesajlar atlanır, ham mesajlar sırayla gelir.
- **Özetleme:** eşik aşılmadıysa özetlemez; aşıldıysa **doğru aralığı** (`yeniSinir`) özetleyip kaydeder; önceki özet yeni isteğe girer.
- **Hafıza çıkarma:** yeni bilgiyi kaydeder; `YOK`/`yok.`/boşu kaydetmez; zaten bilineni tekrarlamaz; 200 karakterden uzunu ve çok satırlıyı süzer.
- **Dayanıklılık:** LLM hata verirse fırlatmaz; özetleme patlarsa hafıza çıkarma yine denenir.

**Mutasyon kontrolü:** Özetleme sınırını (`yeniSinir`) bilerek bozduk: **3 test kırmızıya döndü**, geri alınca yeşil. Yani testler gerçekten koruma sağlıyor.

## 10. Bilmen gerekenler

1. Model hatırlamaz; hafıza = senin gönderdiğin metin. Her mesaj ~40 token ekler ve her istekte yeniden ücretlenir.
2. Kısa süreli: **kayan pencere + yuvarlanan özet** (`U > eşik` ise eskileri özetle, en yeni N'i ham bırak).
3. Uzun süreli: ucuz modelle kalıcı bilgi çıkar, **süz** (YOK, uzunluk, satır, tekrar), kullanıcıya bağla.
4. Yan işleri ucuz modele (~20× ucuz) ve arka plana ver; kullanıcıyı bekletme.
5. Yan işlerin hatası kullanıcının cevabını bozmamalı (hata yalıtımı).
6. Hafızaya giren metin prompt'a girer: uzunluğu ve biçimi sınırla (prompt injection).
7. Kullanıcıya hafızasını görme ve silme hakkı ver; başkasınınkini silemesin.
8. Hafıza anlamsal değil, kalite ucuz modelin kalitesi kadar: gerçek ürün embedding + vektör arama kullanır.
