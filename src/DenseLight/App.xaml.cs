using DenseLight.BusinessLogic;
using DenseLight.Devices;
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
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();
            // Register core services
            services.AddSingleton<IMotor, ZaberMotorService>();
            services.AddSingleton<ICameraService, HikCameraService>();

            // Register business logic services
            services.AddSingleton<AutoFocusService>();
            services.AddSingleton<MotionControlService>();

            // Register view models
            services.AddSingleton<ShellViewModel>();

            return services.BuildServiceProvider();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var shell = new Shell
            {
                DataContext = Services.GetRequiredService<ShellViewModel>()
            };
            shell.Show();
        }

    }

}
