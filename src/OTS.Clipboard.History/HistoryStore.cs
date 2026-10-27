using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OTS.Clipboard.History;

/// <summary>Only local, per-Windows-user DPAPI encrypted history. No network calls.</summary>
public sealed class HistoryStore
{
    // This legacy cryptographic entropy is intentionally unchanged so existing history remains decryptable.
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("OhanaTechSolutions:OTSClipboard:history:v1");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    public string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OhanaTechSolutions", "OTSClipboardHistory", "history.dat");

    // The previous app stored its encrypted history here. Keep it intact as a backup.
    private static readonly string LegacyFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OhanaTechSolutions", "OTSClipboard", "history.dat");

    public List<ClipboardEntry> Load()
    {
        var pathToLoad = File.Exists(FilePath) ? FilePath : LegacyFilePath;
        if (!File.Exists(pathToLoad))
            return new List<ClipboardEntry>();

        var encrypted = File.ReadAllBytes(pathToLoad);
        var plaintext = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
        var entries = JsonSerializer.Deserialize<List<ClipboardEntry>>(plaintext, JsonOptions)
                      ?? new List<ClipboardEntry>();
        var validEntries = entries
            .Where(item => item is not null && !string.IsNullOrEmpty(item.Text))
            .OrderByDescending(item => item.CopiedUtc)
            .ToList();

        // Migrate only AFTER a successful decrypt and deserialize. Preserve the original file.
        if (pathToLoad == LegacyFilePath)
            Save(validEntries);

        return validEntries;
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
