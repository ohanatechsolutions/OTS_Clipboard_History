using System.Text.Json.Serialization;

namespace OTS.Clipboard.History;

public sealed class ClipboardEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = string.Empty;
    public DateTimeOffset CopiedUtc { get; set; } = DateTimeOffset.UtcNow;

    [JsonIgnore]
    public string Preview
    {
        get
        {
            var oneLine = Text.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
            return oneLine.Length > 450 ? oneLine[..450] + "…" : oneLine;
        }
    }

    [JsonIgnore]
    public string LocalTimestamp => CopiedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
}
