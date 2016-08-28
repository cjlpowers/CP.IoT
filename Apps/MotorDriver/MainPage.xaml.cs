using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

using Windows.Devices.Enumeration;
using Windows.Devices.Spi;
using Windows.Devices.Gpio;
using Windows.Devices.Pwm;

using Microsoft.IoT.Lightning.Providers;

using CP.Windows.IoT.Chips;
using System.Collections;

namespace MotorDriver
{
    /// <summary>
    /// The main page
    /// </summary>
    public sealed partial class MainPage : Page
    {
        #region Internal Constructs
        private class Sequence<T> : IEnumerable<T>
        {
            private IEnumerable<T> mValues;

            public Sequence(params IEnumerable<T>[] values)
            {
                this.mValues = new List<T>(values.SelectMany(x=>x));
            }

            public IEnumerator<T> GetEnumerator()
            {
                while (true)
                {
                    foreach (var value in this.mValues)
                        yield return value;
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }
        #endregion

        #region Constants
        private const int MotorAEnablePin = 22;
        private const int MotorAInputPin = 27;
        private const int MotorBEnablePin = 18;
        private const int MotorBInputPin = 15;
        private const int PwmFrequency = 300;
        private const int MinTime = 1;
        private const int MaxTime = 1000;
        #endregion

        #region Variables
        private Queue<double> mOffsetQueue;
        #endregion

        #region Properties
        private Dictionary<string, Sequence<double>> Sequences
        {
            get;
            set;
        }

        private Sequence<double> SelectedSequence
        {
            get;
            set;
        }

        private double MaxPower
        {
            get;
            set;
        }

        private double MinPower
        {
            get;
            set;
        }

        private double SpeedFactor
        {
            get;
            set;
        }

        private int MotorOffset
        {
            get;
            set;
        }
        #endregion

        #region Creation
        public MainPage()
        {
            this.mOffsetQueue = new Queue<double>();
            this.InitializeComponent();

            this.Sequences = new Dictionary<string, Sequence<double>>
            {
                { "Constant", new Sequence<double>(new double[] { 1 }) },
                { "Step", new Sequence<double>(new double[] { 0, 1 }) },
                { "Step (multi)", new Sequence<double>(new double[] { 0, 0.25, 0, 0.25, 0, 0.5, 0.25, 1 }) },
                { "Sawtooth", new Sequence<double>(new double[] { 0, 0.25, 0.5, 0.75, 1 }) },
                { "Ramp", new Sequence<double>(new double[] { 0, 0.1, 0.25, 0.4, 0.6, 1 }) },
                { "Sinusoidal", new Sequence<double>(SinusoidalSequence()) },
                { "Sinusoidal (multi)", new Sequence<double>(SinusoidalSequence(0.5), SinusoidalSequence(0.7), SinusoidalSequence(0.5), SinusoidalSequence(1)) },
                { "Random", new Sequence<double>(RandomSequence(30, 0 , 1)) },
            };
            this.SelectedSequence = Sequences["Constant"];

            // populate the sequence list
            foreach (var entry in this.Sequences.Keys.OrderBy(x => x))
                this.SequenceList.Items.Add(entry);

            Task.Run(async () =>
            {
                try
                {
                    if (!LightningProvider.IsLightningEnabled)
                        throw new Exception("Requires Lighting to be enabled");

                    var pwmController = (await PwmController.GetControllersAsync(LightningPwmProvider.GetPwmProvider()))[1];
                    if (pwmController == null)
                        throw new Exception("No PWM controller");

                    var gpioController = (await GpioController.GetControllersAsync(LightningGpioProvider.GetGpioProvider()))[0];
                    if (gpioController == null)
                        throw new Exception("No PWM controller");

                    pwmController.SetDesiredFrequency(PwmFrequency);
                    var motorAEnable = pwmController.OpenPin(MotorAEnablePin);
                    var motorAInput = gpioController.OpenPin(MotorAInputPin);
                    motorAInput.SetDriveMode(GpioPinDriveMode.Output);
                    var motorBEnable = pwmController.OpenPin(MotorBEnablePin);
                    var motorBInput = gpioController.OpenPin(MotorBInputPin);
                    motorBInput.SetDriveMode(GpioPinDriveMode.Output);

                    var motorDriver = new L239D()
                    {
                        Enable12Pin = motorAEnable,
                        Input1Pin = motorAInput,
                        Enable34Pin = motorBEnable,
                        Input3Pin = motorBInput,
                    };

                    motorDriver.StartMotor(L239D.InputPins.Input1);
                    motorDriver.StartMotor(L239D.InputPins.Input3);

                    while (true)
                    {
                        var sequence = this.SelectedSequence;
                        foreach (var value in sequence)
                        {
                            var power = this.MinPower + value * (this.MaxPower - this.MinPower);
                            this.mOffsetQueue.Enqueue(power);
                            motorDriver.DriveMotor(L239D.EnablePins.Enable12, power);

                            // handle offset if any
                            while (this.mOffsetQueue.Count > this.MotorOffset)
                                power = this.mOffsetQueue.Dequeue();

                            motorDriver.DriveMotor(L239D.EnablePins.Enable34, power);

                            await Task.Delay((int)(MinTime + (MaxTime - MinTime) * (1.0 - this.SpeedFactor)));

                            if (sequence != this.SelectedSequence)
                                break;
                        }
                    }

                    motorAEnable.Dispose();
                    motorAInput.Dispose();
                }
                catch (Exception ex)
                {
                    var error = ex.ToString();
                }
            });
        }
        #endregion

        #region Control Sequences
        static private IEnumerable<double> SinusoidalSequence(double scale = 1)
        {
            var length = 10;
            var sequence = new List<double>(length);
            for (var i = 0; i < length; i++)
                sequence.Add((Math.Sin(Math.PI * 2 * i / length) * scale + scale)/2);
            return sequence;
        }

        static private IEnumerable<double> RandomSequence(int length = 30, double minValue = 0, double maxValue = 1)
        {
            var random = new Random();
            var sequence = new List<double>(length);
            for (var i = 0; i < length; i++)
                sequence.Add(random.NextDouble() * (maxValue - minValue) + minValue);
            return sequence;
        }
        #endregion

        #region Event Handlers
        private void MaxPowerSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            this.MaxPower = e.NewValue / 100;
            if (this.MinPowerSlider.Value > this.MaxPowerSlider.Value)
                this.MinPowerSlider.Value = this.MaxPowerSlider.Value;
        }

        private void MinPowerSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            this.MinPower = e.NewValue / 100;
            if (this.MaxPowerSlider.Value < this.MinPowerSlider.Value)
                this.MaxPowerSlider.Value = this.MinPowerSlider.Value;
        }

        private void SpeedSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            this.SpeedFactor = e.NewValue / 100;
        }

        private void SequenceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Sequence<double> sequence;
            if (this.Sequences.TryGetValue(e.AddedItems.OfType<string>().FirstOrDefault(), out sequence))
                this.SelectedSequence = sequence;
        }

        private void MotorOffsetSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            this.MotorOffset = (int)e.NewValue;
        }
        #endregion
    }
}
