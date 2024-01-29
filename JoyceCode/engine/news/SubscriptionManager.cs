using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using BepuUtilities;
using engine.joyce.components;
using ObjLoader.Loader.Common;
using static engine.Logger;

namespace engine.news;

class SubscriberEntry : IComparable<SubscriberEntry>
{
    public string Path;
    public List<string> SubscriptionPath;
    public Action<Event> Handler;
    public int SerialNumber;
    
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
    private object _lo = new();
    private int _nextSerial = 1000;

    private PathNode _root = new() { Path = "", References = 1};

   
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
    

    public void Subscribe(string path, Action<Event> handler)
    {
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
            SerialNumber = 0
        };
        lock (_lo)
        {
            se.SerialNumber = _nextSerial++;
            _root.AddChild(listPath, se);
        }
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
        lock (_lo)
        {
            _root.RemoveChild(listPath, seRef);
        }
    }


    private void _findSubscribers(PathNode nodeCurr, List<string> pathListEvent, int pathIndex, List<Action<Event>> listActions)
    {
        /*
         * nodeCurr is a match. It's subscribers can be added to the list of actions.
         */
        if (null != nodeCurr.Subscribers)
        {
            foreach (var sub in nodeCurr.Subscribers)
            {
                listActions.Add(sub.Handler);
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
            _findSubscribers(nodeChild, pathListEvent, pathIndex+1, listActions);
        }
    }


    public void Handle(Event ev)
    {
        List<Action<Event>> listActions;
        lock (_lo)
        {
            var pathListEvent = _createList(ev.Type);
            listActions = new();
            _findSubscribers(_root, pathListEvent, 0, listActions);
        }

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
            List<Action<Event>> matchedActions = new();
            registerSubs(pathes, subman, out var a);
            lock (subman._lo)
            {
                subman._findSubscribers(subman._root, _createList("event.key"), 0, matchedActions);
                if (matchedActions.Count != 1 || !matchedActions.Contains(a[2]))
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

            List<Action<Event>> matchedActions = new();
            registerSubs(pathes, subman, out var a);
            lock (subman._lo)
            {
                subman._findSubscribers(subman._root, _createList("input.mouse.released"), 0, matchedActions);
                if (matchedActions.Count != 1 || !matchedActions.Contains(a[0]))
                {
                    Error("Wrong result.");
                    result -= 1;
                }
            }
        }
        return result;
    }
}