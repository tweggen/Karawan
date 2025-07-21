using System.Collections.Generic;
using Permissions = Microsoft.Maui.ApplicationModel.Permissions;

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
