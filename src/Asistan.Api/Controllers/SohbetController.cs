using Asistan.Application.Sohbet;
using Microsoft.AspNetCore.Mvc;

namespace Asistan.Api.Controllers;

// OturumId verilmezse yeni sohbet başlar.
public record SoruDto(string KullaniciId, string Soru, string? OturumId = null);

[ApiController]
[Route("api/sohbet")]
public class SohbetController(ISohbetServisi servis) : ControllerBase
{
    // Aşama 7: cevap + oturumId döner, soru-cevap MongoDB'ye kaydedilir.
    // Aşama 9'da kuyruğa taşınacak (202 Accepted).
    [HttpPost]
    public async Task<IActionResult> Sor(SoruDto dto, CancellationToken ct)
    {
        try
        {
            var sonuc = await servis.SorAsync(dto.KullaniciId, dto.Soru, dto.OturumId, ct);
            return Ok(new { cevap = sonuc.Cevap, oturumId = sonuc.OturumId });
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

    // Bir oturumun son mesajları (eskiden yeniye).
    [HttpGet("{oturumId}/mesajlar")]
    public async Task<IActionResult> Mesajlar(string oturumId, [FromQuery] int adet = 20, CancellationToken ct = default)
    {
        var liste = await servis.MesajlariGetirAsync(oturumId, adet, ct);
        return Ok(liste.Select(m => new { m.Rol, m.Icerik, m.Olusturulma }));
    }
}
