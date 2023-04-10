using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;

using Windows.UI.Xaml;


namespace Barnaby
{
    public class DisplayEntity
    {
        public uint Handle { get; set; }
        public bool Enabled { get; set; }
    }

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private App _app = null;

        private void BtConnectToClick(object sender, RoutedEventArgs e)
        {
            try
            {
                string serverIP = TbServerIP.Text;
                ushort serverPort = 0;
                UInt16.TryParse(TbServerPort.Text, out serverPort);
                _app.TriggerConnect(serverIP, serverPort);
            } catch (Exception ex)
            {
            }
        }
        public MainPage()
        {
            _app = (App)Microsoft.UI.Xaml.Application.Current;
            this.InitializeComponent( );
            List<DisplayEntity> listDisplayEntities = new List<DisplayEntity>();
            listDisplayEntities.Add(new DisplayEntity() { Handle = 0xcafe0001, Enabled = true });
            listDisplayEntities.Add(new DisplayEntity() { Handle = 0xcafe0000, Enabled = true });
            listDisplayEntities.Add(new DisplayEntity() { Handle = 0xcafe0002, Enabled = true });
            listDisplayEntities.Add(new DisplayEntity() { Handle = 0xcafe0003, Enabled = true });
            listDisplayEntities.Add(new DisplayEntity() { Handle = 0xcafe0004, Enabled = true });
            listDisplayEntities.Add(new DisplayEntity() { Handle = 0xcafe0005, Enabled = true });
            lvDisplayEntities.ItemsSource = listDisplayEntities;

            // long result = _app.WireClient.Calculate(2, 4, "*");
            // Console.WriteLine($"Barnaby notices that result is {result}.");
            // listDisplayEntities.Add(new DisplayEntity() { Handle = (uint)result, Enabled = true });
        }
    }
}
