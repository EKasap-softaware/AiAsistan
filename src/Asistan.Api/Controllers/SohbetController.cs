using Asistan.Application.Kuyruk;
using Asistan.Application.Sohbet;
using Microsoft.AspNetCore.Mvc;

namespace Asistan.Api.Controllers;

// OturumId verilmezse yeni sohbet başlar.
public record SoruDto(string KullaniciId, string Soru, string? OturumId = null);

[ApiController]
[Route("api/sohbet")]
public class SohbetController(IIsServisi isler, ISohbetServisi sohbet) : ControllerBase
{
    // Aşama 9: soru kuyruğa bırakılır, API HEMEN döner (202 Accepted). Cevap Worker'dan gelir.
    // Kullanıcı durumUrl'e (GET /api/isler/{isId}) bakarak cevabı alır.
    [HttpPost]
    public async Task<IActionResult> Sor(SoruDto dto, CancellationToken ct)
    {
        try
        {
            var is_ = await isler.KuyrugaAlAsync(dto.KullaniciId, dto.Soru, dto.OturumId, ct);
            var durumUrl = $"/api/isler/{is_.Id}";
            return Accepted(durumUrl, new { isId = is_.Id, oturumId = is_.OturumId, durum = is_.Durum.ToString(), durumUrl });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { hata = ex.Message });
        }
        catch (KuyrukHatasi)
        {
            // RabbitMQ'ya ulaşılamadı: sunucu tarafı geçici sorun (503), istemci hatası değil.
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { hata = "Soru şu an kuyruğa alınamadı, biraz sonra tekrar dene." });
        }
    }

    // Bir oturumun son mesajları (eskiden yeniye).
    [HttpGet("{oturumId}/mesajlar")]
    public async Task<IActionResult> Mesajlar(string oturumId, [FromQuery] int adet = 20, CancellationToken ct = default)
    {
        var liste = await sohbet.MesajlariGetirAsync(oturumId, adet, ct);
        return Ok(liste.Select(m => new { m.Rol, m.Icerik, m.Olusturulma }));
    }
}
