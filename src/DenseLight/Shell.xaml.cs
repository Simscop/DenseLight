using DenseLight.BusinessLogic;
using DenseLight.Services;
using DenseLight.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace DenseLight
{
    /// <summary>
    /// Shell.xaml 的交互逻辑
    /// </summary>
    public partial class Shell
    {
        
        //private readonly ShellViewModel _viewModel;

        public Shell()
        {
            InitializeComponent();
            DataContext = App.Current.Services.GetRequiredService<ShellViewModel>();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Application.Current.Shutdown(); // Ensure the application shuts down when the window is closed
        }
    }
}
