using Microsoft.Win32;
using ScottPlot.Renderable;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yell.Utilities
{
    public class CsvExportService
    {
        public static async Task ExportToCsvAsync(IEnumerable<LogEntry> data)
        {
            var dataSnapShot = data.ToList();
            SaveFileDialog sfd = new SaveFileDialog()
            {
                Filter = "CSV文件 (*.csv)|*.csv",
                FileName = $"测试报告_{DateTime.Now:yyyyMMdd_HHmmss}",
                DefaultExt = ".csv"
            };
            if (sfd.ShowDialog() == true)
            {
                try
                {
                    using (StreamWriter sw = new StreamWriter(sfd.FileName, false, new UTF8Encoding(true)))
                    {
                        await sw.WriteLineAsync("时间戳,数据类型,HEX 字符串,测量值");

                        foreach (var item in dataSnapShot)
                        {
                            string line = $"{item.Time},{item.Direction},{item.RawData},{item.Value}";
                            await sw.WriteLineAsync(line);
                        }
                        await sw.FlushAsync();
                    }
                    System.Windows.MessageBox.Show("导出成功！", "提示");
                }
                catch (Exception ex)
                {
                    throw new  Exception($"导出过程中发生 IO 冲突: {ex.Message}");
                }
            }
        }
    }
}
