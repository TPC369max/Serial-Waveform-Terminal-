using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Yell.Utilities; 

namespace Yell
{
    /// <summary>
    /// 主界面业务逻辑控制中心
    /// 采用 MVVM 模式，负责硬件通讯调度、波形解析分发及数据持久化。
    /// </summary>
    public partial class MainViewModel : ObservableObject
    {
        #region 1. 私有后端字段 (Private Fields)

        private readonly SerialManager serialManager = new SerialManager();
        private readonly List<LogEntry> _testResultCache = new(); // 业务级全量缓存
        private readonly object _cacheLock = new object();        // 缓存同步锁
        private bool _isSimulating = false;                        // 仿真状态标记

        #endregion

        #region 2. UI 绑定属性 (Observable Properties)

        [ObservableProperty] private string _selectedPort;
        [ObservableProperty] private string _selectedStopBitsKey = "1 位 (One)";
        [ObservableProperty] private string _selectedParityKey = "无校验 (None)";
        [ObservableProperty] private string _inputHexText;
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ExecuteSendCommand))]
        private bool _isConnected;

        /// <summary>
        /// 串口列表
        /// </summary>
        public ObservableCollection<string> AvailablePorts { get; } = new();

        /// <summary>
        /// UI 展示日志（保持 1000 条）
        /// </summary>
        [ObservableProperty] private ObservableCollection<LogEntry> _logs = new();

        #endregion

        #region 3. 静态映射字典 (Configuration Maps)

        public Dictionary<string, Parity> ParityMap { get; } = new()
        {
            { "无校验 (None)", Parity.None },
            { "奇校验 (Odd)", Parity.Odd },
            { "偶校验 (Even)", Parity.Even },
            { "标志位 (Mark)", Parity.Mark },
            { "空格位 (Space)", Parity.Space }
        };

        public Dictionary<string, StopBits> StopBitsMap { get; } = new()
        {
            { "1 位 (One)", StopBits.One },
            { "2 位 (Two)", StopBits.Two },
            { "1.5 位 (1.5)", StopBits.OnePointFive }
        };

        #endregion

        #region 3.5串口进阶参数

        // 可选波特率列表（涵盖工业常用标准）
        public List<int> BaudRateList { get; } = new() { 4800, 9600, 19200, 38400, 57600, 115200, 230400, 460800, 921600 };

        // 可选数据位列表
        public List<int> DataBitsList { get; } = new() { 5, 6, 7, 8 };

        [ObservableProperty] private string _selectedBaudRate = "115200"; // 默认 115200
        [ObservableProperty] private int _selectedDataBits = 8;      // 默认 8

        #endregion

        #region 4. 事件与构造函数 (Events & Constructor)

        /// <summary>
        /// 当解析出有效的物理量时触发，供 View 层波形图订阅
        /// </summary>
        public event Action<double> OnNewDataParsed;

        public MainViewModel()
        {
            // 初始化串口列表
            RefreshPorts();
            // 订阅底层完整包到达事件
            serialManager.FullPacketReceivedEvent += OnPacketArrived;
        }

        #endregion

        #region 5. 核心逻辑处理 (Core Logic)

        /// <summary>
        /// 处理来自串口或仿真器的完整协议包
        /// </summary>
        private void OnPacketArrived(byte[] rowData)
        {
            string hexString = HexConverter.BytesToHexString(rowData);
            double processValue = 0.0;
            bool isParsed = false;

            try
            {
                // 工业协议解析：帧头(AA) + 长度(Payload) + 数据(Value)
                if (rowData.Length >= 3 && rowData[0] == 0xAA)
                {
                    // 将原始字节转为物理量 (例：除以10.0保留小数位)
                    processValue = rowData[2] / 10.0;
                    isParsed = true;
                    // 通知波形控件更新
                    OnNewDataParsed?.Invoke(processValue);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"数据解析异常: {ex.Message}");
            }
            finally
            {
                // 记录日志：解析成功显示数值，失败显示占位符
                string displayValue = isParsed ? processValue.ToString("F1") : "-";
                Log("RX", hexString, displayValue);
            }
        }

        /// <summary>
        /// 统一日志记录器：处理 UI 更新、内存缓存与文件持久化
        /// </summary>
        private void Log(string direction, string content, string parsedValue)
        {
            var app=Application.Current;
            if (app == null)
            {
                return;
            }
            LogEntry entry = new LogEntry(
                DateTime.Now.ToString("HH:mm:ss.fff"),
                direction,
                content,
                parsedValue);

            // 1. UI 更新：通过 Dispatcher 确保线程安全，并实现 FIFO 滑动窗口
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (app.MainWindow == null || Logs == null) return;
                if (Logs.Count >= 1000) Logs.RemoveAt(0);
                Logs.Add(entry);
            });

            // 2. 内存缓存：全量保存供 CSV 导出，加锁防止多线程冲突
            lock (_cacheLock)
            {
                _testResultCache.Add(entry);
            }

            // 3. 文件落盘：异步写入本地 .log 文件
            _ = FileLogger.WriteLogAsync(direction, content, parsedValue);
        }

        #endregion

        #region 6. 界面命令实现 (RelayCommands)

        /// <summary>
        /// 刷新本机可用串口
        /// </summary>
        [RelayCommand]
        private void RefreshPorts()
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

        /// <summary>
        /// 建立物理连接
        /// </summary>
        [RelayCommand]
        private void StartConnect()
        {
            if (string.IsNullOrEmpty(SelectedPort))
            {
                MessageBox.Show("请先选择一个有效的串口！");
                return;
            }
            if (!int.TryParse(SelectedBaudRate, out int baudRate))
            {
                baudRate = 115200;
                Log("警告", "波特率输入非法，已自动校正为 115200", "-");
            }
            try
            {
                Parity parity = ParityMap[SelectedParityKey];
                StopBits stopBits = StopBitsMap[SelectedStopBitsKey];

                serialManager.ConfigureAndOpen(
            SelectedPort,
            baudRate, // 动态值
            parity,
            SelectedDataBits, // 动态值
            stopBits
        );
                IsConnected = serialManager.IsOpen;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                MessageBox.Show($"连接失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 自动组装协议帧并发送
        /// 协议格式：AA [PayloadLength] [Data...]
        /// </summary>
        [RelayCommand(AllowConcurrentExecutions = false,CanExecute =nameof(IsConnected))]
        private async Task ExecuteSendAsync()
        {
            // 1. 物理层双重检查 (防止意外断开)
            if (!serialManager.IsOpen)
            {
                IsConnected = false;
                Log("错误", "物理连接已断开，请重新检查串口！", "-");
                return;
            }
            try
            {
                if (string.IsNullOrWhiteSpace(InputHexText)) return;

                byte[] payLoad = HexConverter.HexStringToBytes(InputHexText);
                if (payLoad.Length > 255)
                {
                    MessageBox.Show("单次负载不能超过 255 字节！");
                    return;
                }

                // 自动封包
                byte[] fullFrame = new byte[2 + payLoad.Length];
                fullFrame[0] = 0xAA;
                fullFrame[1] = (byte)payLoad.Length;
                Array.Copy(payLoad, 0, fullFrame, 2, payLoad.Length);

                Log("TX", HexConverter.BytesToHexString(fullFrame), "-");
                await serialManager.SendDataAsync(fullFrame);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"发送格式有误: {ex.Message}");
            }
        }

        /// <summary>
        /// 开启正弦波仿真引擎：验证全链路数据流稳定性
        /// </summary>
        [RelayCommand]
        private async Task RunSimulationAsync()
        {
            if (_isSimulating) return;
            _isSimulating = true;

            Log("系统", "开始生成 1000 条波形模拟数据...", "");

            await Task.Run(async () =>
            {
                for (int i = 0; i < 100; i++)
                {
                    // 正弦波模型计算
                    double sinValue = 10 * Math.Sin(i * 0.1) + 25;
                    byte val = (byte)Math.Clamp(sinValue * 5, 0, 255);

                    byte[] simPacket = new byte[] { 0xAA, 0x01, val };
                    OnPacketArrived(simPacket);

                    await Task.Delay(20); // 模拟 50Hz 采样率
                }
            });

            _isSimulating = false;
            Log("系统", "模拟数据推送完毕。", "");
        }

        /// <summary>
        /// 导出历史缓存至 CSV 文件
        /// </summary>
        [RelayCommand]
        private async Task ExportDataAsync()
        {
            if (_testResultCache.Count == 0)
            {
                MessageBox.Show("当前没有可导出的数据！");
                return;
            }
            try
            {
                // 导出业务全量缓存 _testResultCache
                await CsvExportService.ExportToCsvAsync(Logs);
            }
            catch (Exception ex)
            {
                Log("错误", $"导出异常: {ex.Message}", "-");
            }
        }

        /// <summary>
        /// 重置所有测试数据
        /// </summary>
        [RelayCommand]
        private void ClearData()
        {
            lock (_cacheLock)
            {
                _testResultCache.Clear();
            }
            Logs.Clear();
            Log("系统", "缓冲区已清空，准备开始新一轮测试。", "");
        }

        #endregion
    }
}