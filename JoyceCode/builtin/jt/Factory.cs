using System;
using System.Collections.Generic;
using System.Xml;
using engine;
using engine.news;
using static engine.Logger;

namespace builtin.jt;

public class Factory : AModule
{
    private class OSDLayer : IInputPart
    {
        public LayerDefinition LayerDefinition;
        public RootWidget RootWidget;
        public void InputPartOnInputEvent(Event ev)
        {
            if (ev.Type.StartsWith(Event.INPUT_MOUSE_ANY) || ev.Type.StartsWith(Event.INPUT_TOUCH_ANY))
            {
                return;
            }
        

            if (!ev.IsHandled)
            {
                RootWidget?.PropagateInputEvent(ev);
            }
        }
    }

    private SortedDictionary<string, OSDLayer> _mapOSDLayers = new();
    

    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<LayerCatalogue>(),
        new SharedModule<InputEventPipeline>()
    };
    

    private builtin.jt.ImplementationFactory _implementationFactory = I.Get<builtin.jt.ImplementationFactory>();
    public ImplementationFactory ImplementationFactory
    {
        get => _implementationFactory;
    }
    
    
    public RootWidget FindRootWidget(string layername)
    {
        lock (_lo)
        {
            return _mapOSDLayers[layername].RootWidget;
        }
    }


    public RootWidget Layer(string layername) => FindRootWidget(layername);


    /**
     * Create a new input controller for a given layer.
     */
    private OSDLayer _requireInputLayer(string layername)
    {
        LayerDefinition ld = M<LayerCatalogue>().Get(layername);
        
        OSDLayer osdLayer;
        lock (_lo)
        {
            if (!_mapOSDLayers.TryGetValue(layername, out osdLayer))
            {
                RootWidget wRoot = new RootWidget() { ImplementationFactory = _implementationFactory, Type = "Root" };
                osdLayer = new OSDLayer() { LayerDefinition = ld, RootWidget = wRoot };
                _mapOSDLayers[ld.Name] = osdLayer;
            }
        }

        return osdLayer;
    }


    public void CloseAll(string layername)
    {
        try
        {
            RootWidget wRoot = Layer(layername);
            var children = wRoot.Children;
            if (null != children)
            {
                foreach (var child in children)
                {
                    try
                    {
                        child.Parent = null;
                    }
                    catch (Exception e)
                    {
                        Error($"Unable to remove widget from parent: {e}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Error($"Exception executing close call.");
            return;
        }

    }
    

    public void CloseOSD(string layername, string id)
    {
        OSDLayer osdLayer;
        lock (_lo)
        {
            osdLayer = _mapOSDLayers[layername];
        }

        if (null == osdLayer.RootWidget)
        {
            Error($"No RootWidget active for module {layername}.");
            return;
        }

        osdLayer.RootWidget.GetChild(id, out var wVictim);
        if (null == wVictim)
        {
            Error($"No widget to close with id {id} in layer {layername}");
        }
        else
        {
            wVictim.Parent = null;
            wVictim.Dispose();
        }
    }


    public Widget OpenOSD(Parser parser, string id)
    {
        /*
         * Open the default menu.
         */
        var wMenu = parser.Build(id);
        if (null != wMenu)
        {
            string layername = wMenu["layer"].ToString();
            OSDLayer osdLayer = _requireInputLayer(layername);
            osdLayer.RootWidget.AddChild(wMenu);
            Widget? wFocussedChild = wMenu.FindFirstDefaultFocussedChild(wMenu);
            if (null == wFocussedChild)
            {
                wFocussedChild = wMenu.FindFirstFocussableChild();
            }
            if (null != wFocussedChild)
            {
                osdLayer.RootWidget.SetFocussedChild(wFocussedChild);
            }

            /*
             * Yes, we just keep adding new layers but do not remove old ones.
             */
            M<InputEventPipeline>().AddInputPart(osdLayer.LayerDefinition.ZOrder, osdLayer);
        }

        return wMenu;
    }
    
    
    public Widget OpenOSD(string filename, string id)
    {
        /*
         * Read the xml
         */
        XmlDocument xDoc = new XmlDocument();
        xDoc.Load(engine.Assets.Open(filename));
        
        /*
         * Parse the document
         */
        var parser = new Parser(xDoc, this);

        return OpenOSD(parser, id);
    }
    

}