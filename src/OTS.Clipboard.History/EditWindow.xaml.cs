using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace OTS.Clipboard.History;

public partial class EditWindow : Window
{
    public string EditedText => Editor.Text;

    public EditWindow(string originalText)
    {
        InitializeComponent();
        Editor.Text = originalText;
        Loaded += (_, _) => { Editor.Focus(); Editor.CaretIndex = Editor.Text.Length; };
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Editor.Text))
        {
            MessageBox.Show(this, "An entry cannot be empty.", "OTS Clipboard History",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
