using DenseLight.BusinessLogic;
using DenseLight.Services;
using DenseLight.ViewModels;
using System.Windows;

namespace DenseLight
{
    /// <summary>
    /// Shell.xaml 的交互逻辑
    /// </summary>
    public partial class Shell
    {

        private readonly IMotor _motor;
        private readonly ICameraService _cameraService;
        private readonly AutoFocusService _autoFocusService;
        private readonly ILoggerService _logger;
        private readonly IImageProcessingService _imageProcessingService;
        private readonly VideoProcessingService _videoProcessing;
        private readonly ShellViewModel _viewModel;

        public Shell()
        {
            InitializeComponent();
            DataContext = _viewModel;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Application.Current.Shutdown(); // Ensure the application shuts down when the window is closed
        }
    }
}
