using System.Numerics;
using Android.Content;
using Android.Runtime;
using Android.Views;
using engine;
using engine.news;
using Org.Libsdl.App;
using View = Android.Views.View;

namespace Wuka;

public class GameSurface : SDLSurface
{
    private EventQueue _eq = null;
    
    public unsafe override bool OnTouch(View? v, MotionEvent? e)
    {
        if (null == e)
        {
            return false;
        }

        if (null == _eq)
        {
            _eq = I.Get<EventQueue>();
        }
        
        int touchDevId = e.DeviceId;
        
        /*
         * The number of pointers in this event. 
         */
        int pointerCount = e.PointerCount;
        
        /*
         * The event action that happended. 
         */
        var action = e.ActionMasked;
        
        /*
         * The finger id of the current pointer index         
         */
        int pointerFingerId;
        
        int i = -1;
        float p;
        Vector2 v2Physical;
        
        if (touchDevId < 0) {
            touchDevId -= 1;
        }
            
        switch (action)
        {
            case MotionEventActions.Down:
                i = 0;
                goto case MotionEventActions.PointerDown;
            case MotionEventActions.PointerDown:
                if (-1 == i)
                {
                    i = e.ActionIndex;
                }
                pointerFingerId = e.GetPointerId(i);
                v2Physical = new(e.GetX(i) / Width, e.GetY(i) / Height);
                p = e.GetPressure(i);
                if (p > 1.0f) {
                    // may be larger than 1.0f on some devices
                    // see the documentation of getPressure(i)
                    p = 1.0f;
                }
                
                _eq.Push(new engine.news.Event(engine.news.Event.INPUT_FINGER_PRESSED, "")
                {
                    PhysicalPosition = v2Physical,
                    PhysicalSize = Vector2.One,
                    LogicalPosition = v2Physical,
                    Data1 = (uint) touchDevId,
                    Data2 = (uint) pointerFingerId
                });
                
                break;
            
            case MotionEventActions.Up:
                i = 0;
                goto case MotionEventActions.PointerUp;
            case MotionEventActions.PointerUp:
                if (-1 == i)
                {
                    i = e.ActionIndex;
                }
                
                pointerFingerId = e.GetPointerId(i);
                v2Physical = new(e.GetX(i) / Width, e.GetY(i) / Height);
                p = e.GetPressure(i);
                if (p > 1.0f) {
                    // may be larger than 1.0f on some devices
                    // see the documentation of getPressure(i)
                    p = 1.0f;
                }
                
                _eq.Push(new engine.news.Event(engine.news.Event.INPUT_FINGER_RELEASED, "")
                {
                    PhysicalPosition = v2Physical,
                    PhysicalSize = Vector2.One,
                    LogicalPosition = v2Physical,
                    Data1 = (uint) touchDevId,
                    Data2 = (uint) pointerFingerId
                });
                
                break;
            
            case MotionEventActions.Cancel:
                for (i = 0; i < pointerCount; i++) {
                    pointerFingerId = e.GetPointerId(i);
                    v2Physical = new(e.GetX(i) / Width, e.GetY(i) / Height);
                    p = e.GetPressure(i);
                    if (p > 1.0f) {
                        // may be larger than 1.0f on some devices
                        // see the documentation of getPressure(i)
                        p = 1.0f;
                    }
                    
                    _eq.Push(new engine.news.Event(engine.news.Event.INPUT_FINGER_RELEASED, "")
                    {
                        PhysicalPosition = v2Physical,
                        PhysicalSize = Vector2.One,
                        LogicalPosition = v2Physical,
                        Data1 = (uint) touchDevId,
                        Data2 = (uint) pointerFingerId
                    });
                }
                break;
            
            case MotionEventActions.Move:
                for (i = 0; i < pointerCount; i++) {
                    pointerFingerId = e.GetPointerId(i);
                    v2Physical = new(e.GetX(i) / Width, e.GetY(i) / Height);
                    p = e.GetPressure(i);
                    if (p > 1.0f) {
                        // may be larger than 1.0f on some devices
                        // see the documentation of getPressure(i)
                        p = 1.0f;
                    }
                    
                    _eq.Push(new engine.news.Event(engine.news.Event.INPUT_FINGER_MOVED, "")
                    {
                        PhysicalPosition = v2Physical,
                        PhysicalSize = Vector2.One,
                        LogicalPosition = v2Physical,
                        Data1 = (uint) touchDevId,
                        Data2 = (uint) pointerFingerId
                    });
                    
                }

                break;
            default:
                break;
        }
        return true;
    }


    protected GameSurface(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
    {
    }

    
    public GameSurface(Context context) : base(context)
    {
    }
}