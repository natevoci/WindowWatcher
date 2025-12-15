using System.Windows;

namespace WindowWatcherDaemon;

public partial class ViewEventsWindow : Window
{
    public ViewEventsWindow(ViewEventsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
