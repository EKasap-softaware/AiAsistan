using System.Net.Http.Json;
using System.Text.Json;
using Asistan.Application.Dokuman;

namespace Asistan.Infrastructure.LangFlow;

// Hangi LangFlow akışı çağrılır? (endpoint adı; LangFlow'da akışa verilen kısa ad)
public record LangFlowAyarlari(string Akis);

// ILangFlowIstemcisi'nin gerçek hali. LangFlow her akışı bir REST ucu yapar: POST /api/v1/run/{akis}.
// Adres ve x-api-key başlığı AddInfrastructure'daki typed HttpClient'ta ayarlanır.
public class LangFlowIstemcisi(HttpClient http, LangFlowAyarlari ayar) : ILangFlowIstemcisi
{
    public async Task<string> CalistirAsync(string soru, string oturumId, CancellationToken ct = default)
    {
        var govde = new
        {
            input_value = soru,       // Chat Input bileşenine giden metin
            input_type = "chat",
            output_type = "chat",
            session_id = oturumId
        };

        var yanit = await http.PostAsJsonAsync($"/api/v1/run/{ayar.Akis}?stream=false", govde, ct);
        yanit.EnsureSuccessStatusCode();   // 4xx/5xx → HttpRequestException (Worker bunu yeniden dener)

        var json = await yanit.Content.ReadFromJsonAsync<JsonElement>(ct);
        return Cevabi(json);
    }

    // Yanıt biçimi LangFlow sürümüne göre değişebilir (rehber de uyarıyor). Yol: outputs[0].outputs[0].results.message.text
    // Beklenen biçim gelmezse sessizce boş cevap dönmek yerine NE OLDUĞUNU söyleyen bir hata veririz.
    internal static string Cevabi(JsonElement json)
    {
        try
        {
            var metin = json.GetProperty("outputs")[0]
                            .GetProperty("outputs")[0]
                            .GetProperty("results")
                            .GetProperty("message")
                            .GetProperty("text")
                            .GetString();

            if (!string.IsNullOrWhiteSpace(metin)) return metin;
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            throw new InvalidOperationException("LangFlow yanıtı beklenen biçimde değil (outputs[0].outputs[0].results.message.text bulunamadı).", ex);
        }

        throw new InvalidOperationException("LangFlow boş bir cevap döndürdü.");
    }
}
