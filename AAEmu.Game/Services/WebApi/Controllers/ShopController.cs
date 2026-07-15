using AAEmu.Game.Core.Managers;
using NetCoreServer;

namespace AAEmu.Game.Services.WebApi.Controllers;

/// <summary>
/// Cash shop management endpoints for the WebApi. No character required,
/// so the GM panel can reload the shop headless (no server restart, no kick).
/// </summary>
internal class ShopController : BaseController
{
    [WebApiPost("/api/shop/reload")]
    public HttpResponse ReloadShop(HttpRequest request)
    {
        CashShopManager.Instance.DisableShop();
        CashShopManager.Instance.Load();
        CashShopManager.Instance.EnabledShop();

        var result = new
        {
            reloaded = true,
            shopItems = CashShopManager.Instance.ShopItems.Count,
            menuEntries = CashShopManager.Instance.MenuItems.Count,
            skus = CashShopManager.Instance.SKUs.Count,
            enabled = CashShopManager.Instance.Enabled
        };
        return OkJson(result);
    }
}
