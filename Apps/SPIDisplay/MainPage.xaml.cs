using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;

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

using CP.Windows.IoT.Components;
using CP.Windows.IoT.Chips;

namespace SPIDisplay
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        #region Constants
        private const int CHIP_SELECT_LINE = 0;
        #endregion

        #region Properties
        private bool KeepRunning
        {
            get;
            set;
        }
        #endregion

        #region Creation
        public MainPage()
        {
            this.KeepRunning = true;
            Application.Current.Suspending += Current_Suspending;
            this.InitializeComponent();

            Task.Run(async () =>
            {
                var ledPanel = await CreateLEDPanel();
                await DriveLEDPanel(ledPanel);
            });
        }
        #endregion

        #region Methods
        private async Task<LEDPanel> CreateLEDPanel()
        {
            var settings = new SpiConnectionSettings(CHIP_SELECT_LINE)
            {
                ClockFrequency = 40000,
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

            return new LEDPanel(spi, 5);
        }

        private Task DriveLEDPanel(LEDPanel panel)
        {
            TimeSpan updateInterval = TimeSpan.FromMinutes(5);
            DateTime lastUpdate = DateTime.MinValue;

            return Task.Run(async () =>
            {
                panel.Clear();

                System.Xml.Linq.XNamespace ns = "http://www.w3.org/2005/Atom";
                try
                {
                    IEnumerable<string> entries = null;
                    while (this.KeepRunning)
                    {

                        if (DateTime.Now > lastUpdate + updateInterval)
                        {
                            var request = System.Net.HttpWebRequest.CreateHttp("http://www.theverge.com/rss/index.xml");
                            var response = await request.GetResponseAsync();

                            var document = System.Xml.Linq.XDocument.Load(response.GetResponseStream());
                            entries = document
                                            .Descendants(ns + "title")
                                            .Select(x => x.Value);

                            lastUpdate = DateTime.Now;
                        }

                        if (entries != null)
                        {
                            foreach (var entry in entries)
                            {
                                var buffer = panel.Render("     " + entry + "     -     ");
                                panel.Display(buffer);
                            }
                        }
                        else
                            await Task.Delay(TimeSpan.FromSeconds(5));
                    }
                }
                catch (Exception ex)
                {
                    var buffer = panel.Render(ex.ToString());
                    panel.Display(buffer);
                }
            });
        }
        #endregion

        #region Event Handler
        private void Current_Suspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {
            this.KeepRunning = false;
        }
        #endregion
    }
}
