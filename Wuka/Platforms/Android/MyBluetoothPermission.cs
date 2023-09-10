using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wuka.Platforms.Android;

internal class MyBluetoothPermission : Permissions.BasePlatformPermission
{
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
        new List<(string androidPermission, bool isRuntime)>
        {
        (global::Android.Manifest.Permission.BluetoothConnect, true),
        //(global::Android.Manifest.Permission.WriteExternalStorage, true)
        }.ToArray();
}
