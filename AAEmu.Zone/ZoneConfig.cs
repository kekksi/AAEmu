namespace AAEmu.Zone;

public class ZoneConfig
{
    public string GatewayHost { get; set; } = "127.0.0.1";
    public int GatewayPort { get; set; } = 1300;
    public string SecretKey { get; set; } = "test";
    public uint ZoneId { get; set; } = 1;
    public uint WorldTemplateId { get; set; } = 1;
}
