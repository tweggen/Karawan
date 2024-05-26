using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Numerics;
using BepuUtilities;
using engine.joyce.components;
using ObjLoader.Loader.Common;
using static engine.Logger;

namespace engine.news;

using DistanceFunction = Func<EmissionContext, float>;

public class EmissionContext
{
    public Vector3 PlayerPos;
    public Vector3 CameraPos;
}



class SubscriberEntry : IComparable<SubscriberEntry>
{
    public string Path;
    public List<string> SubscriptionPath;
    public Action<Event> Handler;
    public int SerialNumber;
    public DistanceFunction? DistanceFunc = null;
    
    public int CompareTo(SubscriberEntry other)
    {
        int thisLength = this.SubscriptionPath.Count;
        int otherLength = other.SubscriptionPath.Count;
        int l = Int32.Min(thisLength, otherLength);
        for (int i = 0; i < l; ++i)
        {
            int res = this.SubscriptionPath[i].CompareTo(other.SubscriptionPath[i]);
            if (0 != res)
            {
                return res;
            }
        }

        if (thisLength != otherLength)
        {
            return thisLength - otherLength;
        }
        else
        {
            return SerialNumber - other.SerialNumber;
        }
    }
}

class PathNode
{
    public required string Path;
    public int References = 0;
    public SortedDictionary<string, PathNode> Children = new();
    public List<SubscriberEntry>? Subscribers = null;

    public void AddChild(List<string> listPath, SubscriberEntry se)
    {
        int l = listPath.Count;
        PathNode currNode = this;
        PathNode childNode = null;

        for (int i = 0; i < l; i++)
        {
            string path = listPath[i];
            if (currNode.Children.TryGetValue(path, out childNode))
            {
            }
            else
            {
                /*
                 * Child node for this part of the path, with one reference for this part.
                 */
                childNode = new PathNode()
                {
                    Path = listPath[i],
                    References = 1
                };
                currNode.Children.Add(path, childNode);
            }
            
            /*
             * The parent node carrying the reference to the child node has an additional reference.
             */
            currNode.References++;
            currNode = childNode;
        }

        if (null == currNode.Subscribers)
        {
            currNode.Subscribers = new();
        }
        currNode.Subscribers.Add(se);
    }


    public void RemoveChild(List<string> listPath, SubscriberEntry seRef)
    {
        int l = listPath.Count;
        PathNode currNode = this;
        PathNode childNode = null;

        for (int i = 0; i < l; i++)
        {
            string path = listPath[i];
            if (!currNode.Children.TryGetValue(path, out childNode))
            {
                ErrorThrow($"Invalid tree unsubscribing {listPath}", m => new InvalidOperationException(m));
                return;
            }

            currNode = childNode;
            /*
             * The parent node carrying the reference to the child node has an additional reference.
             */
            --currNode.References;

            // TXWTODO: We might clean up unused nodes.
        }
        
        currNode.Subscribers.RemoveAll(se => se.Path == seRef.Path && se.Handler == seRef.Handler);
    }
}

public class SubscriptionManager
{
    enum Commands
    {
        Subscribe,
        Unsubscribe
    }
        
    class CommandEntry
    {
        public required Commands Command;
        public required string Path;
        public SubscriberEntry se;
        public required Action<Event> Handler;
    }
    
    
    private object _lo = new();
    private int _nextSerial = 1000;

    private PathNode _root = new() { Path = "", References = 1};

    /**
     * Set to true while I am handling an event. In that case, commands are queued.
     */
    private bool _inHandle = false;

    private Queue<CommandEntry> _queueCommands = new();
    

    void _handleCommandNL(CommandEntry ce)
    {
        switch (ce.Command)
        {
            case Commands.Subscribe:
                _root.AddChild(ce.se.SubscriptionPath, ce.se);
                break;
            case Commands.Unsubscribe:
                _root.RemoveChild(ce.se.SubscriptionPath, ce.se);
                break;
            
        }
    }
    

    void _queueAndTryCommand(CommandEntry ce)
    {
        lock (_lo)
        {
            /*
             * It's not really serious if we are 
             */
            if (_inHandle)
            {
                _queueCommands.Enqueue(ce);
            }
            else
            {
                _handleCommandNL(ce); 
            }
        }
        
    }
    
    
    private static List<string> _createList(string path)
    {
        string[] arrParts = path.Split('.');
        int l = arrParts.Length;
        while (l > 0)
        {
            if (arrParts[l - 1].IsNullOrEmpty())
            {
                --l;
                Array.Resize(ref arrParts, l);
            }
            else
            {
                break;
            }
        }

        return new List<string>(arrParts);
    }
    

    public void Subscribe(string path, Action<Event> handler, DistanceFunction distanceFunc = null)
    {
        if (path.Contains("widget"))
        {
            int a = 1;
        }
        var listPath = _createList(path);
        if (null == listPath)
        {
            ErrorThrow($"Invalid path {path}", m => new ArgumentException(m));
        }
        SubscriberEntry se = new()
        {
            Path = path,
            SubscriptionPath = listPath,
            Handler = handler,
            DistanceFunc = distanceFunc,
            SerialNumber = 0
        };
        lock (_lo)
        {
            se.SerialNumber = _nextSerial++;
        }
        CommandEntry ce = new()
        {
            Command = Commands.Subscribe,
            Handler = handler,
            Path = path,
            se = se
        };
        _queueAndTryCommand(ce);
    }

    
    public void Unsubscribe(string path, Action<Event> handler)
    {
        var listPath = _createList(path);
        if (null == listPath)
        {
            ErrorThrow($"Invalid path {path}", m => new ArgumentException(m));
        }
        SubscriberEntry seRef = new()
        {
            Path = path,
            SubscriptionPath = listPath,
            Handler = handler,
            SerialNumber = 0
        };
        CommandEntry ce = new()
        {
            Command = Commands.Unsubscribe,
            Handler = handler,
            Path = path,
            se = seRef
        };
        _queueAndTryCommand(ce);
    }


