---

# 🏭 工业级高频串口波形上位机 (Industrial Serial Waveform Terminal)

[![C#](https://img.shields.io/badge/C%23-10.0%2B-blue.svg)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![WPF](https://img.shields.io/badge/WPF-.NET_6.0%2B-purple.svg)]()
[![MVVM](https://img.shields.io/badge/Architecture-MVVM-green.svg)]()
[![ScottPlot](https://img.shields.io/badge/Library-ScottPlot_4.x-orange.svg)]()

本开源项目是一个专为**半导体测试与自动化产线**设计的工业级串口通信与波形监控上位机。
本项目采用纯 **MVVM 架构**，结合**多线程异步 IO** 与 **ScottPlot 高性能渲染引擎**，完美解决了工业高频数据采集下的 UI 卡顿、数据丢包、断包粘包等核心痛点。

---

## ✨ 核心工业级特性 (Core Features)

- 🛡️ **底层通信防御体系**：
  - 基于 `List<byte>` 滑动窗口与帧头校验机制，彻底解决高频通信下的**“断包”与“粘包”**问题。
  - 严格的十六进制 (HEX) 数据转换体系，防范用户非法输入导致的程序崩溃。
- ⚡ **高性能数据渲染 (FPS 控制)**：
  - 引入 `ScottPlot` 引擎，弃用 WPF 原生绑定画图。
  - 采用 **数据缓存 + 33ms (30FPS) 定时器重绘** 的工业级渲染策略，实现千万级数据点下界面的极致流畅。
- 🧵 **异步非阻塞 IO (Async/Await)**：
  - 串口发送 (`WriteAsync`) 与本地日志持久化 (`File.AppendAllTextAsync`) 全面异步化。
  - 采用 `SemaphoreSlim` 异步锁控制文件流，防止多线程高并发下的文件占用冲突。
- 📊 **数据追溯与双轨存储**：
  - **内存双轨制**：UI 列表限制 1000 条防卡顿，底层业务缓存全量数据防丢失。
  - **全量导出机制**：一键导出包含 `TX/RX方向`、`原始报文`、`时间戳`与`解析值`的 CSV 文件，符合工业故障追溯标准（支持 UTF-8 BOM，Excel 打开无乱码）。
- 🎮 **内建数学仿真引擎**：
  - 无需连接物理硬件，点击“波形仿真”即可通过后台线程生成 `50Hz` 采样率的正弦波封包数据，完成从解包、渲染到落盘的全链路压力测试。

---

## 🏗️ 架构设计 (Architecture Design)

本项目严格遵循 **WPF + CommunityToolkit.Mvvm** 架构规范，实现 UI 与业务逻辑的绝对解耦：

*   **View (视图层)**：只负责数据绑定显示与控件触发。包含基于 `ListBox` 的高性能流式日志显示与自动滚动锁定。
*   **ViewModel (视图模型层)**：控制台枢纽。负责指令组装（自动封包）、协议拆包计算、日志分发，通过 `RelayCommand` 和 `ObservableProperty` 驱动界面。
*   **Model (模型层)**：`SerialManager` 封装底层串口逻辑，内部维护缓冲桶（Buffer），仅在拼凑出**完整协议帧**后，才通过自定义 Event `Action<byte[]>` 向上抛出。
*   **Infrastructure (基础设施)**：
    *   `HexConverter`：严格的 HEX <-> Byte 互相转换工具。
    *   `FileLogger`：负责每天生成滚动日志文件（`.log`），记录设备运行黑匣子。

---

## 🚀 快速开始 (Getting Started)

### 环境要求
*   Visual Studio 2022
*   .NET 6.0 或更高版本

### 依赖库 (NuGet)
*   `CommunityToolkit.Mvvm` (用于现代 MVVM 驱动)
*   `ScottPlot.WPF` (v4.1.71，用于波形渲染)

### 运行仿真模式 (无硬件测试)
1.  克隆本项目并编译运行。
2.  无需配置真实的串口参数。
3.  直接点击界面上的 **"开始波形仿真"** 按钮。
4.  观察下方日志列表的封包流动，以及波形图上呈现的平滑正弦波。
5.  点击 **"导出CSV"**，体验百万级数据的瞬间落盘。

---

## 📸 界面预览 (UI Screenshot)

`![Main UI](./Assets/screenshot.png)`

---
