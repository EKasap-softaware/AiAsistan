using Asistan.Application.Kuyruk;
using Microsoft.AspNetCore.Mvc;

namespace Asistan.Api.Controllers;

// "Siparişim hazır mı?" ucu. İstemci bunu belirli aralıklarla sorar (polling), durum Tamamlandi olunca cevabı alır.
[ApiController]
[Route("api/isler")]
public class IslerController(IIsServisi isler) : ControllerBase
{
    [HttpGet("{isId}")]
    public async Task<IActionResult> Getir(string isId, CancellationToken ct)
    {
        var i = await isler.GetirAsync(isId, ct);
        if (i is null) return NotFound();

        // Tamamlanmışsa cevap, hatalıysa hata alanı dolu gelir; bekleyen işte ikisi de null.
        return Ok(new
        {
            isId = i.Id,
            durum = i.Durum.ToString(),
            oturumId = i.OturumId,
            mod = i.Mod,
            soru = i.Soru,
            cevap = i.Cevap,
            hata = i.Hata,
            denemeSayisi = i.DenemeSayisi,
            olusturulma = i.Olusturulma,
            guncellenme = i.Guncellenme
        });
    }
}
