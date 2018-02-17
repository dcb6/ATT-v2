using OxyPlot;
using OxyPlot.Series;
using MbientLab.MetaWear;
using MbientLab.MetaWear.Core;
using MbientLab.MetaWear.Data;
using MbientLab.MetaWear.Sensor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.Core;
using Windows.Devices.Bluetooth;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Globalization.DateTimeFormatting;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using OxyPlot.Axes;

using WindowsQuaternion = System.Numerics.Quaternion;
using System.Diagnostics;
using Windows.UI.Xaml.Media;
using MbientLab.MetaWear.Core.Settings;
using System.Threading.Tasks;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace RealTimeGraph {
    public class MainViewModel {
        public const int MAX_DATA_SAMPLES = 960;
        public const int MAX_SECONDS = 10;
        public MainViewModel() {
            MyModel = new PlotModel {
                Title = "Angles",
                IsLegendVisible = true
            };
            MyModel.Series.Add(new LineSeries {
                BrokenLineStyle = LineStyle.Solid,
                MarkerStroke = OxyColor.FromRgb(1, 0, 0),
                LineStyle = LineStyle.Solid,
                Title = "x-axis"
            });
            MyModel.Series.Add(new LineSeries {
                MarkerStroke = OxyColor.FromRgb(0, 1, 0),
                LineStyle = LineStyle.Solid,
                Title = "y-axis"
            });
            MyModel.Series.Add(new LineSeries {
                MarkerStroke = OxyColor.FromRgb(0, 0, 1),
                LineStyle = LineStyle.Solid,
                Title = "z-axis"
            });
            MyModel.Axes.Add(new LinearAxis {
                Position = AxisPosition.Left,
                MajorGridlineStyle = LineStyle.Solid,
                AbsoluteMinimum = -1f,
                AbsoluteMaximum = 1f,
                Minimum = -1f,
                Maximum = 1f,
                Title = "Value"
            });
            MyModel.Axes.Add(new LinearAxis {
                IsPanEnabled = true,
                Position = AxisPosition.Bottom,
                MajorGridlineStyle = LineStyle.Solid,
                AbsoluteMinimum = 0,
                Minimum = 0,
                Maximum = MAX_DATA_SAMPLES
            });
        }

        public PlotModel MyModel { get; private set; }
    }

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class LineGraph : Page {

        private IMetaWearBoard metawear;
        private ISensorFusionBosch sensorFusion;

        int numBoards;
        //private IntPtr cppBoard;
        private IntPtr[] boards;
        bool startNext = true;
        string LiveData = "";
        bool isRunning = false;
        bool[] centered = { false, false };
        bool[] shouldCenter = { false, false };
        Stopwatch myStopWatch = new Stopwatch();


        List<DataPoint>[] dataPoints = { new List<DataPoint>(), new List<DataPoint>() };
        int[] dataNums = { 0, 0 };
        string[] dataStrings = { "", "" };
        int[] freq = { 0, 0 };
        Quaternion[] centerQuats = new Quaternion[2];
        List<string>[] data = new List<string>[2];

        PlotModel model;
        int samples;
        int secs = 0;

        //List<List<float>> saveData = new List<List<float>>();

        public LineGraph() {
            InitializeComponent();
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);
            metawear = MbientLab.MetaWear.Win10.Application.GetMetaWearBoard(e.Parameter as BluetoothLEDevice);
            InitBatteryTimer();
            // MEEEEEE
            numBoards = 1;

            model = (DataContext as MainViewModel).MyModel;

            print("Hello.");
        }

        private System.Threading.Timer timer1;
        public void InitFreqTimer()
        {
            timer1 = new System.Threading.Timer(displaySampleFreq, null, 0, 1000);

            //timer1.Tick += new EventHandler<object>(displaySampleFreq);
            //timer1.Interval = new TimeSpan(0, 0, 1);
            //timer1.Start();
        }

        public void InitBatteryTimer()
        {
            timer1 = new System.Threading.Timer(displayBatteryLevel,null,0,10000);
        }

        public async void displayBatteryLevel(Object state)
        {
            byte battery1 = await metawear.ReadBatteryLevelAsync();
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                BatteryTextBlock1.Text = battery1.ToString();
            });
        }

        private async void displaySampleFreq(Object state)
        {
            secs += 1;
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                FrequencyTextBlock1.Text = freq[0] + " Hz";
                FrequencyTextBlock2.Text = freq[1] + " Hz";
                AverageFrequencyTextBlock.Text = samples / secs + "Hz";
            });

            freq[0] = 0;
            freq[1] = 0;
        }

        private async void back_Click(object sender, RoutedEventArgs e) {
            if (!metawear.InMetaBootMode) {
                metawear.TearDown();
                await metawear.GetModule<IDebug>().DisconnectAsync();
            }
            Frame.GoBack();
        }

        private async void streamSwitch_Toggled(object sender, RoutedEventArgs e) {
            if (streamSwitch.IsOn) {
                Clear_Click(null, null);
                myStopWatch.Start();
                isRunning = true;
                samples = 0;

                sensorFusion = metawear.GetModule<ISensorFusionBosch>();
                sensorFusion.Configure();  // default settings is NDoF mode with +/-16g acc range and 2000dps gyro range

                await sensorFusion.Quaternion.AddRouteAsync(source => source.Stream(async data => {
                    var value = data.Value<Quaternion>();
                    if (isRunning)
                    {
                        var secs = myStopWatch.ElapsedMilliseconds * 0.001;
                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                            (model.Series[0] as LineSeries).Points.Add(new DataPoint(samples, value.X));
                            (model.Series[1] as LineSeries).Points.Add(new DataPoint(samples, value.Y));
                            (model.Series[2] as LineSeries).Points.Add(new DataPoint(samples, value.Z));
                            samples++;
                            freq[0]++;

                            model.InvalidatePlot(true);
                            //if (secs > MainViewModel.MAX_SECONDS)
                            if (samples > MainViewModel.MAX_DATA_SAMPLES)
                            {
                                model.Axes[1].Reset();
                                //model.Axes[1].Maximum = secs;
                                //model.Axes[1].Minimum = secs - MainViewModel.MAX_SECONDS;
                                model.Axes[1].Maximum = samples;
                                model.Axes[1].Minimum = (samples - MainViewModel.MAX_DATA_SAMPLES);
                                model.Axes[1].Zoom(model.Axes[1].Minimum, model.Axes[1].Maximum);
                            }

                        });
                    }
                }));

                sensorFusion.Quaternion.Start();
                sensorFusion.Start();
                InitFreqTimer();

                Clear.Background = new SolidColorBrush(Windows.UI.Colors.Red);
            } else {
                isRunning = false;
                sensorFusion.Stop();
                sensorFusion.Quaternion.Stop();
                metawear.TearDown();
                timer1.Dispose();
                myStopWatch.Stop();
                myStopWatch.Reset();
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // saveData();
        }

        private async void Clear_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < numBoards; i++)
            {
                data[i] = new List<String>();
                dataStrings[i] = "";
                dataNums[i] = 0;
                dataPoints[i].Clear();
            }
            
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                foreach (var series in model.Series)
                {
                    var lseries = series as LineSeries;
                    lseries.Points.Clear();
                }
                model.Axes[1].Reset();
                model.Axes[1].Maximum = 0;
                model.Axes[1].Minimum = MainViewModel.MAX_SECONDS;
                model.Axes[1].Zoom(model.Axes[1].Minimum, model.Axes[1].Maximum);
                model.InvalidatePlot(true);
            });


            if (!isRunning)
            {
                Clear.Background = new SolidColorBrush(Windows.UI.Colors.Gray);
            }
        }

        private void Center_Click(object sender, RoutedEventArgs e)
        {
            /*
            shouldCenter[0] = true;
            shouldCenter[1] = true;
            */
        }

        private void Stamp_Click(object sender, RoutedEventArgs e)
        {
            /*
            if (startNext == true)
            {
                String text = "START " + DateTime.Now + " " + printInput.Text;

                System.Diagnostics.Debug.WriteLine(text);
                addPoint(text, 1);
                addPoint(text, 2);
                startNext = false;
                stamp.Background = new SolidColorBrush(Windows.UI.Colors.MediumPurple);
                stamp.Content = "Print 'STOP' +\n";

            }
            else
            {
                String text = "STOP " + DateTime.Now + " " + printInput.Text;
                System.Diagnostics.Debug.WriteLine(text);
                addPoint(text, 1);
                addPoint(text, 2);
                startNext = true;
                stamp.Background = new SolidColorBrush(Windows.UI.Colors.CornflowerBlue);
                stamp.Content = "Print 'START' +\n";

            }
            */
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            /*
            if (!isRunning)
            {
                myStopWatch.Start();
                Clear_Click(null, null);
                Fn_IntPtr[] handlers = { quaternionDataHandler1, quaternionDataHandler2 };
                for (int i = 0; i < numBoards; i++)
                {
                    Clear.Background = new SolidColorBrush(Windows.UI.Colors.Red);

                    isRunning = true;

                    quatStart.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                    quatStart.Content = "Stop";

                    var cppBoard = boards[i];

                    if (quaternionCheckBox.IsChecked == true)
                    {
                        mbl_mw_settings_set_connection_parameters(cppBoard, 7.5F, 7.5F, 0, 6000);
                        mbl_mw_sensor_fusion_set_mode(cppBoard, SensorFusion.Mode.NDOF);
                        mbl_mw_sensor_fusion_set_acc_range(cppBoard, SensorFusion.AccRange.AR_16G); ///AR_2G, 4, 8, 16
						mbl_mw_sensor_fusion_set_gyro_range(cppBoard, SensorFusion.GyroRange.GR_2000DPS); ///GR_2000DPS, 1000, 500, 250

                        mbl_mw_sensor_fusion_write_config(cppBoard);

                        IntPtr quaternionDataSignal = mbl_mw_sensor_fusion_get_data_signal(cppBoard, SensorFusion.Data.QUATERION); //this line works

                        mbl_mw_datasignal_subscribe(quaternionDataSignal, handlers[i]);
                        mbl_mw_sensor_fusion_enable_data(cppBoard, SensorFusion.Data.QUATERION);
                        mbl_mw_sensor_fusion_start(cppBoard);
                    }
                }
            }
            else
            {
                myStopWatch.Stop();
                foreach (IntPtr cppBoard in boards)
                {
                    if (quaternionCheckBox.IsChecked == true)
                    {
                        IntPtr quatSignal = mbl_mw_sensor_fusion_get_data_signal(cppBoard, SensorFusion.Data.QUATERION);

                        mbl_mw_sensor_fusion_stop(cppBoard);
                        mbl_mw_sensor_fusion_clear_enabled_mask(cppBoard);
                        mbl_mw_datasignal_unsubscribe(quatSignal);
                    }
                }

                isRunning = false;
                refreshChart();

                quatStart.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                quatStart.Content = "Start";

                saveData();
            }
            */

        }

        private void print(String s)
        {
            System.Diagnostics.Debug.WriteLine(s);
        }
    }
}
