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

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Barnaby
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public class DisplayEntity
        {
            public uint Handle { get; set; }
            public bool Enabled {  get; set; }
        }

        public MainPage()
        {
            this.InitializeComponent( );
            List<DisplayEntity> listDisplayEntities = new List<DisplayEntity>();
            listDisplayEntities.Add(new DisplayEntity() { Handle = 0xcafe0001, Enabled = true });
            listDisplayEntities.Add(new DisplayEntity() { Handle = 0xcafe0000, Enabled = true });
            listDisplayEntities.Add(new DisplayEntity() { Handle = 0xcafe0002, Enabled = true });
            listDisplayEntities.Add(new DisplayEntity() { Handle = 0xcafe0003, Enabled = true });
            listDisplayEntities.Add(new DisplayEntity() { Handle = 0xcafe0004, Enabled = true });
            listDisplayEntities.Add(new DisplayEntity() { Handle = 0xcafe0005, Enabled = true });
            lvDisplayEntities.ItemsSource = listDisplayEntities;

            WireClient.API aWireClient = new("127.0.0.1", 9001);
            long result = aWireClient.Calculate(2, 4, "*");
            Console.WriteLine($"Barnaby notices that result is {result}.");
        }
    }
}
