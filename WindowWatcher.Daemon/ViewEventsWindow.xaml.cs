using System.Windows;

namespace WindowWatcher.Daemon;

public partial class ViewEventsWindow : Window
{
    public ViewEventsWindow(ViewEventsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
