using System.Drawing;
using System.Text.Json;
using System.Text.RegularExpressions;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Services.WebApi.Models;
using NetCoreServer;

namespace AAEmu.Game.Services.WebApi.Controllers;

// Server-wide GM actions that do NOT need an online executor character.
// Used by the GM web panel so announce/events/kick work even when no GM is in-world.
internal class SystemController : BaseController
{
    [WebApiPost("/api/system/announce")]
    public HttpResponse Announce(HttpRequest request, MatchCollection matches)
    {
        var body = JsonSerializer.Deserialize<JsonElement>(request.Body);
        var text = body.TryGetProperty("text", out var t) ? t.GetString() : null;
        if (string.IsNullOrWhiteSpace(text))
            return BadRequestJson(new ErrorModel("text is required"));
        WorldManager.Instance.BroadcastPacketToServer(new SCNoticeMessagePacket(3, Color.Lime, 0, text));
        return OkJson(new { ok = true, announced = text });
    }

    [WebApiPost("/api/system/snow")]
    public HttpResponse Snow(HttpRequest request, MatchCollection matches)
    {
        var body = JsonSerializer.Deserialize<JsonElement>(request.Body);
        var on = body.TryGetProperty("on", out var o) && (o.ValueKind == JsonValueKind.True || (o.ValueKind == JsonValueKind.String && o.GetString() == "true"));
        WorldManager.Instance.IsSnowing = on;
        WorldManager.Instance.BroadcastPacketToServer(new SCOnOffSnowPacket(on));
        return OkJson(new { ok = true, snowing = on });
    }

    [WebApiPost("/api/system/kick")]
    public HttpResponse Kick(HttpRequest request, MatchCollection matches)
    {
        var body = JsonSerializer.Deserialize<JsonElement>(request.Body);
        var name = body.TryGetProperty("character", out var c) ? c.GetString() : null;
        if (string.IsNullOrWhiteSpace(name))
            return BadRequestJson(new ErrorModel("character is required"));
        var ch = WorldManager.Instance.GetCharacter(name);
        if (ch == null)
            return BadRequestJson(new ErrorModel($"Character \"{name}\" is not online"));
        ch.SendPacket(new SCKickedPacket(KickedReason.KickByGm, name));
        return OkJson(new { ok = true, kicked = name });
    }
}
