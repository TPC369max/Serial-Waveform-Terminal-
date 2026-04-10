using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.Metrics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Yell.Utilities;

namespace Yell
{
    partial class MainViewModel : ObservableObject
    {
        SerialManager serialManager = new SerialManager();
        public ObservableCollection<string> AvailablePorts { get; set; } = new ObservableCollection<string>();

        List<LogEntry> _testResultCache = new();

        public MainViewModel()
        {
            RefreshPorts();
            serialManager.FullPacketReceivedEvent += OnPacketArrived;

        }
        [ObservableProperty]
        public string _selectedStopBitsKey = "1 位 (One)";
        [ObservableProperty]
        public string _selectedParityKey = "无校验 (None)";
        [ObservableProperty]
        private string _selectedPort;
        [ObservableProperty]
        string _inputHexText;

        public event Action<double> OnNewDataParsed;
        public void OnPacketArrived(byte[] rowData)
        {
            string hexString = HexConverter.BytesToHexString(rowData);
            double processValue = 0.0;
            bool isParsed = false;
            try
            {
                if (rowData.Length >= 3 && rowData[0] == 0xAA)
                {
                    processValue = rowData[2] / 10.0;
                    isParsed = true;
                    OnNewDataParsed?.Invoke(processValue);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"数据解析异常: {ex.Message}");
            }
            finally
            {
                string displayValue = isParsed ? processValue.ToString("F1") : "-";
                Log("RX", hexString, displayValue);
            }
        }
        [RelayCommand]
        public async Task RunSimulationAsync()
        {
            if (IsSimulating)
                return;
            IsSimulating = true;

            Log("系统", "开始生成 1000 条波形模拟数据...", "");
            await Task.Run(async () =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    double sinValue = 10 * Math.Sin(i * 0.1) + 25;
                    byte val = (byte)Math.Clamp(sinValue * 5, 0, 255);

                    byte[] simPacket = new byte[] { 0xaa, 0x1, val };
                    OnPacketArrived(simPacket);

                    await Task.Delay(20);
                }
            });

            IsSimulating = false;
            Log("系统", "模拟数据推送完毕。", "");
        }
        bool IsSimulating = false;
        public void RefreshPorts()
        {
            AvailablePorts.Clear();
            string[] ports = SerialPort.GetPortNames();
            foreach (string port in ports)
            {
                AvailablePorts.Add(port);
            }
            if (AvailablePorts.Count > 0)
            {
                SelectedPort = AvailablePorts[0];
            }
        }
        [RelayCommand(AllowConcurrentExecutions = false)]
        async Task ExecuteSendAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(InputHexText))
                {
                    MessageBox.Show("未输入！");
                    return;
                }

                byte[] payLoad = HexConverter.HexStringToBytes(InputHexText);
                if (payLoad.Length > 255)
                {
                    MessageBox.Show("单次发送的数据长度不能超过 255 字节！", "超出协议限制");
                    return;
                }
                byte[] fullFrameToSend = new byte[2 + payLoad.Length];

                fullFrameToSend[0] = 0xAA;
                fullFrameToSend[1] = (byte)payLoad.Length;

                Array.Copy(payLoad, 0, fullFrameToSend, 2, payLoad.Length);

                string fullHexLog = HexConverter.BytesToHexString(fullFrameToSend);
                Log("TX", fullHexLog, "");
                await serialManager.SendDataAsync(fullFrameToSend);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "格式错误");
            }

        }
        [RelayCommand]
        void StartConnect()
        {
            if (string.IsNullOrEmpty(SelectedPort))
            {
                MessageBox.Show("请先选择一个有效的串口！");
                return;
            }
            try
            {
                Parity parity = ParityMap[SelectedParityKey];
                StopBits stopBits = StopBitsMap[SelectedStopBitsKey];

                serialManager.ConfigureAndOpen(SelectedPort, 115200, parity, 8, stopBits);
                //MessageBox.Show("串口尝试打开，请查看控制台日志！");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }



        public Dictionary<string, Parity> ParityMap
        {
            get;
        } = new Dictionary<string, Parity>
    {
        { "无校验 (None)", Parity.None },
        { "奇校验 (Odd)", Parity.Odd },
        { "偶校验 (Even)", Parity.Even },
        { "标志位 (Mark)", Parity.Mark },
        { "空格位 (Space)", Parity.Space }
    };

        public Dictionary<string, StopBits> StopBitsMap
        {
            get;
        } = new Dictionary<string, StopBits>
        {
        { "1 位 (One)", StopBits.One },
        { "2 位 (Two)", StopBits.Two },
        { "1.5 位 (1.5)", StopBits.OnePointFive }
        };

        readonly object _cacheLock = new object();
        [ObservableProperty]
        ObservableCollection<LogEntry> _logs = new();
        void Log(string direction, string content, string parsedValue)
        {
            LogEntry entry = new LogEntry(DateTime.Now.ToString("HH:mm:ss.fff"), direction, content, parsedValue);
            App.Current.Dispatcher.Invoke(() =>
            {
                if (Logs.Count > 1000)
                    Logs.RemoveAt(0);
                Logs.Add(entry);
            });
            lock (_cacheLock)
            {
                _testResultCache.Add(entry);
            }
            _ = FileLogger.WriteLogAsync(direction, content, parsedValue);
            
        }

        [RelayCommand]
        void ClearData()
        {
            lock (_cacheLock)
            {
                _testResultCache.Clear();
            }
            Logs.Clear();
            Log("系统", "缓冲区已清空，准备开始新一轮测试。", "");
        }

        [RelayCommand]
        async Task ExportDataAsync()
        {
            if (Logs == null || Logs.Count == 0)
            {
                MessageBox.Show("当前没有可导出的数据！");
                return;
            }
            try
            {
                await CsvExportService.ExportToCsvAsync(Logs);
            }
            catch (Exception ex)
            {
                Log("错误", $"导出异常: {ex.Message}","-");
            }

            }
    }
}
