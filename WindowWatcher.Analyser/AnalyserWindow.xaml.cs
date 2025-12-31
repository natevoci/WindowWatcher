using System.Windows;

namespace WindowWatcher.Analyser;

public partial class AnalyserWindow : Window
{
    public AnalyserWindow()
    {
        InitializeComponent();
        DataContext = new ViewModels.AnalyserViewModel();
    }
}
