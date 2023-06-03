using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        public string Name { get; set; }
        public bool Enabled { get; set; }

        ObservableCollection<DisplayComponent> components;
    }


    public class DisplayComponent
    {
        public string Type { get; set; }
        public string Value { get; set; }

        public List<DisplayProperty> Properties;
    }


    public class DisplayProperty
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
    }
    
    
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private App _app = null;


        // private ObservableCollection<DisplayEntity> 

        private void _reloadEntities()
        {
            Console.WriteLine("_reloadEntities() called.");
            List<DisplayEntity> listDisplayEntities = new List<DisplayEntity>();
            if (!_app.IsConnected())
            {
                LvDisplayEntities.ItemsSource = listDisplayEntities;
                return;
            }

            var entityShorts = _app.WireClient.GetEntities();
            foreach (var entityShort in entityShorts)
            { 
                listDisplayEntities.Add(new DisplayEntity()
                {
                    Handle = (uint)entityShort.EntityId,
                    Name = entityShort.Name,
                    Enabled = true // TXWTODO; Read enabled.
                });
            }
            LvDisplayEntities.ItemsSource = listDisplayEntities;
        }

        
        private void _loadEntity(int entityId)
        {
            Console.WriteLine($"_loadEntity(): Trying to load entity {entityId}.");
            List<DisplayComponent> listDisplayComponents = new List<DisplayComponent>();
            if (!_app.IsConnected())
            {
                LvDisplayComponents.ItemsSource = listDisplayComponents;
                return;
            }

            Wire.Entity entity = _app.WireClient.GetEntity(entityId);
            foreach(Wire.Component comp in entity.Components)
            {
                DisplayComponent displayComponent = new()
                {
                    Type = comp.Type, Value = comp.Value, Properties = new()
                };
                foreach (Wire.CompProp prop in comp.Properties)
                {
                    DisplayProperty displayProperty = new()
                    {
                        Type = prop.Type, Name = prop.Name, Value = prop.Value
                    };
                    displayComponent.Properties.Add(displayProperty);
                }
                listDisplayComponents.Add(displayComponent);
            }

            LvDisplayComponents.ItemsSource = listDisplayComponents;
        }


        private async void _doConnectTo(string serverIP, ushort serverPort)
        {
            BtConnectTo.Content = "Connecting to ";
            BtConnectTo.IsEnabled = false;
            try
            {
                _app.TriggerConnect(serverIP, serverPort);
                bool isConnected = await _app.WireClient.IsReadyAsync(DateTime.Now.AddMilliseconds(100).ToUniversalTime());
                if (isConnected)
                {
                    BtConnectTo.Content = "Disconnect from "; 
                }
                else
                {
                    BtConnectTo.Content = "Connect to ";
                }
            }
            catch (Exception ex)
            {
                // this.Navigator().ShowMessageDialogAsync(this, 
                //    title: "Connectivity error", 
                //    content: $"Unable to connect to {serverIP}:{serverPort}");
                BtConnectTo.Content = "Connect to ";
            }
            BtConnectTo.IsEnabled = true;
        }


        private void _connectToCurrent()
        {
            string serverIP = TbServerIP.Text;
            ushort serverPort = 0;
            try
            {
                UInt16.TryParse(TbServerPort.Text, out serverPort);
            }
            catch (Exception ex)
            {
                return;
            }
            _doConnectTo(serverIP, serverPort);
        }
        
        
        private void BtConnectToClick(object sender, RoutedEventArgs e)
        {
            _connectToCurrent();
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


        private void TbEntityClick(object sender, PointerRoutedEventArgs e)
        {
            if (!_app.IsConnected())
            {
                Console.WriteLine("TbEntityClick(): Not connected.");
                _app.ShowNotConnected();
                return;
            }

            Microsoft.UI.Xaml.Controls.TextBlock tb = (TextBlock)sender;
            int entityId;
            if (!Int32.TryParse(tb.Name, out entityId))
            {
                Console.WriteLine($"TbEntityClick(): Unable to parse entity id ${tb.Name}");
                return;
            }

            _loadEntity(entityId);
        }


        private void _onLoaded(object sender, RoutedEventArgs a)
        {
            _connectToCurrent();
        }
        
        
        public MainPage()
        {
            _app = (App)Microsoft.UI.Xaml.Application.Current;
            this.InitializeComponent( );

            this.Loaded += _onLoaded;
            // long result = _app.WireClient.Calculate(2, 4, "*");
            // Console.WriteLine($"Barnaby notices that result is {result}.");
            // listDisplayEntities.Add(new DisplayEntity() { Handle = (uint)result, Enabled = true });

        }
    }
    
    class ExplorerItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ComponentTemplate { get; set; }
        public DataTemplate PropertyTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            if (item is DisplayComponent)
            {
                return ComponentTemplate;
            } 
            else if (item is DisplayProperty)
            {
                return PropertyTemplate;
            }
            else
            {
                return PropertyTemplate;
            }
        }
    }
}
