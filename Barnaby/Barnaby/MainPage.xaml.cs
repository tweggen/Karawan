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


        private void _reloadEntities()
        {
            List<DisplayEntity> listDisplayEntities = new List<DisplayEntity>();
            if (!_app.IsConnected())
            {
                lvDisplayEntities.ItemsSource = listDisplayEntities;
                return;
            }

            var entities = _app.WireClient.GetEntities();
            foreach (var entity in entities)
            {
                //{ "outline": "Entity 1:1.0" }
                string outline = entity.Outline;
                try
                {
                    int ofsEntityId = outline.IndexOf(':');
                    int ofsVersion = outline.IndexOf('.');
                    short worldId = Int16.Parse(outline.Substring(7, ofsEntityId-7));
                    int entityId = Int32.Parse(outline.Substring(ofsEntityId+1, ofsVersion-(ofsEntityId+1)));
                    int version = Int16.Parse(outline.Substring(ofsVersion+1));
                    listDisplayEntities.Add(new DisplayEntity() { Handle = (uint)entityId, Enabled = -1 != entityId });
                }
                catch (Exception e)
                {
                    Console.WriteLine($"_readEntites(): Unable to parse entity: {e}");
                }
            }
            lvDisplayEntities.ItemsSource = listDisplayEntities;
        }

        
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


        private void BtPause(object sender, RoutedEventArgs e)
        {
            if (!_app.IsConnected())
            {
                _app.ShowNotConnected();
                return;
            }
            _app.WireClient.Pause();
            _reloadEntities();
        }

        
        private void BtContinue(object sender, RoutedEventArgs e)
        {
            if (!_app.IsConnected())
            {
                _app.ShowNotConnected();
                return;
            }
            _app.WireClient.Continue();
        }


        
        public MainPage()
        {
            _app = (App)Microsoft.UI.Xaml.Application.Current;
            this.InitializeComponent( );

            // long result = _app.WireClient.Calculate(2, 4, "*");
            // Console.WriteLine($"Barnaby notices that result is {result}.");
            // listDisplayEntities.Add(new DisplayEntity() { Handle = (uint)result, Enabled = true });
        }
    }
}