    private void _findSubscribers(PathNode nodeCurr, List<string> pathListEvent, int pathIndex, List<SubscriberEntry> listSubscriptionEntries, ref bool haveDistanceFunc)
    {
        /*
         * nodeCurr is a match. It's subscribers can be added to the list of actions.
         */
        if (null != nodeCurr.Subscribers)
        {
            foreach (var sub in nodeCurr.Subscribers)
            {
                if (sub.DistanceFunc != null)
                {
                    haveDistanceFunc = true;
                }
                listSubscriptionEntries.Add(sub);
            }
        }

        /*
         * Now, if this hasn't been the last segment in the path, recurse to the next one.
         */
        int l = pathListEvent.Count;
        if (pathIndex == l)
        {
            return;
        }


        string path = pathListEvent[pathIndex];

        /*
         * Look, if we can recurse into more specific subscriber pathes.
         */
        if (nodeCurr.Children.TryGetValue(path, out var nodeChild))
        {
            /*
             * There is a handler for this segment. Recurse into it.
             */
            _findSubscribers(nodeChild, pathListEvent, pathIndex+1, listSubscriptionEntries, ref haveDistanceFunc);
        }
    }


    public void Handle(Event ev, EmissionContext ectx)
    {
        List<Action<Event>> listActions = new();
        List<SubscriberEntry> listSubscribers = new();

        bool haveDistanceFunc = false;
        lock (_lo)
        {
            while (_queueCommands.Count > 0)
            {
                var ce = _queueCommands.Dequeue();
                _handleCommandNL(ce);
            }
            var pathListEvent = _createList(ev.Type);
            _findSubscribers(_root, pathListEvent, 0, listSubscribers, ref haveDistanceFunc);
            _inHandle = true;
        }
        
        /*
         * Now, if we have any matches that include a distance function, sort them using
         * the distance function.
         */
        if (haveDistanceFunc)
        {
            listSubscribers.Sort((a, b) =>
            {
                float distA = a.DistanceFunc != null ? a.DistanceFunc(ectx) : Single.MaxValue;
                float distB = b.DistanceFunc != null ? b.DistanceFunc(ectx) : Single.MaxValue;
                return Single.Sign(distA - distB);
            });
            listSubscribers.ForEach(sub => listActions.Add(sub.Handler));
        }

        listSubscribers = null;

        /*
         * Now, with no more references to the llist of subscribers pending, we
         * can finally execute the actions.
         */
        foreach (var action in listActions)
        {
            if (ev.IsHandled) break;
            try
            {
                action(ev);
            }
            catch (Exception e)
            {
                Warning($"Caught exception while handling event {ev}: {e}");
            }
        }

        lock (_lo)
        {
            _inHandle = false;
        }
    }


    static public int Unit()
    {
        var registerSubs = (string[] pathes, SubscriptionManager subman, out List<Action<Event>> a) =>
        {
            a = new();
            for (int i = 0; i < pathes.Length; ++i)
            {
                a.Add(ev => { });
                subman.Subscribe(pathes[i], a[i]);
            }
        };
        int result = 0;
        {
            var subman = new SubscriptionManager();

            string[] pathes =
            {
                "event.key.up", "event.key.down", "event", "event.collision", "absolutely.nothing", "freely.speaking"
            };
            List<SubscriberEntry> matchedSubscribers = new();
            registerSubs(pathes, subman, out var a);
            lock (subman._lo)
            {
                bool haveDistanceFunc = false;
                subman._findSubscribers(subman._root, _createList("event.key"), 0, matchedSubscribers, ref haveDistanceFunc);
                if (matchedSubscribers.Count != 1 || null != matchedSubscribers.Find(sub => sub.Handler == a[2]))
                {
                    Error("Wrong result.");
                    result -= 1;
                }
            }
        }

        {
            var subman = new SubscriptionManager();

            string[] pathes =
            {
                "input.", "input.mouse.pressed", "input.touch.pressed", "lifecycle.resume", "lifecycle.suspend",
                "map.range",
                "nogame.minimap.toggleMap", "nogame.playerhover.collision.anonymous",
                "nogame.playerhover.collision.car3",
                "nogame.playerhover.collision.cube",
                "nogame.playerhover.collision.polytope", "nogame.scenes.root.Scene.kickoff",
                "nogame.scenes.root.Scene.kickoff", "view.size.changed"
            };

            List<SubscriberEntry> matchedSubscribers = new();
            registerSubs(pathes, subman, out var a);
            lock (subman._lo)
            {
                bool haveDistanceFunc = false;
                subman._findSubscribers(subman._root, _createList("input.mouse.released"), 0, matchedSubscribers, ref haveDistanceFunc);
                if (matchedSubscribers.Count != 1 || null != matchedSubscribers.Find(sub => sub.Handler == a[0]))
                {
                    Error("Wrong result.");
                    result -= 1;
                }
            }
        }
        return result;
    }
}