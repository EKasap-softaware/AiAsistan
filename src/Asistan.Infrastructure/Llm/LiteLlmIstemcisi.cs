using System.Net.Http.Json;
using System.Text.Json;
using Asistan.Application.Sohbet;

namespace Asistan.Infrastructure.Llm;

// LiteLLM OpenAI formatı konuşur: POST /v1/chat/completions
public class LiteLlmIstemcisi(HttpClient http) : ILlmIstemcisi
{
    public async Task<string> TamamlaAsync(IEnumerable<LlmMesaj> mesajlar, string model = "akilli", CancellationToken ct = default)
    {
        var istek = new
        {
            model,
            messages = mesajlar.Select(m => new { role = m.Rol, content = m.Icerik })
        };

        var yanit = await http.PostAsJsonAsync("/v1/chat/completions", istek, ct);
        yanit.EnsureSuccessStatusCode();

        var json = await yanit.Content.ReadFromJsonAsync<JsonElement>(ct);
        return json.GetProperty("choices")[0]
                   .GetProperty("message")
                   .GetProperty("content")
                   .GetString()!;
    }
}
