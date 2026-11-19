# Lab 07: AI Memory (kısa/uzun süreli hafıza mantığı)

**Hedef:** Projedeki hafıza stratejisinin saf mantığını (sliding window, özet eşiği, hafıza filtresi) TDD ile kendin yaz.
**Dosyalar:** `src/Asistan.Application/Bellek/KonusmaPenceresi.cs` ve `tests/Asistan.Tests/KonusmaPenceresiTests.cs`

Zihinsel model: LLM **hafızasızdır**; her istekte geçmişi SEN gönderirsin. Geçmiş büyüdükçe token ve maliyet artar (ölçtük: 0/4/12 mesaj → 56/216/536 token).
- **Kısa süreli:** son N mesaj (**sliding window**) + eşik aşılınca eski kısmın **özeti**.
- **Uzun süreli:** konuşmadan çıkarılan kalıcı gerçekler ("kullanıcı kahve sever"), **filtrelenerek** saklanır (çöp/uzun/tekrar elenir).
Gerçek hâli: `HafizaServisi.cs`. Sen onun saf mantığını ayrı, test edilebilir bir sınıfa yazıyorsun.

## Birebir uyman gereken imzalar (gizli testler bunları çağırır)

```csharp
namespace Asistan.Application.Bellek;

public static class KonusmaPenceresi
{
    public static IReadOnlyList<string> SonN(IReadOnlyList<string> mesajlar, int n);
    public static bool OzetGerekliMi(int mesajSayisi, int esik);
    public static IReadOnlyList<string> HafizaFiltrele(IEnumerable<string> adaylar);
}
```

## Görevler

**G1. Sınıfı yaz.**
- `SonN`: listenin SON `n` elemanını sırayı bozmadan döndür. `n` sayıdan büyükse hepsi; `n <= 0` ise boş liste.
  > Örnek (benzer, sen SonN yazacaksın): `IlkN(["a","b","c"], 2)` → `["a","b"]`. LINQ: `Take`/`Skip`/`TakeLast`.
- `OzetGerekliMi`: `mesajSayisi >= esik` ise true (eşik 20, sayı 20 → true; 19 → false).
- `HafizaFiltrele`: her adayı `Trim()`; şunları ele: boş, `YOK` (büyük/küçük harf fark etmez), 200 karakterden uzun, birden çok satırlı. Tekrarları (büyük/küçük harf farkıyla bile) ele, ilk görüneni koru.
  > Örnek (kural zinciri): `["  Kahve ", "yok", "kahve"]` → `["Kahve"]`.
  > Neden filtre? Modelin çıkardığı hafıza **güvenilmez girdidir**: "YOK" yazar, roman yazar, tekrar eder; temizlemeden DB'ye gitmez (proje: `HafizaServisi`'ndeki aynı kurallar).

**G2. En az 5 unit test yaz** (`KonusmaPenceresiTests.cs`, xUnit + FluentAssertions; Moq gerekmez: saf mantık!).
> Örnek test iskeleti:
> ```csharp
> [Fact]
> public void SonN_NSayidanBuyuk_HepsiniDondurur()
>     => KonusmaPenceresi.SonN(new[] { "a", "b" }, 5).Should().Equal("a", "b");
> [Theory][InlineData(0)][InlineData(-3)]
> public void SonN_SifirVeyaNegatif_BosDondurur(int n) => /* ... */;
> ```

**G3. Gizli testler.** Doğrulayıcı kendi testlerini geçici olarak projene kopyalar, çalıştırır, siler. Başarısız testin adı davranışı söyler.

## Mülakat soruları
1. LLM neden "hafızasız"? Context window nedir, dolarsa ne olur?
2. Sliding window ile özetleme (summarization) farkı, ikisi birlikte neden?
3. Kısa vs uzun süreli hafıza; uzun süreli nerede saklanır (vektör DB / belge DB) ve nasıl geri çağrılır?
4. Modelin ürettiği hafızaya neden güvenmemeliyiz (prompt injection, çöp veri)?
5. Hafıza silme/görüntüleme (KVKK/GDPR) neden bir özellik olmalı?
