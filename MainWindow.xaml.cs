using System.Collections.Specialized;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ScottPlot;
namespace Yell
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        double[] _dataBuffer = new double[1000];
        int _nextDataIndex = 0;
        ScottPlot.Plottable.SignalPlot _signalPlot;

        public MainWindow()
        {
            InitializeComponent();
            InitPlotV4();
            this.Loaded += (s, e) =>
            {
                if (this.DataContext is MainViewModel vm)
                {
                    ((INotifyCollectionChanged)vm.Logs).CollectionChanged += (sender, args) =>
                    {
                        if (args.Action == NotifyCollectionChangedAction.Add)
                        {
                            if (AutoScrollCheckBox.IsChecked == true)
                            {
                                LogListBox.ScrollIntoView(args.NewItems[0]);
                            }
                        }

                    };
                    vm.OnNewDataParsed += Vm_OnNewDataParsed;
                }
            };
        }

        void InitPlotV4()
        {
            _signalPlot = RealTimePlot.Plot.AddSignal(_dataBuffer);

            RealTimePlot.Plot.Title("实时半导体测试波形 (v4)");
            RealTimePlot.Plot.YLabel("物理量");
            RealTimePlot.Plot.SetAxisLimits(0, 1000, 0, 50);

            RealTimePlot.Refresh();

        }

        void Vm_OnNewDataParsed(double vaule)
        {
            if (_nextDataIndex < _dataBuffer.Length)
            {
                _dataBuffer[_nextDataIndex] = vaule;
                _nextDataIndex++;
            }
            else
            {
                Array.Clear(_dataBuffer, 0, _dataBuffer.Length);
                _nextDataIndex = 0;
            }
            Dispatcher.Invoke(() =>  RealTimePlot.Render() );
        }

    }
}