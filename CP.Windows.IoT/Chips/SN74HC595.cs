using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Windows.Devices.Enumeration;
using Windows.Devices.Spi;

namespace CP.Windows.IoT.Chips
{
    /// <summary>
    /// A class which helps interface with the SN74HC595 8-Bit Shift Register
    /// </summary>
    public class SN74HC595
    {
        #region Variables
        protected SpiDevice Spi;
        #endregion

        #region Creation
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="spiModule">The SPI Module</param>
        public SN74HC595(SpiDevice spiDevice)
        {
            if (spiDevice == null)
                throw new ArgumentNullException("spiDevice");
            this.Spi = spiDevice;
        }

        public static async Task<SN74HC595> Create(int chipSelectLine)
        {
            var settings = new SpiConnectionSettings(chipSelectLine)
            {
                ClockFrequency = 1000000,
                Mode = SpiMode.Mode0,
                DataBitLength = 8
            };

            var aqs = SpiDevice.GetDeviceSelector();
            var dis = await DeviceInformation.FindAllAsync(aqs);
            if (!dis.Any())
                throw new Exception("No SPI controllers");
            var spi = await SpiDevice.FromIdAsync(dis[0].Id, settings);
            if (spi == null)
                throw new Exception(string.Format("SPI Controller '{0}' could not be initialized", dis[0].Id));

            return new SN74HC595(spi);
        }
        #endregion

        #region Methods
        public void Write(byte[] data)
        {
            this.Spi.Write(data);
        }

        public void Write(byte data)
        {
            Write(new byte[] { data });
        }
        #endregion
    }
}
