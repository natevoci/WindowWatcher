using System.Windows;

namespace WindowWatcherAnalyser;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new ViewModels.AnalyserViewModel();
    }
}
