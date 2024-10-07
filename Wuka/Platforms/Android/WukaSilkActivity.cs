// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Android.App;
using Android.Content.PM;
using Android.Provider;
using Org.Libsdl.App;
using Silk.NET.Core.Loader;
using Silk.NET.Core.Native;
using Silk.NET.SDL;

namespace Wuka
{
    public abstract class WukaSilkActivity : SDLActivity
    {
        public const ConfigChanges ConfigChangesFlags =
            ConfigChanges.Orientation | ConfigChanges.KeyboardHidden | ConfigChanges.ScreenSize;
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void MainFunc();

        internal static WukaSilkActivity Instance { get; private set; }
        internal static MainFunc CurrentMain { get; private set; }
        internal static nint NativeCurrentMain { get; private set; }

        [DllImport("libmain.so", EntryPoint = "sdSetMain")]
        internal static extern void SetupMain(nint funcPtr);

        static WukaSilkActivity()
        {
            SearchPathContainer.Platform = UnderlyingPlatform.Android;
            CurrentMain = Main;
        }

        internal static unsafe void Main()
        {
            if (Instance is null)
            {
                throw new InvalidOperationException("No SilkActivity present.");
            }

            // SdlProvider.SetMainReady = true;
            Instance.Run();
            Instance = null;
        }

        public override unsafe void LoadLibraries()
        {
            base.LoadLibraries();
            if (ReferenceEquals(Instance, this))
            {
                return;
            }

            if (Instance is not null)
            {
                throw new InvalidOperationException
                    ("Only one SilkActivity may be present throughout the whole application.");
            }

            Instance = this;
            if (NativeCurrentMain is 0)
            {
                SetupMain(NativeCurrentMain = SilkMarshal.DelegateToPtr(CurrentMain));
            }
        }

        public override void SetOrientationBis(int w, int h, bool resizable, string hint)
        {
            // do nothing, Silk.NET respects the OS and doesn't want to do any meddling the consumer can't control.
        }

        protected abstract void OnRun();

        protected override void OnDestroy()
        {
            Instance = null;
            base.OnDestroy();
        }

        private void Run()
        {
            OnRun();
        }
    }
}