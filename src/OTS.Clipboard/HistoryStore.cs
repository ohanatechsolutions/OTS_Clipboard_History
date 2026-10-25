using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OTS.Clipboard;

/// <summary>Only local, per-Windows-user DPAPI encrypted history. No network calls.</summary>
public sealed class HistoryStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("OhanaTechSolutions:OTSClipboard:history:v1");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    public string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OhanaTechSolutions", "OTSClipboard", "history.dat");

    public List<ClipboardEntry> Load()
    {
        if (!File.Exists(FilePath))
            return new List<ClipboardEntry>();

        var encrypted = File.ReadAllBytes(FilePath);
        var plaintext = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
        var entries = JsonSerializer.Deserialize<List<ClipboardEntry>>(plaintext, JsonOptions)
                      ?? new List<ClipboardEntry>();
        return entries
            .Where(item => item is not null && !string.IsNullOrEmpty(item.Text))
            .OrderByDescending(item => item.CopiedUtc)
            .ToList();
    }

    public void Save(IEnumerable<ClipboardEntry> entries)
    {
        var directory = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(directory);
        var data = JsonSerializer.SerializeToUtf8Bytes(entries.ToArray(), JsonOptions);
        var encrypted = ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);
        var tempPath = FilePath + ".tmp";

        try
        {
            File.WriteAllBytes(tempPath, encrypted);
            File.Move(tempPath, FilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}
