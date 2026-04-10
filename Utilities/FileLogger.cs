using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yell.Utilities
{
    public static class FileLogger
    {
        static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        static readonly string LogFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        public static async Task WriteLogAsync(string direction, string rawData, string parsedValue)
        {
            string displayValue = string.IsNullOrWhiteSpace(parsedValue) ? "-" : parsedValue;
            if (!Directory.Exists(LogFolder))
                Directory.CreateDirectory(LogFolder);
            string fileName = DateTime.Now.ToString("yyyy-MM-dd") + ".log";
            string filePath = Path.Combine(LogFolder, fileName);
            string logLine = $"[{DateTime.Now:HH:mm:ss.fff}] | {direction} | {rawData} | {displayValue}{Environment.NewLine}";
            await _semaphore.WaitAsync();
            try
            {

                await File.AppendAllTextAsync(filePath, logLine, Encoding.UTF8);
            }
            finally { _semaphore.Release(); }
        }
    }
}
