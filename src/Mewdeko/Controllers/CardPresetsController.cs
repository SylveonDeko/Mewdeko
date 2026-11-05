using Mewdeko.Common.Cards;
using Mewdeko.Controllers.Common.AuditLog;
using Mewdeko.Controllers.Common.CardPresets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Serves the shared background presets that card designers offer next to uploads.
/// </summary>
/// <param name="presets">The preset service.</param>
[ApiController]
[Route("botapi/[controller]")]
[Authorize("ApiKeyPolicy")]
public class CardPresetsController(CardPresetService presets) : Controller
{
    /// <summary>
    ///     Gets the catalog: the categories and every preset with full image and thumbnail URLs.
    /// </summary>
    [HttpGet]
    [SkipAudit]
    public async Task<IActionResult> Get()
    {
        var catalog = await presets.GetCatalogAsync();
        var baseUrl = presets.BaseUrl;
        return Ok(new CardPresetCatalogResponse
        {
            Categories = catalog.Categories.Select(c => new CardPresetCategoryResponse
            {
                Id = c.Id, Name = c.Name, Order = c.Order
            }).ToList(),
            Presets = catalog.Presets.Select(p => new CardPresetResponse
            {
                Id = p.Id,
                Category = p.Category,
                Name = p.Name,
                Reference = CardPresets.Reference(p.Id),
                ImageUrl = CardPresets.Url(baseUrl, p.Image),
                ThumbUrl = CardPresets.Url(baseUrl, p.Thumb),
                Width = p.Width,
                Height = p.Height,
                Credit = p.Credit
            }).ToList()
        });
    }
}
