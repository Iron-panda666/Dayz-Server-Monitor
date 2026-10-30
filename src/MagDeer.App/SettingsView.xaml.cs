using System.Windows.Input;

namespace MagDeer.App;

public sealed record SettingsAction(string Name, object Sender, RoutedEventArgs Arguments);

/// <summary>The actual settings surface can be rendered without creating a Window.</summary>
public partial class SettingsView : UserControl
{
    public event EventHandler<SettingsAction>? ActionRequested;
    private sealed record PageChoice(string Label);
    public SettingsView()
    {
        InitializeComponent();
        // Navigation contains labels, not TabItem controls: ComboBox otherwise renders the selected page itself.
        var choices = Sections.Items.Cast<TabItem>().Select(item => new PageChoice(item.Header?.ToString() ?? "")).ToArray();
        Navigation.ItemsSource = CompactNavigation.ItemsSource = choices;
        Sections.SelectedIndex = 0;
    }
    private void ForwardAction(object sender, RoutedEventArgs e)
    { if (sender is FrameworkElement { Tag: string action }) ActionRequested?.Invoke(this, new(action, sender, e)); }
    private void ForwardSelection(object sender, SelectionChangedEventArgs e) => ForwardAction(sender, e);
    private void ForwardMinutes(object sender, KeyEventArgs e) => ActionRequested?.Invoke(this, new("MinutesKeyDown", sender, e));
    private void GoToRestarts(object sender, RoutedEventArgs e) => Sections.SelectedIndex = 1;
    private void ViewSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (NavigationWidth is null) return;
        bool narrow = ActualWidth < 680;
        Root.Margin = new Thickness(narrow ? 12 : 20);
        NavigationWidth.Width = new GridLength(narrow ? 0 : 184); NavigationGap.Width = new GridLength(narrow ? 0 : 20);
        Sidebar.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
        CompactNavigation.Visibility = narrow ? Visibility.Visible : Visibility.Collapsed;
    }
    private void SectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, Sections) || ProfileContext is null) return;
        ProfileContext.Visibility = Sections.SelectedIndex is 0 or 1 ? Visibility.Visible : Visibility.Collapsed;
    }
}
