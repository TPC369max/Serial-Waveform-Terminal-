using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Threading;

namespace Yell
{
    public partial class MainWindow : Window
    {
        private double[] _dataBuffer = new double[1000];
        private int _nextDataIndex = 0;
        private ScottPlot.Plottable.SignalPlot _signalPlot;

        // 【关键】标志位，确保整个生命周期只订阅一次
        private bool _isInitialized = false;

        public MainWindow()
        {
            InitializeComponent();
            InitPlotV4();

            // 1. 订阅事件，为了应对未来可能的 DataContext 切换
            this.DataContextChanged += OnDataContextChanged;

            // 2. 【核心修复】如果 DataContext 已经在 XAML 里加载好了，手动调用一次初始化逻辑
            if (this.DataContext != null)
            {
                // 我们手动构造一个“伪造”的事件参数传进去，或者直接提取逻辑
                InitializeViewModelLogic(this.DataContext);
            }
        }

        // 提取一个通用的初始化逻辑方法
        private void InitializeViewModelLogic(object dc)
        {
            if (dc is MainViewModel vm)
            {
                // 确保不重复订阅
                ((INotifyCollectionChanged)vm.Logs).CollectionChanged -= OnLogsChanged;
                ((INotifyCollectionChanged)vm.Logs).CollectionChanged += OnLogsChanged;

                vm.OnNewDataParsed -= Vm_OnNewDataParsed;
                vm.OnNewDataParsed += Vm_OnNewDataParsed;
            }
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // 当 DataContext 发生变化时，调用初始化逻辑
            InitializeViewModelLogic(e.NewValue);
        }

        void Vm_OnNewDataParsed(double value)
        {

            if (_nextDataIndex < _dataBuffer.Length)
            {
                _dataBuffer[_nextDataIndex] = value;
                _nextDataIndex++;
            }
            else
            {
                Array.Clear(_dataBuffer, 0, _dataBuffer.Length);
                _nextDataIndex = 0;
            }

            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() => {
                RealTimePlot.Render();
            }));
        }

        private void OnLogsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // 只在添加新行，且用户勾选了自动滚动时才去排队
            if (e.Action == NotifyCollectionChangedAction.Add && AutoScrollCheckBox.IsChecked == true)
            {
                // 使用 Lowest 优先级，确保“先画图，后滚动”，不卡顿
                Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                {
                    if (LogListBox.Items.Count > 0)
                    {
                        LogListBox.ScrollIntoView(LogListBox.Items[LogListBox.Items.Count - 1]);
                    }
                }));
            }
        }

        void InitPlotV4()
        {
            _signalPlot = RealTimePlot.Plot.AddSignal(_dataBuffer);
            RealTimePlot.Plot.Title("实时半导体测试波形 (v4)");
            RealTimePlot.Plot.SetAxisLimits(0, 1000, 0, 50);
            RealTimePlot.Refresh();
        }

        
    }
}