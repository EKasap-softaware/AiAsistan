namespace Asistan.Application.Sohbet;

// Rol: "system" | "user" | "assistant"
public record LlmMesaj(string Rol, string Icerik);

// Application "modele sor" der; LiteLLM'i, Claude'u, HTTP'yi bilmez.
public interface ILlmIstemcisi
{
    Task<string> TamamlaAsync(IEnumerable<LlmMesaj> mesajlar, string model = "akilli", CancellationToken ct = default);
}
