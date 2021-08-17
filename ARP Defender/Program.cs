using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ARP_Defender
{
    static class Program
    {
        /// <summary>
        /// 應用程式的主要進入點。
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());

            string path = Application.StartupPath;
            SettingHel.SetAutoRun(path + @"\IISCheck.exe", true);
        }

        public static class SettingHel
        {
            /// <summary>
            /// 设置应用程序开机自动运行
            /// </summary>
            /// <param name="fileName">应用程序的文件名</param>
            /// <param name="isAutoRun">是否自动运行,为false时，取消自动运行</param>
            /// <exception cref="system.Exception">设置不成功时抛出异常</exception>
            /// <returns>返回1成功，非1不成功</returns>
            public static String SetAutoRun(string fileName, bool isAutoRun)
            {
                string reSet = string.Empty;
                RegistryKey reg = null;
                try
                {
                    if (!System.IO.File.Exists(fileName))
                    {
                        reSet = "NOT FOUND！";
                    }
                    string name = fileName.Substring(fileName.LastIndexOf(@"\") + 1);
                    reg = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                    if (reg == null)
                    {
                        reg = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
                    }
                    if (isAutoRun)
                    {
                        reg.SetValue(name, fileName);
                        reSet = "1";
                    }
                    else
                    {
                        reg.SetValue(name, false);
                    }

                }
                catch (Exception)
                {
                    MessageBox.Show("無法設置開機自動啟動本應用程式。", "錯誤");
                }
                finally
                {
                    if (reg != null)
                    {
                        reg.Close();
                    }
                }
                return reSet;
            }
        }
    }
}
