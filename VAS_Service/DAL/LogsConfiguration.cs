using Serilog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceProcess;
namespace VAS_Service.DAL
{
    public class LogsConfiguration
    {
        public void GenerateLogs()
        {
            //try
            //{
            //    int FileSize = Convert.ToInt32(ConfigurationSettings.AppSettings["LogsFileSize"].ToString());
            //    Log.Logger = new LoggerConfiguration()
            //       .MinimumLevel.Debug() // Set the minimum log level
            //       .WriteTo.File(
            //           path: ConfigurationSettings.AppSettings["LogsPath"].ToString() + "/Logs/AppLogs-.log", // Log file path
            //           rollingInterval: RollingInterval.Day, // Daily rolling
            //           fileSizeLimitBytes: FileSize * 1024 * 1024, // 20 MB file size limit
            //           retainedFileCountLimit: null, // Retain all files (use a number if you want to limit the number of files)
            //           rollOnFileSizeLimit: true // Roll file when size limit is reached
            //       )
            //       .CreateLogger();
            //}
            //catch (Exception)
            //{
            //}
            try
            {
                int fileSize = Convert.ToInt32(ConfigurationSettings.AppSettings["LogsFileSize"].ToString());
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug() // Set the minimum log level for the root logger
                    .WriteTo.Logger(lc => lc
                        .MinimumLevel.Information() // Set minimum level for Info logs
                        .WriteTo.File(
                            path: ConfigurationSettings.AppSettings["LogsPath"].ToString() + "/InfoLogs/AppLogs-.log", // Info logs path
                            rollingInterval: RollingInterval.Day,
                            fileSizeLimitBytes: fileSize * 1024 * 1024,
                            retainedFileCountLimit: null,
                            rollOnFileSizeLimit: true
                        )
                    )
                    .WriteTo.Logger(lc => lc
                        .MinimumLevel.Error() // Set minimum level for Error logs
                        .WriteTo.File(
                            path: ConfigurationSettings.AppSettings["LogsPath"].ToString() + "/ErrorLogs/AppLogs-.log", // Error logs path
                            rollingInterval: RollingInterval.Day,
                            fileSizeLimitBytes: fileSize * 1024 * 1024,
                            retainedFileCountLimit: null,
                            rollOnFileSizeLimit: true
                        )
                    )
                    .CreateLogger();
            }
            catch (Exception ex)
            {
                // Handle exceptions related to logger configuration
                //Console.WriteLine($"An error occurred while configuring the logger: {ex.Message}");
              //  EventLog.WriteEntry($"An error occurred while configuring the logger: {ex.Message}");
            }
        }







    }
}
