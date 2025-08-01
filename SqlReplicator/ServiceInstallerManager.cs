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
        private const string MyServiceName = "SpsReplicationService"; // نام سرویس ویندوز
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
            ServiceExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SpsReplicationService.exe");

            if (!File.Exists(ServiceExePath))
            {
                progress.Report(Tuple.Create("خطا: فایل اجرایی سرویس 'SqlReplicationService.exe' یافت نشد. اطمینان حاصل کنید که در کنار برنامه قرار دارد.", false));
                return false;
            }

            progress.Report(Tuple.Create("در حال بررسی وضعیت سرویس...", true));

            // مرحله 1: بررسی و حذف سرویس موجود (اگر وجود دارد)
            ServiceController? service = ServiceController.GetServices().FirstOrDefault(s => s.ServiceName == MyServiceName);

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
                    if (!await RemoveServiceIfExists(progress)) return false;
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

            if (!await StartService(MyServiceName, progress)) return false;

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

        private static async Task<bool> RemoveServiceIfExists(IProgress<Tuple<string, bool>> progress)
        {
            try
            {
                ServiceController? service =  ServiceController.GetServices().FirstOrDefault(s => s.ServiceName.Equals(MyServiceName, StringComparison.OrdinalIgnoreCase));

                if (service != null)
                {
                    progress.Report(Tuple.Create($"⏳ توقف سرویس موجود: {MyServiceName}", true));

                    if (service.Status != ServiceControllerStatus.Stopped)
                    {
                        service.Stop();
                        service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                    }

                    progress.Report(Tuple.Create($"🗑 حذف سرویس قبلی: {MyServiceName}", true));

                    await RunCommand("sc", $"delete {MyServiceName}", progress);

                    // کمی صبر تا سیستم واقعاً سرویس را حذف کند
                    await Task.Delay(1000);
                }
                else
                {
                    progress.Report(Tuple.Create("ℹ️ سرویس قبلاً نصب نشده است.", true));
                }

                return true;
            }
            catch (Exception ex)
            {
                progress.Report(Tuple.Create($"❌ خطا در حذف سرویس: {ex.Message}", false));
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

                //await RunCommand(installUtilPath, $"\"{ServiceExePath}\"", progress);
                //string serviceName = "SqlReplicationService";
                string scArgs = $"create {MyServiceName} binPath= \"{ServiceExePath}\" start= auto";
                await RunCommand("sc", scArgs, progress);
                progress.Report(Tuple.Create("سرویس با موفقیت نصب شد.", true));
                return true;
            }
            catch (Exception ex)
            {
                progress.Report(Tuple.Create($"خطا در نصب سرویس: {ex.Message}", false));
                return false;
            }
        }

        private static async Task<bool> StartService(string serviceName, IProgress<Tuple<string, bool>> progress)
        {
            try
            {
                // بررسی اینکه سرویس وجود دارد
                var service = ServiceController.GetServices()
                    .FirstOrDefault(s => s.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));

                if (service == null)
                {
                    progress.Report(Tuple.Create($"❌ سرویس \"{serviceName}\" یافت نشد. ممکن است نصب نشده باشد.", false));
                    return false;
                }

                // اگر سرویس اجرا نشده، آن را اجرا کن
                if (service.Status != ServiceControllerStatus.Running)
                {
                    progress.Report(Tuple.Create($"⏳ در حال اجرای سرویس \"{serviceName}\"...", true));

                    await RunCommand("sc", $"start {serviceName}", progress);

                    // کمی تأخیر بده تا وضعیت اجرا مشخص بشه
                    service.Refresh();
                    service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                }

                progress.Report(Tuple.Create($"✅ سرویس \"{serviceName}\" در حال اجرا است.", true));
                return true;
            }
            catch (Exception ex)
            {
                progress.Report(Tuple.Create($"❌ خطا در اجرای سرویس: {ex.Message}", false));
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
        //private static async Task RunCommand(string command, string args, IProgress<Tuple<string, bool>> progress)
        //{
        //    using (Process process = new Process())
        //    {
        //        process.StartInfo.FileName = command;
        //        process.StartInfo.Arguments = args;
        //        process.StartInfo.UseShellExecute = false; // عدم استفاده از Shell برای اجرای مستقیم برنامه
        //        process.StartInfo.RedirectStandardOutput = true;
        //        process.StartInfo.RedirectStandardError = true;
        //        process.StartInfo.CreateNoWindow = true; // پنجره سیاه را نشان نده

        //        process.Start();

        //        string output = await process.StandardOutput.ReadToEndAsync();
        //        string error = await process.StandardError.ReadToEndAsync();

        //        process.WaitForExit();

        //        if (process.ExitCode != 0)
        //        {
        //            // اگر خطا رخ داده باشد، پیام خطا را گزارش کنید.
        //            string errorMessage = string.IsNullOrEmpty(error) ? output : error;
        //            throw new InvalidOperationException($"خطا در اجرای دستور: {command} {args}\nپیام: {errorMessage}");
        //        }
        //        else
        //        {
        //            // می توانید خروجی را برای اطلاعات بیشتر گزارش کنید.
        //            // progress.Report(Tuple.Create($"خروجی دستور: {output}", true));
        //        }
        //    }
        //}

        private static async Task<bool> RunCommand(string fileName, string arguments, IProgress<Tuple<string, bool>> progress)
        {
            try
            {
                progress.Report(Tuple.Create($"💬 اجرای دستور: {fileName} {arguments}", true));

                var tcs = new TaskCompletionSource<bool>();

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    },
                    EnableRaisingEvents = true
                };

                // گرفتن خروجی‌ها
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                        progress.Report(Tuple.Create(e.Data, true));
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                        progress.Report(Tuple.Create("⚠️ " + e.Data, false));
                };

                process.Exited += (sender, e) =>
                {
                    tcs.SetResult(process.ExitCode == 0);
                    process.Dispose();
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                return await tcs.Task;
            }
            catch (Exception ex)
            {
                progress.Report(Tuple.Create($"❌ خطا در اجرای دستور: {ex.Message}", false));
                return false;
            }
        }


        /// <summary>
        /// به روز رسانی فایل App.config سرویس با رشته اتصال دیتابیس پایه.
        /// </summary>
        private static bool UpdateServiceAppConfig(string baseConnectionString, IProgress<Tuple<string, bool>> progress)
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SpsReplicationService.exe.config");
                if (!File.Exists(configPath))
                {
                    progress.Report(Tuple.Create("خطا: فایل پیکربندی سرویس یافت نشد.", false));
                    return false;
                }

                XDocument doc = XDocument.Load(configPath);
                var connectionStringElement = doc.Descendants("connectionStrings")
                    .Elements("add")
                    .FirstOrDefault(e => e.Attribute("name")?.Value == "BaseConnectionString");

                if (connectionStringElement != null)
                {
                    connectionStringElement.Attribute("connectionString")?.SetValue(baseConnectionString);
                }
                else
                {
                    var connectionStringsElement = doc.Descendants("connectionStrings").FirstOrDefault();
                    if (connectionStringsElement != null)
                    {
                        connectionStringsElement.Add(new XElement("add",
                            new XAttribute("name", "BaseConnectionString"),
                            new XAttribute("connectionString", baseConnectionString)));
                    }
                }

                doc.Save(configPath);
                progress.Report(Tuple.Create("فایل پیکربندی سرویس به روز رسانی شد.", true));
                return true;
            }
            catch (Exception ex)
            {
                progress.Report(Tuple.Create($"خطا در به روز رسانی فایل پیکربندی: {ex.Message}", false));
                return false;
            }
        }

        /// <summary>
        /// Get the current status of the replication service.
        /// </summary>
        /// <returns>Service status as string, or null if service doesn't exist.</returns>
        public static async Task<string?> GetServiceStatus()
        {
            try
            {
                var service = ServiceController.GetServices().FirstOrDefault(s => s.ServiceName == MyServiceName);
                
                if (service == null)
                {
                    return null; // Service doesn't exist
                }

                return service.Status.ToString();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
