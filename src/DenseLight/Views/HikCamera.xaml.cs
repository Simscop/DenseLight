using DenseLight.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DenseLight.Views
{
    /// <summary>
    /// HikCamera.xaml 的交互逻辑
    /// </summary>
    public partial class HikCamera : UserControl
    {
        private readonly IServiceScope _scope;
        public HikCamera()
        {
            InitializeComponent();

            _scope = App.Current.Services.CreateScope();

            DataContext = _scope.ServiceProvider.GetRequiredService<CameraViewModel>();


        }
    }
}
