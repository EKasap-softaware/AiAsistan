using Asistan.Application.Kullanicilar;
using Microsoft.AspNetCore.Mvc;

namespace Asistan.Api.Controllers;

[ApiController]
[Route("api/kullanicilar")]
public class KullanicilarController(IKullaniciServisi servis) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Olustur(KullaniciOlusturIstegi istek, CancellationToken ct)
    {
        try
        {
            var k = await servis.OlusturAsync(istek, ct);
            return CreatedAtAction(nameof(Getir), new { id = k.Id }, new { k.Id, k.Ad, k.Email, k.KayitTarihi });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { hata = ex.Message });
        }
        catch (EmailZatenVarException ex)
        {
            return Conflict(new { hata = ex.Message });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Getir(int id, CancellationToken ct)
    {
        var k = await servis.GetirAsync(id, ct);
        return k is null
            ? NotFound()
            : Ok(new { k.Id, k.Ad, k.Email, k.KayitTarihi, Abonelikler = k.Abonelikler.Select(a => a.Plan) });
    }
}
