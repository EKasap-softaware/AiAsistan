using Asistan.Application.Sohbet;
using Microsoft.AspNetCore.Mvc;

namespace Asistan.Api.Controllers;

public record SoruDto(string KullaniciId, string Soru);

[ApiController]
[Route("api/sohbet")]
public class SohbetController(ISohbetServisi servis) : ControllerBase
{
    // Aşama 6: senkron versiyon. Aşama 9'da kuyruğa taşınacak (202 Accepted).
    [HttpPost]
    public async Task<IActionResult> Sor(SoruDto dto, CancellationToken ct)
    {
        try
        {
            var cevap = await servis.SorAsync(dto.KullaniciId, dto.Soru, ct);
            return Ok(new { cevap });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { hata = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { hata = "LLM'e ulaşılamadı: " + ex.Message });
        }
    }
}
