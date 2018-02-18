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
using System.Text;

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

        int numBoards = 1;
        //private IntPtr cppBoard;
        private IntPtr[] boards;
        bool startNext = true;
        bool isRunning = false;
        bool[] centered = { false, false };
        bool[] shouldCenter = { false, false };
        Stopwatch myStopWatch = new Stopwatch();
        List<DataPoint>[] dataPoints = { new List<DataPoint>(), new List<DataPoint>() };
        int[] freq = { 0, 0 };
        Quaternion[] centerQuats = new Quaternion[2];
        PlotModel model;
        int[] samples = { 0,0 };
        int secs = 0;
        StringBuilder[] csv = {new StringBuilder(), new StringBuilder() };
        TextBlock[] textblocks = new TextBlock[2];
        private System.Threading.Timer timer1;

        public LineGraph() {
            InitializeComponent();
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);
            metawear = MbientLab.MetaWear.Win10.Application.GetMetaWearBoard(e.Parameter as BluetoothLEDevice);
            textblocks[0] = DataTextBlock1;
            textblocks[1] = DataTextBlock2;

            InitBatteryTimer();
            // MEEEEEE
            if (numBoards == 1)
            {
                removeBoardTwoFormatting();
            }

            model = (DataContext as MainViewModel).MyModel;
        }
        
        public void removeBoardTwoFormatting()
        {
            dataGrid.Children.RemoveAt(1);
            dataGrid.ColumnDefinitions.RemoveAt(1);

            controlGrid.Children.Remove(FrequencyTextBlock2);
            controlGrid.Children.Remove(BatteryTextBlock2);
            controlGrid.ColumnDefinitions.RemoveAt(1);
        }

        public void InitFreqTimer()
        {
            timer1 = new System.Threading.Timer(displaySampleFreq, null, 0, 1000);
        }

        public void InitBatteryTimer()
        {
            timer1 = new System.Threading.Timer(displayBatteryLevel,null,0,10000);
        }

        public async void displayBatteryLevel(Object state)
        {
            byte battery1 = await metawear.ReadBatteryLevelAsync();
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                BatteryTextBlock1.Text = battery1.ToString() + " %";
            });
        }

        private async void displaySampleFreq(Object state)
        {
            secs += 1;
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
                FrequencyTextBlock1.Text = freq[0] + " Hz";
                FrequencyTextBlock2.Text = freq[1] + " Hz";
                AverageFrequencyTextBlock.Text = samples[0] / secs + "Hz";
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

        void setText(Quaternion q, int sensorNumber)
        {
            String s = "W: " + q.W.ToString() + "\nX: " + q.X.ToString() + "\nY: " + q.Y.ToString() + "\nZ: " + q.Z.ToString();
            Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { textblocks[sensorNumber].Text = s; });
        }

        private async void streamSwitch_Toggled(object sender, RoutedEventArgs e) {
            if (streamSwitch.IsOn) {
                Clear_Click(null, null);
                myStopWatch.Start();
                isRunning = true;
                samples[0] = 0;

                sensorFusion = metawear.GetModule<ISensorFusionBosch>();
                sensorFusion.Configure();  // default settings is NDoF mode with +/-16g acc range and 2000dps gyro range

                await sensorFusion.Quaternion.AddRouteAsync(source => source.Stream(async data => {
                    var value = data.Value<Quaternion>();
                    if (isRunning)
                    {
                        var secs = myStopWatch.ElapsedMilliseconds * 0.001;
                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                            (model.Series[0] as LineSeries).Points.Add(new DataPoint(samples[0], value.X));
                            (model.Series[1] as LineSeries).Points.Add(new DataPoint(samples[0], value.Y));
                            (model.Series[2] as LineSeries).Points.Add(new DataPoint(samples[0], value.Z));
                            samples[0]++;
                            freq[0]++;
                            setText(value,0);
                            String newLine = string.Format("{0},{1},{2},{3},{4}{5}", samples[0],value.W,value.X,value.Y,value.Z,Environment.NewLine);
                            addPoint(newLine, 0);

                            model.InvalidatePlot(true);
                            //if (secs > MainViewModel.MAX_SECONDS)
                            if (samples[0] > MainViewModel.MAX_DATA_SAMPLES)
                            {
                                model.Axes[1].Reset();
                                //model.Axes[1].Maximum = secs;
                                //model.Axes[1].Minimum = secs - MainViewModel.MAX_SECONDS;
                                model.Axes[1].Maximum = samples[0];
                                model.Axes[1].Minimum = (samples[0] - MainViewModel.MAX_DATA_SAMPLES);
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
            saveData();
        }

        private async Task saveData(int sensorNumber = 1)
        {
            print("save initiated for sensor: ");
            print(sensorNumber.ToString());

            //for (int i = 0; i < numBoards; i++)  {
            var savePicker = new Windows.Storage.Pickers.FileSavePicker();
            // Default start location
            //savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            // Dropdown of file types the user can save the file as
            savePicker.FileTypeChoices.Add("Plain Text", new List<string>() { ".txt" });
            // Default file name if the user does not type one in or select a file to replace
            savePicker.SuggestedFileName = "New Document";

            Windows.Storage.StorageFile file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                // Prevent updates to the remote version of the file until
                // we finish making changes and call CompleteUpdatesAsync.
                Windows.Storage.CachedFileManager.DeferUpdates(file);
                // write to file
                await Windows.Storage.FileIO.WriteTextAsync(file, csv[sensorNumber - 1].ToString());
                // Let Windows know that we're finished changing the file so
                // the other app can update the remote version of the file.
                // Completing updates may require Windows to ask for user input.
                Windows.Storage.Provider.FileUpdateStatus status =
                    await Windows.Storage.CachedFileManager.CompleteUpdatesAsync(file);
                if (status == Windows.Storage.Provider.FileUpdateStatus.Complete)
                {
                    //this.textBlock.Text = "File " + file.Name + " was saved.";
                    if (sensorNumber == 1 && numBoards == 2)
                    {
                        saveData(2);
                    }
                }
                else
                {
                    //this.textBlock.Text = "File " + file.Name + " couldn't be saved.";
                }
            }
            else
            {
                //this.textBlock.Text = "Operation cancelled.";
            }
            //	}
        }

        private async void Clear_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < numBoards; i++)
            {
                //data[i] = new List<String>();
                //dataStrings[i] = "";
                //dataNums[i] = 0;
                //dataPoints[i].Clear();
                csv[i] = new StringBuilder();
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
            shouldCenter[0] = true;
            shouldCenter[1] = true;
        }

        void addPoint(String s, int sensorNumber)
        {
            if (isRunning)
            {
                csv[sensorNumber].Append(s);
            }
        }

        private void print(String s)
        {
            System.Diagnostics.Debug.WriteLine(s);
        }
    }
}
