using System.Collections.ObjectModel;
using System.ComponentModel;
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

namespace OTS.Clipboard;

public partial class MainWindow : Window
{
    private const int WmClipboardUpdate = 0x031D;
    private const int MaxCopiedTextLength = 2_000_000;
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "OTSClipboard";

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
    private HwndSource? _source;
    private IntPtr _hwnd;
    private ClipboardEntry? _contextItem;
    private uint _lastSelfCopySequence;
    private bool _exitRequested;
    private bool _tracking = true;
    private bool _storageAvailable = true;

    public MainWindow()
    {
        InitializeComponent();
        HistoryGrid.ItemsSource = _history;
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
                "OTS Clipboard · History warning",
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
        menu.Items.Add("Open OTS Clipboard", null, (_, _) => RestoreWindow());
        menu.Items.Add("Exit OTS Clipboard", null, (_, _) => ExitApplication());
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "OTS Clipboard",
            Visible = true,
            ContextMenuStrip = menu
        };
        _trayIcon.DoubleClick += (_, _) => RestoreWindow();
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized && !_exitRequested)
                Hide();
        };
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
                if (!_tracking || !_storageAvailable || !System.Windows.Clipboard.ContainsText())
                    return;

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
                System.Windows.Clipboard.SetText(entry.Text);
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
        var row = GetRow(e.OriginalSource as DependencyObject);
        if (row?.Item is ClipboardEntry entry)
            CopyEntry(entry);
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
        var dialog = new EditWindow(entry.Text) { Owner = this };
        if (dialog.ShowDialog() != true)
            return;
        entry.Text = dialog.EditedText;
        HistoryGrid.Items.Refresh();
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
                StatusText.Text = "OTS Clipboard will start when you sign in to Windows";
            }
            else
            {
                key.DeleteValue(RunValueName, throwOnMissingValue: false);
                StatusText.Text = "Start with Windows disabled";
            }
        }
        catch (Exception ex)
        {
            AutoStartCheckBox.IsChecked = ReadAutoStart();
            MessageBox.Show(this, "Could not change startup setting:\n" + ex.Message,
                "OTS Clipboard", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static bool ReadAutoStart()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(RunValueName) is string value && !string.IsNullOrWhiteSpace(value);
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
                "OTS Clipboard", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshCounts()
        => CountText.Text = _history.Count.ToString("N0") + " saved entries";

    private void RestoreWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
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
            Hide();
            StatusText.Text = "Still recording in system tray. Right-click tray icon to exit.";
            return;
        }

        if (_hwnd != IntPtr.Zero)
            RemoveClipboardFormatListener(_hwnd);
        _source?.RemoveHook(WndProc);
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        base.OnClosing(e);
    }
}
