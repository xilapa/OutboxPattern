namespace Common;

public sealed class SenderSettings
{
    public string DatabaseConnectionString { get; set; } = null!;
    public string Exchange { get; set; } = null!;
}