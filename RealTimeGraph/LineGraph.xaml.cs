﻿using OxyPlot;
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

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace RealTimeGraph {
    public class MainViewModel {
        public const int MAX_DATA_SAMPLES = 960;
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
                AbsoluteMinimum = -8f,
                AbsoluteMaximum = 8f,
                Minimum = -8f,
                Maximum = 8f,
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
        private IAccelerometer accelerometer;
        private ISensorFusionBosch sensorFusion;

        public LineGraph() {
            InitializeComponent();
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            var samples = 0;
            var model = (DataContext as MainViewModel).MyModel;

            metawear = MbientLab.MetaWear.Win10.Application.GetMetaWearBoard(e.Parameter as BluetoothLEDevice);

            //accelerometer = metawear.GetModule<IAccelerometer>();
            //accelerometer.Configure(odr: 100f, range: 8f);

            sensorFusion = metawear.GetModule<ISensorFusionBosch>();
            sensorFusion.Configure();  // default settings is NDoF mode with +/-16g acc range and 2000dps gyro range

            print("Sensor fusion configured.");

            //await accelerometer.Acceleration.AddRouteAsync(source => source.Stream(async data => {
            //    var value = data.Value<Acceleration>();
            await sensorFusion.Quaternion.AddRouteAsync(source => source.Stream(async data => {
                var value = data.Value<Quaternion>();
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                    (model.Series[0] as LineSeries).Points.Add(new DataPoint(samples, value.X));
                    (model.Series[1] as LineSeries).Points.Add(new DataPoint(samples, value.Y));
                    (model.Series[2] as LineSeries).Points.Add(new DataPoint(samples, value.Z));
                    samples++;

                    model.InvalidatePlot(true);
                    if (samples > MainViewModel.MAX_DATA_SAMPLES) {
                        model.Axes[1].Reset();
                        model.Axes[1].Maximum = samples;
                        model.Axes[1].Minimum = (samples - MainViewModel.MAX_DATA_SAMPLES);
                        model.Axes[1].Zoom(model.Axes[1].Minimum, model.Axes[1].Maximum);
                    }
                });
            }));
        }

        private async void back_Click(object sender, RoutedEventArgs e) {
            if (!metawear.InMetaBootMode) {
                metawear.TearDown();
                await metawear.GetModule<IDebug>().DisconnectAsync();
            }
            Frame.GoBack();
        }

        private void streamSwitch_Toggled(object sender, RoutedEventArgs e) {
            if (streamSwitch.IsOn) {
                //accelerometer.Acceleration.Start();
                //accelerometer.Start();
                sensorFusion.Quaternion.Start();
                sensorFusion.Start();
            } else {
                //accelerometer.Acceleration.Stop();
                //accelerometer.Stop();
                sensorFusion.Quaternion.Stop();
                sensorFusion.Stop();
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // saveData();
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            /*
            for (int i = 0; i < numBoards; i++)
            {
                data[i] = new List<String>();
                dataStrings[i] = "";
                dataNums[i] = 0;
                dataPoints[i].Clear();
            }
            refreshChart();
            if (!isRunning)
            {
                Clear.Background = new SolidColorBrush(Windows.UI.Colors.Gray);
            }
            */
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