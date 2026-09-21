namespace Kable.UI.Wpf;

using System.Windows.Controls;
using Kable.UI.Wpf.ViewModels;

public partial class CommTerminalView : UserControl
{
    public CommTerminalView()
    {
        InitializeComponent();
    }

    public CommTerminalView(CommTerminalViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
