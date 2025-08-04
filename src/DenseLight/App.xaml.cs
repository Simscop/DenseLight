using CommunityToolkit.Mvvm.Messaging;
using DenseLight.BusinessLogic;
using DenseLight.Devices;
using DenseLight.Logger;
using DenseLight.Services;
using DenseLight.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Configuration;
using System.Data;
using System.Windows;

namespace DenseLight
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public IServiceProvider Services { get; private set; }

        public App()
        {
            Services = ConfigureServices();
            this.InitializeComponent();
        }

        public new static App Current => (App)Application.Current;


        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Register configuration settings
            services.AddSingleton<ILoggerService, FileLoggerService>();

            // Register core services
            services.AddSingleton<IMotor, ZaberMotorService>();
            services.AddSingleton<ICameraService, HikCameraService>();

            // Register business logic services
            services.AddSingleton<AutoFocusService>();
            services.AddSingleton<MotionControlService>();

            services.AddSingleton<PositionUpdateService>();
            //services.AddSingleton<FrameRefreshService>();

            //services.AddSingleton<VideoProcessingService>();

            // Register video processing service
            services.AddSingleton<IImageProcessingService, ImageProcessingService>();

            // Register view models
            services.AddSingleton<ShellViewModel>();
            services.AddSingleton<SteerViewModel>();
            services.AddSingleton<CameraViewModel>();

            // Register Messenger for MVVM communication
            services.AddSingleton<IMessenger>(new WeakReferenceMessenger());

            return services.BuildServiceProvider();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 用这个会开两次
            //var shell = new Shell
            //{
            //    DataContext = Services.GetRequiredService<ShellViewModel>()
            //};
            //shell.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            try
            {
                // Dispose of services
                if (Services is ServiceProvider serviceProvider)
                {
                    serviceProvider.Dispose();
                }
            }
            catch (Exception ex)
            {
                // Log any exceptions during shutdown
                var logger = Services.GetService<ILoggerService>();
                logger?.LogError($"Error during application exit: {ex.Message}");
            }
        }

    }

}
