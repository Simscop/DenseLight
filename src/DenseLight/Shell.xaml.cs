using DenseLight.BusinessLogic;
using DenseLight.Devices;
using DenseLight.Services;
using DenseLight.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Media.Imaging;

namespace DenseLight
{
    /// <summary>
    /// Shell.xaml 的交互逻辑
    /// </summary>
    public partial class Shell
    {

        private readonly HikCameraService _hikCameraService;

        public Shell()
        {
            InitializeComponent();
            DataContext = App.Current.Services.GetRequiredService<ShellViewModel>();

            //_hikCameraService = App.Current.Services.GetRequiredService<HikCameraService>();
            //_hikCameraService = new HikCameraService();
            //_hikCameraService.ImageReady += OnImageReady;

            //Closed += Shell_Closed;
        }

        //private void Shell_Closed(object? sender, EventArgs e)
        //{
        //    // 窗口关闭时注销事件
        //    _hikCameraService.ImageReady -= OnImageReady;
        //}

        //private void OnImageReady(BitmapSource image)
        //{
        //    var viewModel = (ShellViewModel)DataContext;
        //    viewModel.UpdateImage(image);
        //}


        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Application.Current.Shutdown(); // Ensure the application shuts down when the window is closed
        }
    }
}
