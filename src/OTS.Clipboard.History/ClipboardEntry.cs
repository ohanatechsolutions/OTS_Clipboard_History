using System.IO;
using System.Text.Json.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Text.RegularExpressions;

namespace OTS.Clipboard.History;

public sealed class ClipboardEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = string.Empty;
    // Older encrypted JSON entries have no image property; they remain readable.
    public string? ImagePngBase64 { get; set; }
    public DateTimeOffset CopiedUtc { get; set; } = DateTimeOffset.UtcNow;

    [JsonIgnore] public bool IsImage => !string.IsNullOrEmpty(ImagePngBase64);
    [JsonIgnore] public bool IsColor => !IsImage && Regex.IsMatch(Text.Trim(), @"^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$");
    [JsonIgnore] public bool IsLink => !IsImage && Uri.TryCreate(Text.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    [JsonIgnore] public bool IsCode => !IsImage && !IsLink && !IsColor &&
        (Text.Contains("using System;", StringComparison.Ordinal) ||
         (Text.Contains('\n') && (Text.Contains('{') || Text.Contains("=>", StringComparison.Ordinal) ||
                                  Text.Contains("</", StringComparison.Ordinal))));
    [JsonIgnore] public string Kind => IsImage ? "Image" : IsColor ? "Color" : IsLink ? "Links" : IsCode ? "Code" : "Text";

    [JsonIgnore]
    public string Preview
    {
        get
        {
            if (IsImage) return "Image clip · click to copy";
            var text = Text.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Trim();
            return text.Length > 280 ? text[..280] + "…" : text;
        }
    }

    [JsonIgnore]
    public Brush SwatchBrush
    {
        get
        {
            try
            {
                if (IsColor && ColorConverter.ConvertFromString(Text.Trim()) is Color color)
                {
                    var brush = new SolidColorBrush(color);
                    brush.Freeze();
                    return brush;
                }
            }
            catch (FormatException) { }
            return Brushes.Transparent;
        }
    }

    private BitmapSource? _thumbnail;
    [JsonIgnore]
    public BitmapSource? ImagePreview
    {
        get
        {
            if (!IsImage) return null;
            if (_thumbnail is not null) return _thumbnail;
            try
            {
                using var stream = new MemoryStream(Convert.FromBase64String(ImagePngBase64!));
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.DecodePixelWidth = 220;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                _thumbnail = image;
            }
            catch (Exception) { return null; }
            return _thumbnail;
        }
    }

    [JsonIgnore] public string LocalTimestamp => CopiedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
}
