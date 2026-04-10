using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Yell
{
    /// <summary>
    /// 工业级串口通讯管理类
    /// 具备：断包粘包处理、解析超时清理、异步高并发发送功能
    /// </summary>
    internal class SerialManager
    {
        #region 1. 私有字段与常量 (Fields & Constants)

        private SerialPort _serialPort = new SerialPort();
        private System.Timers.Timer _timeoutTimer;
        private readonly List<byte> _buffer = new List<byte>();

        private const int PACKET_TIMEOUT = 500;   // 解析超时时间 (ms)
        private const int MAX_BUFFER_SIZE = 4096; // 缓冲区保护上限 (bytes)
        public bool IsOpen => _serialPort != null && _serialPort.IsOpen;

        #endregion

        #region 2. 事件声明 (Events)

        /// <summary>
        /// 当解析出符合协议(AA开头且长度匹配)的完整数据包时触发
        /// </summary>
        public event Action<byte[]> FullPacketReceivedEvent;

        #endregion

        #region 3. 构造函数 (Constructor)

        public SerialManager()
        {
            // 初始化解析超时定时器：防止残包长期占用内存
            _timeoutTimer = new System.Timers.Timer(PACKET_TIMEOUT);
            _timeoutTimer.AutoReset = false;
            _timeoutTimer.Elapsed += (s, e) =>
            {
                lock (_buffer)
                {
                    if (_buffer.Count > 0)
                    {
                        _buffer.Clear();
                        System.Diagnostics.Debug.WriteLine("检测到解析超时，已清空缓存区数据。");
                    }
                }
            };
        }

        #endregion

        #region 4. 公共操作接口 (Public Methods)

        /// <summary>
        /// 配置并打开串口
        /// </summary>
        public void ConfigureAndOpen(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits)
        {
            try
            {
                // 如果当前已打开，先关闭释放资源
                if (_serialPort.IsOpen)
                {
                    this.Close();
                }

                // 注入硬件参数
                _serialPort = new SerialPort(portName, baudRate, parity, dataBits, stopBits);

                // 工业级握手控制（针对物理芯片稳定性）
                _serialPort.DtrEnable = true;
                _serialPort.RtsEnable = true;

                // 超时保护
                _serialPort.ReadTimeout = 2000;
                _serialPort.WriteTimeout = 2000;

                // 注册底层物理接收事件
                _serialPort.DataReceived += SerialPort_DataReceived;

                _serialPort.Open();

                // 工业现场建议保留此类关键操作的确认弹窗
                MessageBox.Show($"串口已连接: {portName}\n参数: {baudRate},{parity},{dataBits},{stopBits}", "通讯就绪");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开串口: {ex.Message}", "硬件异常");
            }
        }

        /// <summary>
        /// 同步发送数据 (用于简单指令)
        /// </summary>
        public void SendData(byte[] data)
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Write(data, 0, data.Length);
            }
        }

        /// <summary>
        /// 异步发送数据 (工业推荐：高并发下不卡顿 UI)
        /// </summary>
        public async Task SendDataAsync(byte[] data)
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                try
                {
                    // 开启异步流写入，防止大规模数据写入阻塞主线程
                    await _serialPort.BaseStream.WriteAsync(data, 0, data.Length);
                    await _serialPort.BaseStream.FlushAsync();
                }
                catch (Exception ex)
                {
                    throw new Exception($"底层写入异常: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 彻底关闭串口并清理相关逻辑资源
        /// </summary>
        public void Close()
        {
            try
            {
                // 1. 停止超时监控逻辑，防止空转
                if (_timeoutTimer != null)
                {
                    _timeoutTimer.Stop();
                }

                if (_serialPort != null && _serialPort.IsOpen)
                {
                    // 2. 先解绑事件，防止在关闭过程中还有数据冲进来触发逻辑
                    _serialPort.DataReceived -= SerialPort_DataReceived;

                    // 3. 物理关闭
                    _serialPort.Close();
                }

                // 4. 清空缓存桶，确保下次连接时数据是干净的
                lock (_buffer)
                {
                    _buffer.Clear();
                }
            }
            catch (Exception ex)
            {
                // 串口关闭有时会抛出物理异常（比如 USB 被暴力拔出），这里要接住
                System.Diagnostics.Debug.WriteLine($"串口关闭异常: {ex.Message}");
            }
        }

        #endregion

        #region 5. 核心逻辑：断包粘包解析 (Parsing Logic)

        /// <summary>
        /// 物理层接收回调：核心任务是将碎裂的字节拼凑为完整逻辑包
        /// </summary>
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            // 只要有数据进来，重置超时计时器（续命）
            _timeoutTimer.Stop();
            _timeoutTimer.Start();

            // 1. 读取当前物理缓冲区的所有字节
            int bytesToRead = _serialPort.BytesToRead;
            byte[] tempBuffer = new byte[bytesToRead];
            _serialPort.Read(tempBuffer, 0, bytesToRead);

            // 2. 线程安全地进入缓存桶
            lock (_buffer)
            {
                _buffer.AddRange(tempBuffer);

                // 内存防御：异常乱码导致缓冲区膨胀保护
                if (_buffer.Count > MAX_BUFFER_SIZE)
                {
                    _buffer.Clear();
                }

                // 3. 循环拆解完整协议包 (处理粘包：即一次 DataReceived 收到两个 AA 帧)
                while (_buffer.Count >= 2)
                {
                    // 寻找帧头 0xAA
                    if (_buffer[0] != 0xAA)
                    {
                        _buffer.RemoveAt(0); // 剔除非协议字节
                        continue;
                    }

                    // 确定长度位 (假设协议第2字节为 Payload 长度)
                    int payloadLength = _buffer[1];
                    int fullFrameLength = 2 + payloadLength;

                    // 检查缓存桶里的字节是否足够组成一个完整包
                    if (_buffer.Count >= fullFrameLength)
                    {
                        // 提取完整帧
                        byte[] fullPacket = _buffer.GetRange(0, fullFrameLength).ToArray();

                        // 从缓存中移除已解析的部分
                        _buffer.RemoveRange(0, fullFrameLength);

                        // 4. 向上层 (ViewModel) 推送完整数据包
                        FullPacketReceivedEvent?.Invoke(fullPacket);
                    }
                    else
                    {
                        // 长度不足，等待后续字节到达后再处理
                        break;
                    }
                }
            }
        }

        #endregion
    }
}