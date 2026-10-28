using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using MessageBox = System.Windows.MessageBox;
using MenuItem = System.Windows.Controls.MenuItem;
using DataGridRow = System.Windows.Controls.DataGridRow;
using Color = System.Windows.Media.Color;

namespace OTS.Clipboard.History;

public partial class MainWindow : Window
{
    private const int WmClipboardUpdate = 0x031D;
    private const int MaxCopiedTextLength = 2_000_000;
    private const int MaxImageBytes = 5 * 1024 * 1024;
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "OTSClipboardHistory";
    // Preserve previous users' optional startup preference during the rename.
    private const string LegacyRunValueName = "OTSClipboard";

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();

    private readonly HistoryStore _store = new();
    private readonly ObservableCollection<ClipboardEntry> _history = new();
    private readonly System.Windows.Forms.NotifyIcon _trayIcon;
    private readonly System.Drawing.Icon _trayIconImage;
    private HwndSource? _source;
    private IntPtr _hwnd;
    private ClipboardEntry? _contextItem;
    private uint _lastSelfCopySequence;
    private bool _exitRequested;
    private bool _tracking = true;
    private bool _storageAvailable = true;
    private ICollectionView? _view;

    public MainWindow()
    {
        InitializeComponent();
        _view = CollectionViewSource.GetDefaultView(_history);
        _view.Filter = FilterEntry;
        HistoryGrid.ItemsSource = _view;
        try
        {
            foreach (var entry in _store.Load())
                _history.Add(entry);
        }
        catch (Exception ex)
        {
            _storageAvailable = false;
            MessageBox.Show(this,
                "Saved history could not be decrypted or read. Existing history will NOT be overwritten.\n\n" +
                ex.Message + "\n\nCheck the local history file or Windows account before restarting.",
                "OTS Clipboard History · History warning",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        AutoStartCheckBox.IsChecked = ReadAutoStart();
        RefreshCounts();
        StatusText.Text = _storageAvailable
            ? "Monitoring text copied in Windows · history is encrypted locally"
            : "History unavailable · recording is disabled to protect your saved data";
        if (!_storageAvailable)
        {
            _tracking = false;
            TrackingCheckBox.IsChecked = false;
            UpdateTrackingLabel();
        }

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Show / Hide OTS Clipboard History", null, (_, _) => Dispatcher.BeginInvoke(new Action(ToggleWindowVisibility)));
        menu.Items.Add("Exit OTS Clipboard History", null, (_, _) => ExitApplication());
        _trayIconImage = LoadTrayIcon();
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = _trayIconImage,
            Text = "OTS Clipboard History - click to show/hide",
            Visible = true,
            ContextMenuStrip = menu
        };
        // A single left-click toggles the window. Right-click opens the exit menu.
        _trayIcon.MouseClick += (_, e) =>
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
                Dispatcher.BeginInvoke(new Action(ToggleWindowVisibility));
        };
        Loaded += (_, _) => _trayIcon.Visible = true;
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized && !_exitRequested)
                HideToTray();
        };
    }

    private static System.Drawing.Icon LoadTrayIcon()
    {
        // WPF embeds the exact same icon resource used by our window and executable.
        // Clone the native icon so the WPF resource stream can be safely disposed.
        var resource = System.Windows.Application.GetResourceStream(
            new Uri("pack://application:,,,/Resources/App.ico", UriKind.Absolute))
            ?? throw new InvalidOperationException("The embedded OTS Clipboard History icon was not found.");

        using var stream = resource.Stream;
        using var icon = new System.Drawing.Icon(stream);
        return (System.Drawing.Icon)icon.Clone();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);
        if (!AddClipboardFormatListener(_hwnd))
        {
            _tracking = false;
            TrackingCheckBox.IsChecked = false;
            UpdateTrackingLabel();
            StatusText.Text = "Windows clipboard notifications could not be enabled.";
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmClipboardUpdate && _tracking && _storageAvailable)
        {
            var sequence = GetClipboardSequenceNumber();
            if (sequence == 0 || sequence != _lastSelfCopySequence)
                _ = ReadCopiedTextAsync();
        }
        return IntPtr.Zero;
    }

    private async Task ReadCopiedTextAsync()
    {
        // Other applications can briefly lock the clipboard. Retry non-blockingly.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (!_tracking || !_storageAvailable) return;

                if (System.Windows.Clipboard.ContainsImage())
                {
                    var image = System.Windows.Clipboard.GetImage();
                    if (image is null) return;
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(image));
                    using var stream = new MemoryStream();
                    encoder.Save(stream);
                    if (stream.Length > MaxImageBytes)
                    {
                        StatusText.Text = "Image skipped (maximum 5 MB per clip).";
                        return;
                    }
                    var png = Convert.ToBase64String(stream.ToArray());
                    if (_history.Count > 0 && _history[0].ImagePngBase64 == png)
                        return;
                    _history.Insert(0, new ClipboardEntry { ImagePngBase64 = png });
                    Persist();
                    RefreshCounts();
                    return;
                }

                if (!System.Windows.Clipboard.ContainsText()) return;
                var text = System.Windows.Clipboard.GetText();
                if (string.IsNullOrEmpty(text))
                    return;
                if (text.Length > MaxCopiedTextLength)
                {
                    StatusText.Text = "Very large clipboard text skipped (2 million character safety limit).";
                    return;
                }
                if (_history.Count > 0 && string.Equals(_history[0].Text, text, StringComparison.Ordinal))
                    return; // Ignore consecutive identical copies, preserving original timestamp.

                _history.Insert(0, new ClipboardEntry { Text = text, CopiedUtc = DateTimeOffset.UtcNow });
                Persist();
                RefreshCounts();
                return;
            }
            catch (COMException)
            {
                // Clipboard is in use.
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                // Clipboard is in use.
            }
            catch (Exception ex)
            {
                StatusText.Text = "Clipboard read error: " + ex.Message;
                return;
            }
            await Task.Delay(65 * (attempt + 1));
        }
    }

    private void CopyEntry(ClipboardEntry entry)
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                if (entry.IsImage)
                {
                    using var stream = new MemoryStream(Convert.FromBase64String(entry.ImagePngBase64!));
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    System.Windows.Clipboard.SetImage(bitmap);
                }
                else System.Windows.Clipboard.SetText(entry.Text);
                _lastSelfCopySequence = GetClipboardSequenceNumber();
                StatusText.Text = "Copied selected entry back to Windows clipboard";
                return;
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                System.Threading.Thread.Sleep(45);
            }
        }
        StatusText.Text = "Clipboard is busy. Try copying again.";
    }

    private void HistoryGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        // Clicking a selection checkbox should NEVER copy the clip again.
        if (IsInsideCheckBox(source)) return;

        _contextItem = null;
        var row = GetRow(source);
        if (row?.Item is ClipboardEntry entry &&
            (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == ModifierKeys.None)
            CopyEntry(entry);
    }

    private static bool IsInsideCheckBox(DependencyObject? source)
    {
        while (source is not null && source is not DataGridRow)
        {
            if (source is CheckBox) return true;
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }

    private void ClipCheckbox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Intercept before DataGrid's click-to-select handler, so checking a second
        // row does not clear the first selection and does not copy its contents.
        if (sender is not CheckBox box) return;
        var row = GetRow(box);
        if (row?.Item is not ClipboardEntry entry) return;

        _contextItem = null;
        if (HistoryGrid.SelectedItems.Contains(entry))
            HistoryGrid.SelectedItems.Remove(entry);
        else
            HistoryGrid.SelectedItems.Add(entry);

        e.Handled = true;
    }

    private void HistoryGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedCountText is null) return;
        var count = HistoryGrid.SelectedItems.Count;
        SelectedCountText.Text = count == 1 ? "1 selected" : $"{count:N0} selected";
        SelectedCountText.Foreground = new SolidColorBrush(
            count > 0 ? Color.FromRgb(180, 212, 245) : Color.FromRgb(139, 168, 205));
    }

    private void HistoryGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var row = GetRow(e.OriginalSource as DependencyObject);
        _contextItem = row?.Item as ClipboardEntry;
        if (row is not null && !row.IsSelected)
        {
            HistoryGrid.SelectedItems.Clear();
            row.IsSelected = true;
        }
    }

    private static DataGridRow? GetRow(DependencyObject? source)
    {
        while (source is not null && source is not DataGridRow)
            source = VisualTreeHelper.GetParent(source);
        return source as DataGridRow;
    }

    private ClipboardEntry? GetActionEntry()
        => _contextItem ?? HistoryGrid.SelectedItem as ClipboardEntry;

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (GetActionEntry() is { } entry)
            CopyEntry(entry);
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (GetActionEntry() is not { } entry)
            return;
        if (entry.IsImage)
        {
            StatusText.Text = "Image clips can be copied or deleted; editing is text-only.";
            return;
        }
        var dialog = new EditWindow(entry.Text) { Owner = this };
        if (dialog.ShowDialog() != true)
            return;
        entry.Text = dialog.EditedText;
        _view?.Refresh();
        RefreshCounts();
        Persist();
        StatusText.Text = "Saved edited entry";
    }

    private void ToggleSelection_Click(object sender, RoutedEventArgs e)
    {
        if (GetActionEntry() is not { } entry)
            return;
        if (HistoryGrid.SelectedItems.Contains(entry))
            HistoryGrid.SelectedItems.Remove(entry);
        else
            HistoryGrid.SelectedItems.Add(entry);
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e) => HistoryGrid.SelectAll();
    private void DeselectAll_Click(object sender, RoutedEventArgs e) => HistoryGrid.UnselectAll();

    private void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = HistoryGrid.SelectedItems.Cast<ClipboardEntry>().ToArray();
        if (selected.Length == 0 && GetActionEntry() is { } context)
            selected = new[] { context };
        if (selected.Length == 0)
            return;
        if (selected.Length > 1 && MessageBox.Show(this,
                "Delete " + selected.Length + " selected history entries?", "Confirm deletion",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        foreach (var entry in selected)
            _history.Remove(entry);
        _contextItem = null;
        Persist();
        RefreshCounts();
        StatusText.Text = "Deleted " + selected.Length + " history entr" +
                          (selected.Length == 1 ? "y" : "ies");
    }

    private void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = ClearHistoryButton.ContextMenu;
        if (menu is null)
            return;
        menu.PlacementTarget = ClearHistoryButton;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void ClearRange_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item || item.Tag is not string range)
            return;

        var now = DateTimeOffset.UtcNow;
        DateTimeOffset cutoff = range switch
        {
            "15m" => now.AddMinutes(-15),
            "30m" => now.AddMinutes(-30),
            "1h" => now.AddHours(-1),
            "2h" => now.AddHours(-2),
            "3h" => now.AddHours(-3),
            "4h" => now.AddHours(-4),
            "5h" => now.AddHours(-5),
            "6h" => now.AddHours(-6),
            "12h" => now.AddHours(-12),
            "24h" => now.AddHours(-24),
            "48h" => now.AddHours(-48),
            "72h" => now.AddHours(-72),
            "1w" => now.AddDays(-7),
            "1mo" => DateTimeOffset.Now.AddMonths(-1).ToUniversalTime(),
            "1y" => DateTimeOffset.Now.AddYears(-1).ToUniversalTime(),
            "all" => DateTimeOffset.MinValue,
            _ => now
        };

        var remove = range == "all"
            ? _history.ToArray()
            : _history.Where(entry => entry.CopiedUtc >= cutoff).ToArray();
        if (remove.Length == 0)
        {
            StatusText.Text = "No history entries found in that time range";
            return;
        }

        var prompt = "Permanently delete " + remove.Length + " history entr" +
                     (remove.Length == 1 ? "y" : "ies") + " from " + item.Header + "?";
        if (MessageBox.Show(this, prompt, "Clear clipboard history",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        foreach (var entry in remove)
            _history.Remove(entry);
        _contextItem = null;
        Persist();
        RefreshCounts();
        StatusText.Text = "Cleared " + remove.Length + " history entr" +
                          (remove.Length == 1 ? "y" : "ies");
    }

    private void Tracking_Click(object sender, RoutedEventArgs e)
    {
        _tracking = TrackingCheckBox.IsChecked == true && _storageAvailable;
        UpdateTrackingLabel();
        StatusText.Text = _tracking ? "Tracking resumed" : "Tracking paused";
    }

    private void UpdateTrackingLabel()
    {
        RecordingLabel.Text = _tracking ? "Recording" : "Paused";
        RecordingDot.Fill = new SolidColorBrush(_tracking
            ? Color.FromRgb(59, 201, 159)
            : Color.FromRgb(241, 171, 85));
    }

    private void AutoStart_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (AutoStartCheckBox.IsChecked == true)
            {
                var path = Environment.ProcessPath
                           ?? throw new InvalidOperationException("Application executable path not found.");
                key.SetValue(RunValueName, "\"" + path + "\"");
                key.DeleteValue(LegacyRunValueName, throwOnMissingValue: false);
                StatusText.Text = "OTS Clipboard History will start when you sign in to Windows";
            }
            else
            {
                key.DeleteValue(RunValueName, throwOnMissingValue: false);
                key.DeleteValue(LegacyRunValueName, throwOnMissingValue: false);
                StatusText.Text = "Start with Windows disabled";
            }
        }
        catch (Exception ex)
        {
            AutoStartCheckBox.IsChecked = ReadAutoStart();
            MessageBox.Show(this, "Could not change startup setting:\n" + ex.Message,
                "OTS Clipboard History", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static bool ReadAutoStart()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key.GetValue(RunValueName) is string existing && !string.IsNullOrWhiteSpace(existing))
                return true;

            if (key.GetValue(LegacyRunValueName) is string old && !string.IsNullOrWhiteSpace(old))
            {
                // Replace the old startup path with the renamed executable (no duplicate launches).
                var path = Environment.ProcessPath;
                if (!string.IsNullOrWhiteSpace(path))
                {
                    key.SetValue(RunValueName, "\"" + path + "\"");
                    key.DeleteValue(LegacyRunValueName, throwOnMissingValue: false);
                    return true;
                }
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private void Persist()
    {
        if (!_storageAvailable)
            return;
        try
        {
            _store.Save(_history);
        }
        catch (Exception ex)
        {
            _storageAvailable = false;
            _tracking = false;
            TrackingCheckBox.IsChecked = false;
            UpdateTrackingLabel();
            StatusText.Text = "Storage failed; tracking paused to prevent losing data";
            MessageBox.Show(this, "Could not safely save encrypted history.\n\n" + ex.Message,
                "OTS Clipboard History", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshCounts()
    {
        CountText.Text = _history.Count.ToString("N0") + " saved clips";
        if (EmptyState is not null)
            EmptyState.Visibility = _view is null || _view.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        if (SelectedCountText is not null)
        {
            var selected = HistoryGrid.SelectedItems.Count;
            SelectedCountText.Text = selected == 1 ? "1 selected" : $"{selected:N0} selected";
        }
    }

    private bool FilterEntry(object candidate)
    {
        if (candidate is not ClipboardEntry entry) return false;
        var query = SearchBox?.Text?.Trim() ?? "";
        if (query.Length > 0 &&
            !(entry.Text.Contains(query, StringComparison.OrdinalIgnoreCase) ||
              entry.Kind.Contains(query, StringComparison.OrdinalIgnoreCase)))
            return false;
        var category = (CategoryFilter?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "All";
        return category == "All" || entry.Kind == category;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_view is null || CountText is null) return;
        _view.Refresh();
        RefreshCounts();
    }

    private void CategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_view is null || CountText is null) return;
        _view.Refresh();
        RefreshCounts();
    }

    private void RestoreWindow()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ToggleWindowVisibility()
    {
        if (IsVisible && WindowState != WindowState.Minimized)
            HideToTray();
        else
            RestoreWindow();
    }

    private void HideToTray()
    {
        ShowInTaskbar = false;
        Hide();
    }

    private void ExitApplication()
    {
        _exitRequested = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_exitRequested)
        {
            e.Cancel = true;
            HideToTray();
            StatusText.Text = "Running in the tray. Click its icon to show or hide.";
            return;
        }

        if (_hwnd != IntPtr.Zero)
            RemoveClipboardFormatListener(_hwnd);
        _source?.RemoveHook(WndProc);
        _trayIcon.Visible = false;
        _trayIcon.ContextMenuStrip?.Dispose();
        _trayIcon.Dispose();
        _trayIconImage.Dispose();
        base.OnClosing(e);
    }
}
