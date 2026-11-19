namespace Asistan.Application.Sohbet;

public interface ISohbetServisi
{
    Task<string> SorAsync(string kullaniciId, string soru, CancellationToken ct = default);
}

public class SohbetServisi(ILlmIstemcisi llm) : ISohbetServisi
{
    public const string SistemPrompt = "Sen bir .NET eğitmenisin. Türkçe, kısa ve net cevap ver.";

    public async Task<string> SorAsync(string kullaniciId, string soru, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(soru))
            throw new ArgumentException("Soru boş olamaz.", nameof(soru));

        var mesajlar = new List<LlmMesaj>
        {
            new("system", SistemPrompt),
            new("user", soru.Trim())
        };

        return await llm.TamamlaAsync(mesajlar, ct: ct);
    }
}
