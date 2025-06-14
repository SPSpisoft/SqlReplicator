using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace SqlReplicator
{
    public class ServiceInstallerManager
    {
        private const string ServiceName = "SqlServerReplicationService"; // نام سرویس ویندوز
        private static string ServiceExePath; // مسیر فایل اجرایی سرویس

        /// <summary>
        /// مدیریت نصب/حذف/شروع سرویس همگام سازی.
        /// </summary>
        /// <param name="baseConnectionString">رشته اتصال به دیتابیس پایه که در App.config سرویس ذخیره می شود.</param>
        /// <param name="progress">شیء IProgress برای گزارش وضعیت به UI.</param>
        /// <returns>True اگر عملیات با موفقیت انجام شود، در غیر این صورت False.</returns>
        public static async Task<bool> ManageReplicationService(string baseConnectionString, IProgress<Tuple<string, bool>> progress)
        {
            // مسیر فایل اجرایی سرویس (فرض می کنیم در همان مسیر برنامه WPF است)
            // شما باید مطمئن شوید که فایل SqlReplicationService.exe در کنار YourWpfApp.exe کپی شده است.
            ServiceExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SqlReplicationService.exe");

            if (!File.Exists(ServiceExePath))
            {
                progress.Report(Tuple.Create("خطا: فایل اجرایی سرویس 'SqlReplicationService.exe' یافت نشد. اطمینان حاصل کنید که در کنار برنامه قرار دارد.", false));
                return false;
            }

            progress.Report(Tuple.Create("در حال بررسی وضعیت سرویس...", true));

            // مرحله 1: بررسی و حذف سرویس موجود (اگر وجود دارد)
            ServiceController service = ServiceController.GetServices().FirstOrDefault(s => s.ServiceName == ServiceName);

            if (service != null)
            {
                progress.Report(Tuple.Create("سرویس موجود شناسایی شد. در حال توقف سرویس...", true));
                try
                {
                    if (service.Status != ServiceControllerStatus.Stopped && service.Status != ServiceControllerStatus.StopPending)
                    {
                        service.Stop();
                        service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                    }
                    progress.Report(Tuple.Create("سرویس متوقف شد. در حال حذف سرویس...", true));
                    if (!await UninstallService(progress)) return false;
                }
                catch (Exception ex)
                {
                    progress.Report(Tuple.Create($"خطا در توقف یا حذف سرویس: {ex.Message}", false));
                    return false;
                }
            }
            else
            {
                progress.Report(Tuple.Create("هیچ سرویس قبلی یافت نشد.", true));
            }

            // مرحله 2: به روز رسانی App.config سرویس با رشته اتصال دیتابیس پایه
            progress.Report(Tuple.Create("در حال به روز رسانی فایل پیکربندی سرویس...", true));
            if (!UpdateServiceAppConfig(baseConnectionString, progress))
            {
                return false;
            }

            // مرحله 3: نصب سرویس جدید
            progress.Report(Tuple.Create("در حال نصب سرویس جدید...", true));
            if (!await InstallService(progress)) return false;

            // مرحله 4: شروع سرویس
            progress.Report(Tuple.Create("در حال شروع سرویس...", true));
            try
            {
                service = new ServiceController(ServiceName);
                if (service.Status != ServiceControllerStatus.Running && service.Status != ServiceControllerStatus.StartPending)
                {
                    service.Start();
                    service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                }
                progress.Report(Tuple.Create("سرویس با موفقیت شروع شد.", true));
            }
            catch (Exception ex)
            {
                progress.Report(Tuple.Create($"خطا در شروع سرویس: {ex.Message}", false));
                return false;
            }

            progress.Report(Tuple.Create("عملیات مدیریت سرویس با موفقیت به پایان رسید.", true));
            return true;
        }

        /// <summary>
        /// حذف سرویس.
        /// </summary>
        private static async Task<bool> UninstallService(IProgress<Tuple<string, bool>> progress)
        {
            try
            {
                string installUtilPath = GetInstallUtilPath();
                if (string.IsNullOrEmpty(installUtilPath))
                {
                    progress.Report(Tuple.Create("خطا: InstallUtil.exe یافت نشد. اطمینان حاصل کنید که .NET Framework نصب است.", false));
                    return false;
                }

                await RunCommand(installUtilPath, $"/u \"{ServiceExePath}\"", progress);
                progress.Report(Tuple.Create("سرویس با موفقیت حذف شد.", true));
                return true;
            }
            catch (Exception ex)
            {
                progress.Report(Tuple.Create($"خطا در حذف سرویس: {ex.Message}", false));
                return false;
            }
        }

        /// <summary>
        /// نصب سرویس.
        /// </summary>
        private static async Task<bool> InstallService(IProgress<Tuple<string, bool>> progress)
        {
            try
            {
                string installUtilPath = GetInstallUtilPath();
                if (string.IsNullOrEmpty(installUtilPath))
                {
                    progress.Report(Tuple.Create("خطا: InstallUtil.exe یافت نشد. اطمینان حاصل کنید که .NET Framework نصب است.", false));
                    return false;
                }

                await RunCommand(installUtilPath, $"\"{ServiceExePath}\"", progress);
                progress.Report(Tuple.Create("سرویس با موفقیت نصب شد.", true));
                return true;
            }
            catch (Exception ex)
            {
                progress.Report(Tuple.Create($"خطا در نصب سرویس: {ex.Message}", false));
                return false;
            }
        }

        /// <summary>
        /// پیدا کردن مسیر InstallUtil.exe.
        /// </summary>
        private static string GetInstallUtilPath()
        {
            // سعی کنید مسیر InstallUtil.exe را از نسخه های مختلف .NET Framework پیدا کنید
            string frameworkPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Microsoft.NET", "Framework64");
            if (!Directory.Exists(frameworkPath))
            {
                frameworkPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Microsoft.NET", "Framework");
            }

            var netVersions = Directory.GetDirectories(frameworkPath)
                                     .OrderByDescending(d => d) // جدیدترین نسخه را اول بیاورید
                                     .Where(d => d.Contains("v4.0") || d.Contains("v2.0")) // فقط نسخه های 2.0 و 4.0 و بالاتر
                                     .ToList();

            foreach (var versionPath in netVersions)
            {
                string installUtil = Path.Combine(versionPath, "InstallUtil.exe");
                if (File.Exists(installUtil))
                {
                    return installUtil;
                }
            }
            return null;
        }

        /// <summary>
        /// اجرای یک دستور در خط فرمان.
        /// </summary>
        private static async Task RunCommand(string command, string args, IProgress<Tuple<string, bool>> progress)
        {
            using (Process process = new Process())
            {
                process.StartInfo.FileName = command;
                process.StartInfo.Arguments = args;
                process.StartInfo.UseShellExecute = false; // عدم استفاده از Shell برای اجرای مستقیم برنامه
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true; // پنجره سیاه را نشان نده

                process.Start();

                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();

                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    // اگر خطا رخ داده باشد، پیام خطا را گزارش کنید.
                    string errorMessage = string.IsNullOrEmpty(error) ? output : error;
                    throw new InvalidOperationException($"خطا در اجرای دستور: {command} {args}\nپیام: {errorMessage}");
                }
                else
                {
                    // می توانید خروجی را برای اطلاعات بیشتر گزارش کنید.
                    // progress.Report(Tuple.Create($"خروجی دستور: {output}", true));
                }
            }
        }

        /// <summary>
        /// به روز رسانی فایل App.config سرویس برای ذخیره رشته اتصال دیتابیس پایه.
        /// </summary>
        private static bool UpdateServiceAppConfig(string baseConnectionString, IProgress<Tuple<string, bool>> progress)
        {
            string configFilePath = ServiceExePath + ".config";
            try
            {
                XDocument doc;
                if (File.Exists(configFilePath))
                {
                    doc = XDocument.Load(configFilePath);
                }
                else
                {
                    // اگر فایل config وجود نداشت، یک فایل پایه ایجاد کنید.
                    doc = new XDocument(
                        new XElement("configuration",
                            new XElement("appSettings")
                        )
                    );
                }

                XElement appSettings = doc.Element("configuration")?.Element("appSettings");
                if (appSettings == null)
                {
                    appSettings = new XElement("appSettings");
                    doc.Element("configuration")?.Add(appSettings);
                }

                XElement setting = appSettings.Elements("add")
                                              .FirstOrDefault(e => e.Attribute("key")?.Value == "BaseDatabaseConnection");

                if (setting != null)
                {
                    setting.SetAttributeValue("value", baseConnectionString);
                }
                else
                {
                    appSettings.Add(new XElement("add",
                                       new XAttribute("key", "BaseDatabaseConnection"),
                                       new XAttribute("value", baseConnectionString)));
                }

                doc.Save(configFilePath);
                return true;
            }
            catch (Exception ex)
            {
                progress.Report(Tuple.Create($"خطا در به روز رسانی فایل App.config سرویس: {ex.Message}", false));
                return false;
            }
        }
    }
}
