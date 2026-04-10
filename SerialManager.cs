using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Windows;
namespace Yell
{
    internal class SerialManager
    {
        SerialPort _serialPort=new SerialPort();
        System.Timers.Timer _timeoutTimer;
        const int PACKET_TIMEOUT = 500;
        public List<byte> _buffer=new List<byte>();
        public event Action<byte[]> FullPacketReceivedEvent;

        public SerialManager()
        {
            // 初始化定时器
            _timeoutTimer = new System.Timers.Timer(PACKET_TIMEOUT);
            _timeoutTimer.AutoReset = false; // 只执行一次
            _timeoutTimer.Elapsed += (s, e) => {
                lock (_buffer)
                {
                    if (_buffer.Count > 0)
                    {
                        _buffer.Clear(); // 超过半秒没动静，直接清空桶
                                         // 实际开发中，可以在这里记录一条警告日志
                    }
                }
            };
        }
        public void ConfigureAndOpen(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits)
        {
            try
            {
                if (_serialPort.IsOpen)
                {
                    _serialPort.Close();
                }
                _serialPort = new SerialPort(portName, baudRate, parity, dataBits, stopBits);
                _serialPort.DtrEnable = true; // 启用数据终端准备好信号
                _serialPort.RtsEnable = true; // 启用请求发送信号
                _serialPort.DataReceived += SerialPort_DataReceived;
                _serialPort.ReadTimeout = 2000;
                _serialPort.WriteTimeout = 2000;

                _serialPort.Open();
                MessageBox.Show($"成功打开 {portName}，配置为: {baudRate}, {parity}, {dataBits}, {stopBits}");

            }
            catch (Exception ex) { Console.WriteLine($"串口配置失败: {ex.Message}"); }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            _timeoutTimer.Stop();
            _timeoutTimer.Start();  
            // A. 把水龙头里的水全部接进桶里
            int bytesToRead = _serialPort.BytesToRead;
            byte[] tempBuffer = new byte[bytesToRead];
            _serialPort.Read(tempBuffer, 0, bytesToRead);

            lock (_buffer) // 防止多线程同时操作缓冲区
            {
                _buffer.AddRange(tempBuffer);
                if (_buffer.Count > 4096)
                {
                    _buffer.Clear();
                }

                // B. 在桶里“摸鱼”：寻找符合协议的完整包
                // 循环处理，防止一次性收到了两个包
                while (_buffer.Count >= 2) // 至少要包含帧头(AA)和长度位
                {
                    // 1. 寻找帧头 0xAA
                    if (_buffer[0] != 0xAA)
                    {
                        _buffer.RemoveAt(0); // 不是帧头，扔掉，继续找
                        continue;
                    }

                    // 2. 确定后续数据长度 (假设第2个字节是长度)
                    int dataLength = _buffer[1];
                    int expectedFullLength = 2 + dataLength; // 帧头+长度位+数据位

                    // 3. 检查桶里的水够不够一个包
                    if (_buffer.Count >= expectedFullLength)
                    {
                        // 够了！提取这一个完整包
                        byte[] fullPacket = _buffer.GetRange(0, expectedFullLength).ToArray();

                        // 移除已处理的数据
                        _buffer.RemoveRange(0, expectedFullLength);

                        // 4. 抛给 ViewModel (注意跨线程)
                        FullPacketReceivedEvent?.Invoke(fullPacket);
                    }
                    else
                    {
                        // 水不够，等下一次 DataReceived 触发再凑
                        break;
                    }
                }
            }
        }


        public void SendData(byte[] data)
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Write(data, 0, data.Length);
            }
        }

        public async Task SendDataAsync(byte[] data)
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                try
                {
                    await _serialPort.BaseStream.WriteAsync(data, 0, data.Length);
                    await _serialPort.BaseStream.FlushAsync();
                }
                catch (Exception ex)
                {
                    throw new Exception($"异步发送失败: {ex.Message}");
                }
            }
                }

    }
}
