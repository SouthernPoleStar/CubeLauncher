using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using CubeLauncher.Core;
using CubeLauncher.UI;
using Microsoft.Win32;

namespace CubeLauncher
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                SetProcessDPIAware();
                System.Threading.ThreadPool.SetMinThreads(64, 64);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                EnableBrowserEmulation();
                App.Initialize();

                Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
                {
                    Log.Error("未处理的界面异常", e.Exception);
                    MessageBox.Show("启动器遇到一个错误：\n" + e.Exception.Message + "\n\n详细信息已写入日志。", "方块启动器",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                };
                AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
                {
                    Exception ex = e.ExceptionObject as Exception;
                    if (ex != null) Log.Error("未处理的异常", ex);
                };

                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                try
                {
                    Log.Error("启动器启动失败", ex);
                }
                catch { }
                MessageBox.Show("启动器启动失败：\n" + ex.Message, "方块启动器", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void EnableBrowserEmulation()
        {
            try
            {
                string exe = Path.GetFileName(Application.ExecutablePath);
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION"))
                {
                    if (key != null) key.SetValue(exe, 11001, RegistryValueKind.DWord);
                }
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_GPU_RENDERING"))
                {
                    if (key != null) key.SetValue(exe, 1, RegistryValueKind.DWord);
                }
            }
            catch
            {
            }
        }

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();
    }
}
