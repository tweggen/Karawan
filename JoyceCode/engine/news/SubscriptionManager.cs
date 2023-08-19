using System;
using System.Collections.Generic;
using static engine.Logger;

namespace engine.news;

class SubscriberEntry : IComparable<SubscriberEntry>
{
    public string SubscriptionPath;
    public Action<Event> Handler;
    public int SerialNumber;
    
    public int CompareTo(SubscriberEntry other)
    {
        int result = SubscriptionPath.CompareTo(other.SubscriptionPath);
        if (result != 0)
        {
            return result;
        }
        else
        {
            return SerialNumber - other.SerialNumber;
        }
    }
}

public class SubscriptionManager
{
    private object _lo = new();
    private int _nextSerial = 1000;

    private SortedList<SubscriberEntry, SubscriberEntry> _subscriberList = new();


    /*
     * Return the index of the first entry greater or equal than path.
     */
    private int _findFirstPathGE_nolock(string path)
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
            lastResult = _subscriberList.GetKeyAtIndex(i0).SubscriptionPath.CompareTo(path);
        }
        else
        {
            while (i0 != i1)
            {
                int il = i1 - i0 + 1;

                im = i0 + il / 2;
                lastResult = _subscriberList.GetKeyAtIndex(im).SubscriptionPath.CompareTo(path);

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
                        i1 = im;
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
            if (im == l - 1)
            {
                return -1;
            }
            else
            {
                return im + 1;
            }
        }
        else
        {
            return im;
        }
    }
    
    
    public void Subscribe(string path, Action<Event> handler)
    {
        SubscriberEntry se = new()
        {
            SubscriptionPath = path,
            Handler = handler,
            SerialNumber = 0
        };
        lock (_lo)
        {
            se.SerialNumber = _nextSerial++;
            _subscriberList.Add(se, se);
        }
    }


    public void Unsubscribe(string path, Action<Event> handler)
    {
        SubscriberEntry seRef = new()
        {
            SubscriptionPath = path,
            Handler = handler,
            SerialNumber = 0
        };
        lock (_lo)
        {
            int idx = _subscriberList.IndexOfKey(seRef);
            if (-1 == idx)
            {
                Wonder($"Unable to find subscription path for \"{path}\".");
                return;
            }
            
        }
    }


    public void Handle(Event ev)
    {
        List<Action<Event>> listActions;
        lock (_lo)
        {
            int idx = _findFirstPathGE_nolock(ev.Type);
            if (idx < 0)
            {
                return;
            }

            listActions = new();
            while (true)
            {
                var sub = _subscriberList.GetKeyAtIndex(idx);
                if (!ev.Type.StartsWith(sub.SubscriptionPath))
                {
                    break;
                }
                listActions.Add(sub.Handler);
            }
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
        var subman = new SubscriptionManager();

        subman.Subscribe("event.key.up", ev => {});
        subman.Subscribe("event.key.down", ev => {});
        subman.Subscribe("event", ev => {});
        subman.Subscribe("event.collision", ev => {});

        lock (subman._lo)
        {
            int res = subman._findFirstPathGE_nolock("event.key");
            if (res != 2)
            {
                Error("Wrong result.");
                result -= 1;
            }
        }

        if (0 != result)
        {
            ErrorThrow("Unit test failed.", (m) => new InvalidOperationException(m));
        }

        return result;
    }
}