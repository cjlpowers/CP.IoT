using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Windows.Devices;
using Windows.Devices.Enumeration;
using Windows.Devices.Pwm;
using Windows.Devices.Gpio;

namespace CP.Windows.IoT.Chips
{
    /// <summary>
    /// A class which helps interface with the L239D Quadruple Half-H Driver
    /// </summary>
    public class L239D
    {
        #region Properties
        public PwmPin Enable12Pin
        {
            get;
            set;
        }

        public GpioPin Input1Pin
        {
            get;
            set;
        }

        public GpioPin Input2Pin
        {
            get;
            set;
        }

        public PwmPin Enable34Pin
        {
            get;
            set;
        }

        public GpioPin Input3Pin
        {
            get;
            set;
        }

        public GpioPin Input4Pin
        {
            get;
            set;
        }
        #endregion

        #region Pins
        public enum EnablePins
        {
            Enable12,
            Enable34
        }

        private PwmPin GetPin(EnablePins pin)
        {
            PwmPin pwmPin = null;
            if (pin == EnablePins.Enable12)
                pwmPin = this.Enable12Pin;
            else
                pwmPin = this.Enable34Pin;

            if (pwmPin == null)
                throw new Exception("Invalid Pin");

            return pwmPin;
        }

        public enum InputPins
        {
            Input1,
            Input2,
            Input3,
            Input4
        }

        private GpioPin GetPin(InputPins pin)
        {
            GpioPin gpioPin = null;
            if (pin == InputPins.Input1)
                gpioPin = this.Input1Pin;
            else if (pin == InputPins.Input2)
                gpioPin = this.Input2Pin;
            else if (pin == InputPins.Input3)
                gpioPin = this.Input3Pin;
            else if (pin == InputPins.Input4)
                gpioPin = this.Input4Pin;

            if (gpioPin == null)
                throw new Exception("Invalid Pin");

            return gpioPin;
        }
        #endregion

        #region Motor Control
        public void StartMotor(InputPins pin)
        {
            var inputPin = GetPin(pin);
            inputPin.Write(GpioPinValue.High);
        }

        public void StopMotor(InputPins pin)
        {
            var inputPin = GetPin(pin);
            inputPin.Write(GpioPinValue.Low);
        }

        public void DriveMotor(EnablePins pin, double dutyCyclePercentage)
        {
            var pwmPin = GetPin(pin);
            pwmPin.SetActiveDutyCyclePercentage(dutyCyclePercentage);
            if (!pwmPin.IsStarted)
                pwmPin.Start();
        }
        #endregion
    }
}
