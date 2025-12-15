using System.Windows;

namespace WindowWatcher.Analyser;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new ViewModels.AnalyserViewModel();
    }
}
