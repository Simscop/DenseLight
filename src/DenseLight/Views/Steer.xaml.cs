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
    /// Steer.xaml 的交互逻辑
    /// </summary>
    public partial class Steer : UserControl
    {
        private readonly SteerViewModel _viewModel;
        private bool _disposed = false; // 用于跟踪是否已释放资源
        public Steer()
        {
            InitializeComponent();
            _viewModel = App.Current.Services.GetRequiredService<SteerViewModel>();
            DataContext = _viewModel;

            Unloaded += OnUnloaded;
        }

        private void Steer_Unloaded(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // 释放托管资源
                Unloaded -= OnUnloaded;

                // 释放 ViewModel
                if (_viewModel is IDisposable disposableViewModel)
                {
                    disposableViewModel.Dispose();
                }
            }

            _disposed = true;
        }

        // 可选：析构函数作为安全网
        ~Steer()
        {
            Dispose(false);
        }
    }
}
