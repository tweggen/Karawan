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

    //private SortedList<SubscriberEntry, SubscriberEntry> _subscriberList = new();
    private PathNode _root = new() { Path = "", References = 1};

#if true
    private int _findFirstSubscriber(List<string> listPath)
    {
    }
#else
    /*
     * Return the index of the first entry greater or equal than path.
     */
    private int _findFirstSubscriber(string path)
    {
        int l = _subscriberList.Count;
        
        if (0 == l)
        {
            return -1;
        }

        int i0 = 0;
        int im = 0;
        int i1 = l - 1;
        int lastResult = 0;

        if (i0 == i1)
        {
            var subscribedPath = _subscriberList.GetKeyAtIndex(i0).SubscriptionPath;
            lastResult = String.CompareOrdinal(subscribedPath, 0, path, 0, subscribedPath.Length);
        }
        else
        {
            while (i0 != i1)
            {
                int il = i1 - i0 + 1;

                im = i0 + il / 2;
                var subscribedPath = _subscriberList.GetKeyAtIndex(im).SubscriptionPath;
                lastResult = String.CompareOrdinal(subscribedPath, 0, path, 0, subscribedPath.Length);

                if (lastResult < 0)
                {
                    /*
                     * im contains a path smaller than path.
                     */
                    i0 = im;
                }
                else
                {
                    if (lastResult == 0)
                    {
                        /*
                         * im contains the same path value than path.
                         * However, this does not mean this is the first path we match.
                         * 
                         * So continue to search for the beginning, including this end.
                         */
                        i1 = im - 1;
                    }
                    else
                    {
                        /*
                         * im contains a path value larger than path.
                         * Note: We know that im can't be equal to i0.
                         * However, im-1 might equal i0.
                         */
                        i1 = im - 1;
                    }
                }
            }

            if (i0 != im)
            {
                var subscribedPath = _subscriberList.GetKeyAtIndex(i0).SubscriptionPath;
                lastResult = String.CompareOrdinal(subscribedPath, 0, path, 0, subscribedPath.Length);
            }
        }

        /*
         * First and last is same.
         * So if the final comparison of that terminal entry shows, that it is smaller than path,
         * we found a smaller one. If it is the last one of the data structure, we do not have anything
         * to offer.
         * However, if the entry is greater or equal to the path, we found something.
         */
        if (lastResult < 0)
        {
            if (i0 == l - 1)
            {
                return -1;
            }
            else
            {
                return i0 + 1;
            }
        }
        else
        {
            return i0;
        }
    }
#endif
    
    
    private List<string> _createList(string path)
    {
        string[] arrParts = path.Split('.');
        int l = arrParts.Length;
        while (l > 0)
        {
            if (arrParts[l - 1].IsNullOrEmpty())
            {
                --l;
                arrParts.Resize(l);
            }
            else
            {
                break;
            }
        }
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


    private void _findSubscribers(PathNode nodeCurr, List<string> pathListEvent, int pathIndex, ref List<Action<Event>> listActions)
    {
        int l = pathListEvent.Count;
        string path = pathListEvent[pathIndex];

        /*
         * nodeCurr is a match. It's subscribers can be added to the list of actions.
         */
        if (null != nodeCurr.Subscribers)
        {
            foreach (var sub in nodeCurr.Subscribers)
            {
                listActions.Add(sub);
            }
        }

        /*
         * Look, if we can recurse into more specific subscriber pathes.
         */
        if (nodeCurr.Children.TryGetValue(path, out var nodeChild))
        {
            ++pathIndex;
            if (pathIndex == l)
            {
                /*
                 * No further recursion possible, no leaf in the tree.
                 */
                return;
            }
        }
    }


    public void Handle(Event ev)
    {
        if (ev.Type == Event.INPUT_MOUSE_RELEASED || ev.Type == Event.INPUT_TOUCH_RELEASED)
        {
            int a = 1;
        }
        if (ev.Type == Event.INPUT_MOUSE_PRESSED || ev.Type == Event.INPUT_TOUCH_PRESSED)
        {
            int a = 1;
        }
        if (ev.Type == Event.INPUT_MOUSE_MOVED)
        {
            int a = 1;
        }

        List<Action<Event>> listActions;
        lock (_lo)
        {
#if false
            int idx = _findFirstSubscriber(ev.Type);
            if (idx < 0)
            {
                return;
            }

            listActions = new();
            int l = _subscriberList.Count;
            while (idx<l)
            {
                var sub = _subscriberList.GetKeyAtIndex(idx);
                if (!ev.Type.StartsWith(sub.SubscriptionPath))
                {
                    break;
                }
                listActions.Add(sub.Handler);
                idx++;
            }
#else
            var pathListEvent = _createList(ev.Type);
            listActions = new();
            _findSubscribers(_root, pathListEvent, 0, ref listActions)
#endif
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
        int result = 0;
        {
            var subman = new SubscriptionManager();

            subman.Subscribe("event.key.up", ev => { });
            subman.Subscribe("event.key.down", ev => { });
            subman.Subscribe("event", ev => { });
            subman.Subscribe("event.collision", ev => { });
            subman.Subscribe("absolutely.nothing", ev => { });
            subman.Subscribe("freely.speaking", ev => { });

            lock (subman._lo)
            {
                int res = subman._findFirstSubscriber("event.key");
                if (res != 1)
                {
                    Error("Wrong result.");
                    result -= 1;
                }
            }
        }
        if (0 != result)
        {
            ErrorThrow("Unit test failed.", (m) => new InvalidOperationException(m));
        }

        {
            var subman = new SubscriptionManager();

            subman.Subscribe("input.", ev => { });
            subman.Subscribe("input.mouse.pressed", ev => { });
            subman.Subscribe("input.touch.pressed", ev => { });
            subman.Subscribe("lifecycle.resume", ev => { });
            subman.Subscribe("lifecycle.suspend", ev => { });
            subman.Subscribe("map.range", ev => { });
            subman.Subscribe("nogame.minimap.toggleMap", ev => { });
            subman.Subscribe("nogame.playerhover.collision.anonymous", ev => { });
            subman.Subscribe("nogame.playerhover.collision.car3", ev => { });
            subman.Subscribe("nogame.playerhover.collision.cube", ev => { });
            subman.Subscribe("nogame.playerhover.collision.polytope", ev => { });
            subman.Subscribe("nogame.scenes.root.Scene.kickoff", ev => { });
            subman.Subscribe("nogame.scenes.root.Scene.kickoff", ev => { });
            subman.Subscribe("view.size.changed", ev => { });

            lock (subman._lo)
            {
                int res = subman._findFirstSubscriber("input.mouse.released");
                if (res != 0)
                {
                    Error("Wrong result.");
                    result -= 1;
                }
            }
        }
        return result;
    }
}