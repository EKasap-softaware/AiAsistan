using Asistan.Application.Bellek;
using Microsoft.AspNetCore.Mvc;

namespace Asistan.Api.Controllers;

// Kullanıcının "görme ve silme hakkı" (KVKK/GDPR): asistan hakkında ne hatırlıyorsa buradan görülür ve silinir.
// UYARI: Henüz kimlik doğrulama yok; kullaniciId'yi bilen herkes bu uçları kullanabilir.
[ApiController]
[Route("api/hafiza")]
public class HafizaController(IHafizaServisi servis) : ControllerBase
{
    [HttpGet("{kullaniciId}")]
    public async Task<IActionResult> Listele(string kullaniciId, CancellationToken ct)
    {
        var liste = await servis.HafizalariGetirAsync(kullaniciId, ct);
        return Ok(liste.Select(h => new { h.Id, h.Icerik, h.Olusturulma }));
    }

    [HttpDelete("{kullaniciId}/{hafizaId}")]
    public async Task<IActionResult> Sil(string kullaniciId, string hafizaId, CancellationToken ct)
        => await servis.HafizaSilAsync(kullaniciId, hafizaId, ct) ? NoContent() : NotFound();

    [HttpDelete("{kullaniciId}")]
    public async Task<IActionResult> TumunuSil(string kullaniciId, CancellationToken ct)
        => Ok(new { silinen = await servis.TumunuSilAsync(kullaniciId, ct) });
}
