using System;
using System.Threading;
using System.Windows;

namespace KittyFocus
{
    /// <summary>
    /// App.xaml 的交互逻辑。
    /// 负责单实例守卫（托盘常驻应用应避免多开）与启动流程。
    /// </summary>
    public partial class App : Application
    {
        private const string MutexName = "Global\\KittyFocus_SingleInstance_3F7A1C";
        private static Mutex _singleInstanceMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 单实例守卫：已存在实例则提示并退出。
            _singleInstanceMutex = new Mutex(initiallyOwned: true, name: MutexName, out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show("专注猫灵已经在运行啦~", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            // 手动构造主窗口，保证托盘图标等资源在窗口显示前就绪。
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try { _singleInstanceMutex?.ReleaseMutex(); } catch { }
            _singleInstanceMutex?.Dispose();
            base.OnExit(e);
        }
    }
}
