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
            List<DisplayEntity> listDisplayEntities = new List<DisplayEntity>();
            if (!_app.IsConnected())
            {
                LvDisplayEntities.ItemsSource = listDisplayEntities;
                return;
            }

            var entities = _app.WireClient.GetEntities();
            foreach (var entityId in entities)
            { 
                listDisplayEntities.Add(new DisplayEntity()
                {
                    Handle = (uint)entityId, Enabled = true // TXWTODO; Read enabled.
                });
            }
            LvDisplayEntities.ItemsSource = listDisplayEntities;
        }

        
        private void _loadEntity(int entityId)
        {
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


        private void TbEntityClick(object sender, PointerRoutedEventArgs e)
        {
            if (!_app.IsConnected())
            {
                _app.ShowNotConnected();
                return;
            }

            Microsoft.UI.Xaml.Controls.TextBlock tb = (TextBlock)sender;
            int entityId;
            if (!Int32.TryParse(tb.Name, out entityId))
            {
                return;
            }

            _loadEntity(entityId);
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
