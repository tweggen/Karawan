using System;
using System.Numerics;
using Android.Content;
using Android.Runtime;
using Android.Text;
using Android.Views;
using Android.Views.InputMethods;
using engine;
using engine.news;
using Org.Libsdl.App;
using View = Android.Views.View;
using static engine.Logger;

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
        
        /*
         * Every branch below divides by these to normalise the touch into 0..1, so read
         * them once and refuse the event if they are not usable yet.
         *
         * View.Width/Height are 0 until the surface has been laid out, and a MotionEvent
         * can arrive in that window - also after the surface is destroyed and recreated
         * on resume. Dividing by 0 yields Infinity, or NaN for the 0/0 at the very origin.
         *
         * That value does NOT stay local. RightStickFingerState accumulates finger deltas
         * into InputController.V2RightTouchMove, and FollowCameraController accumulates
         * those AGAIN into its own persistent _v2MouseOffseting. Neither ever clears on a
         * non-finite value, so a single bad event poisons the camera orientation for the
         * rest of the process: the view spins wildly, then renders black, while OpenAL
         * logs "Listener orientation out of range" every frame forever.
         *
         * SDL guards the identical division in its own SDLSurface.java
         * (getNormalizedX: "if (mWidth <= 1) return 0.5f"). Dropping the event is the
         * honest choice here - a touch we cannot locate carries no information.
         */
        int width = Width;
        int height = Height;
        if (width <= 0 || height <= 0)
        {
            Warning($"Dropping touch event: surface not laid out yet ({width}x{height}).");
            return true;
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
                v2Physical = new(e.GetX(i) / width, e.GetY(i) / height);
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
                v2Physical = new(e.GetX(i) / width, e.GetY(i) / height);
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
                    v2Physical = new(e.GetX(i) / width, e.GetY(i) / height);
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
                    v2Physical = new(e.GetX(i) / width, e.GetY(i) / height);
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


    public override IInputConnection OnCreateInputConnection(EditorInfo outAttrs)
    {
        /*
         * Note we deliberately do NOT call base.OnCreateInputConnection(): SDLSurface
         * is a plain SurfaceView and does not implement it, so View.onCreateInputConnection()
         * returns null by contract. Any connection we hand out has to stand on its own —
         * KarawanInputConnection derives from BaseInputConnection for exactly that reason.
         *
         * This method is called by the IME framework whenever it binds to the focused
         * view, which on Android 14+ already happens on the first window focus gain,
         * long before the engine exists. It must therefore be safe to call at any time.
         */

        // Prevent fullscreen/extract mode keyboard in landscape
        outAttrs.ImeOptions |= ImeFlags.NoFullscreen;

        /*
         * Select the input type based on the current keyboard input type.
         * On "email"/"password"/"number", composition is disabled by the input type
         * flags so each character commits immediately. On "text", the IME may compose;
         * KarawanInputConnection turns that into INPUT_TEXT_REPLACE events.
         */
        string inputType = "text";
        try
        {
            inputType = I.TryGet<engine.Engine>()?.KeyboardInputType ?? "text";
        }
        catch (Exception)
        {
            // Engine not ready yet, use default.
        }

        switch (inputType)
        {
            case "email":
                outAttrs.InputType = InputTypes.ClassText | InputTypes.TextVariationEmailAddress;
                break;

            case "password":
                outAttrs.InputType = InputTypes.ClassText | InputTypes.TextVariationVisiblePassword;
                break;

            case "number":
                outAttrs.InputType = InputTypes.ClassNumber;
                break;

            default:
                outAttrs.InputType = InputTypes.ClassText | InputTypes.TextFlagNoSuggestions;
                break;
        }

        return new KarawanInputConnection(this);
    }


    protected GameSurface(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
    {
    }


    public GameSurface(Context context) : base(context)
    {
    }
}