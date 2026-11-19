using System.Text;
using Asistan.Domain.Entities;
using Asistan.Application.Sohbet;
using Microsoft.Extensions.Logging;

namespace Asistan.Application.Bellek;

public interface IHafizaServisi
{
    // Modele gidecek mesaj listesini kurar: [system (+hafızalar +özet)] + [özetlenmemiş mesajlar] + [yeni soru]
    Task<List<LlmMesaj>> BaglamKurAsync(string kullaniciId, string oturumId, string soru, CancellationToken ct = default);

    // Cevap kaydedildikten sonra çağrılır: gerekirse özetler, kalıcı bilgi çıkarır. ASLA hata fırlatmaz.
    Task SohbetSonrasiAsync(string kullaniciId, string oturumId, string soru, string cevap, CancellationToken ct = default);

    Task<IReadOnlyList<Hafiza>> HafizalariGetirAsync(string kullaniciId, CancellationToken ct = default);
    Task<bool> HafizaSilAsync(string kullaniciId, string hafizaId, CancellationToken ct = default);
    Task<long> TumunuSilAsync(string kullaniciId, CancellationToken ct = default);
}

public class HafizaServisi(
    IMesajDeposu mesajlar,
    IHafizaDeposu hafizalar,
    IOturumOzetDeposu ozetler,
    ILlmIstemcisi llm,
    HafizaAyarlari ayar,
    ILogger<HafizaServisi> log) : IHafizaServisi
{
    private const int MaksHafizaKarakter = 200;

    public async Task<List<LlmMesaj>> BaglamKurAsync(string kullaniciId, string oturumId, string soru, CancellationToken ct = default)
    {
        var ilgili = await hafizalar.KullaniciHafizalariAsync(kullaniciId, ayar.MaksHafiza, ct);
        var ozet = await ozetler.GetirAsync(oturumId, ct);
        var ozetlenen = ozet?.OzetlenenMesajSayisi ?? 0;

        // Özete girmemiş (henüz ham duran) mesajlar. Özet + bunlar = oturumun tamamı, hiçbir şey kaybolmaz.
        var ham = await mesajlar.AralikGetirAsync(oturumId, ozetlenen, ayar.OzetEsigi + ayar.PencereBoyutu, ct);

        var sistem = new StringBuilder(SohbetServisi.SistemPrompt);
        if (ilgili.Count > 0)
        {
            sistem.Append("\n\nKullanıcı hakkında bildiklerin:");
            foreach (var h in ilgili) sistem.Append("\n- ").Append(h.Icerik);
        }
        if (ozet is not null)
            sistem.Append("\n\nBu konuşmanın önceki kısmının özeti: ").Append(ozet.Ozet);

        var liste = new List<LlmMesaj> { new("system", sistem.ToString()) };
        liste.AddRange(ham.Select(m => new LlmMesaj(m.Rol, m.Icerik)));
        liste.Add(new LlmMesaj("user", soru));
        return liste;
    }

    public async Task SohbetSonrasiAsync(string kullaniciId, string oturumId, string soru, string cevap, CancellationToken ct = default)
    {
        // İki iş birbirinden bağımsız: biri patlarsa diğeri yine denenir.
        try { await GerekirseOzetleAsync(oturumId, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { log.LogWarning(ex, "Oturum özetlenemedi (oturum {OturumId}).", oturumId); }

        try { await HafizaCikarAsync(kullaniciId, soru, cevap, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { log.LogWarning(ex, "Hafıza çıkarılamadı (kullanıcı {KullaniciId}).", kullaniciId); }
    }

    private async Task GerekirseOzetleAsync(string oturumId, CancellationToken ct)
    {
        var toplam = await mesajlar.SayAsync(oturumId, ct);
        var onceki = await ozetler.GetirAsync(oturumId, ct);
        var ozetlenen = onceki?.OzetlenenMesajSayisi ?? 0;

        if (toplam - ozetlenen <= ayar.OzetEsigi) return;

        // [ozetlenen, toplam - PencereBoyutu) aralığı özete girer; en yeni PencereBoyutu mesaj ham kalır.
        var yeniSinir = toplam - ayar.PencereBoyutu;
        var eskiler = await mesajlar.AralikGetirAsync(oturumId, ozetlenen, yeniSinir - ozetlenen, ct);
        if (eskiler.Count == 0) return;

        var metin = new StringBuilder();
        if (onceki is not null) metin.Append("Önceki özet: ").Append(onceki.Ozet).Append("\n\n");
        metin.Append("Yeni konuşma:\n");
        foreach (var m in eskiler)
            metin.Append(m.Rol == "user" ? "Kullanıcı: " : "Asistan: ").Append(m.Icerik).Append('\n');

        var yeniOzet = await llm.TamamlaAsync(
        [
            new("system", "Konuşmaları kısa bir Türkçe özete dönüştürürsün. En fazla 5 cümle yaz. " +
                          "Kullanıcının adı, hedefleri, seviyesi ve konuşulan konular korunsun. Sadece özeti yaz."),
            new("user", metin.ToString())
        ], ayar.HizliModel, ct);

        await ozetler.KaydetAsync(oturumId, yeniOzet.Trim(), yeniSinir, ct);
    }

    private async Task HafizaCikarAsync(string kullaniciId, string soru, string cevap, CancellationToken ct)
    {
        var mevcut = await hafizalar.KullaniciHafizalariAsync(kullaniciId, ayar.MaksHafiza, ct);

        var bilinenler = mevcut.Count == 0
            ? "(henüz yok)"
            : string.Join("\n", mevcut.Select(h => "- " + h.Icerik));

        var ham = await llm.TamamlaAsync(
        [
            new("system", "Bir konuşmadan, kullanıcı hakkında GELECEK konuşmalarda da işe yarayacak kalıcı bir bilgi " +
                          "(ad, seviye, hedef, kullandığı teknoloji, tercih) çıkarırsın. Geçici şeyleri (tek seferlik sorular) ve " +
                          "zaten bilinenleri yazma. Varsa SADECE tek kısa Türkçe cümle yaz (\"Kullanıcının adı Emre.\" gibi). " +
                          "Yoksa sadece YOK yaz."),
            new("user", $"Zaten bilinenler:\n{bilinenler}\n\nKullanıcı: {soru}\nAsistan: {cevap}")
        ], ayar.HizliModel, ct);

        var temiz = Temizle(ham);
        if (temiz is null) return;
        if (mevcut.Any(h => string.Equals(h.Icerik, temiz, StringComparison.OrdinalIgnoreCase))) return;

        await hafizalar.EkleAsync(new Hafiza { KullaniciId = kullaniciId, Icerik = temiz }, ct);
    }

    // Model çıktısını güvenli bir hafıza cümlesine çevirir; geçerli değilse null.
    // Hafıza daha sonra system prompt'a girdiği için uzunluk ve satır sonu sınırlanır.
    private static string? Temizle(string ham)
    {
        var t = string.Join(' ', ham.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        t = t.TrimStart('-', '*', ' ').Trim();

        if (t.Length == 0) return null;
        if (t.TrimEnd('.', ' ', '!').Equals("YOK", StringComparison.OrdinalIgnoreCase)) return null;
        if (t.Length > MaksHafizaKarakter) return null;
        return t;
    }

    public Task<IReadOnlyList<Hafiza>> HafizalariGetirAsync(string kullaniciId, CancellationToken ct = default)
        => hafizalar.KullaniciHafizalariAsync(kullaniciId, 100, ct);

    public Task<bool> HafizaSilAsync(string kullaniciId, string hafizaId, CancellationToken ct = default)
        => hafizalar.SilAsync(kullaniciId, hafizaId, ct);

    public Task<long> TumunuSilAsync(string kullaniciId, CancellationToken ct = default)
        => hafizalar.TumunuSilAsync(kullaniciId, ct);
}
