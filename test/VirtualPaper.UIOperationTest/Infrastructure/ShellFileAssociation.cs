using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace VirtualPaper.UIOperationTest.Infrastructure;

// Invoke the same registered handler as Explorer's Open With menu, targeting
// this package explicitly without changing the user's default application.
internal static class ShellFileAssociation {
    internal static void Invoke(string path, string appId) {
        SHAssocEnumHandlers(Path.GetExtension(path), 0, out var handlers);
        try {
            while (handlers.Next(1, out var handler, out uint fetched) == 0 && fetched == 1) {
                try {
                    handler.GetName(out string name);
                    string? id = null;
                    if (handler is IObjectWithAppUserModelId identified) identified.GetAppId(out id);
                    using var application = Registry.ClassesRoot.OpenSubKey(name + @"\Application");
                    id ??= application?.GetValue("AppUserModelID") as string;
                    if (id != appId && name != appId) continue;
                    InvokeHandler(handler, path);
                    return;
                }
                finally { Marshal.FinalReleaseComObject(handler); }
            }
            throw new InvalidOperationException("The installed package has no Shell handler for " + Path.GetExtension(path));
        }
        finally { Marshal.FinalReleaseComObject(handlers); }
    }

    private static void InvokeHandler(IAssocHandler handler, string path) {
        var itemId = new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE");
        SHCreateItemFromParsingName(path, 0, ref itemId, out nint item);
        try {
            var arrayId = new Guid("B63EA76D-1F85-456F-A19C-48159EFA858B");
            SHCreateShellItemArrayFromShellItem(item, ref arrayId, out var array);
            try {
                var handlerId = new Guid("B8C0BD9F-ED24-455C-83E6-D5390C4FE8C4");
                var dataId = new Guid("0000010E-0000-0000-C000-000000000046");
                array.BindToHandler(0, ref handlerId, ref dataId, out nint data);
                try { handler.Invoke(data); }
                finally { Marshal.Release(data); }
            }
            finally { Marshal.FinalReleaseComObject(array); }
        }
        finally { Marshal.Release(item); }
    }

    [ComImport, Guid("973810AE-9599-4B88-9E4D-6EE98C9552DA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IEnumAssocHandlers {
        [PreserveSig] int Next(uint count, out IAssocHandler handler, out uint fetched);
    }

    [ComImport, Guid("F04061AC-1659-4A3F-A954-775AA57FC083"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAssocHandler {
        void GetName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        void GetUIName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        void GetIconLocation([MarshalAs(UnmanagedType.LPWStr)] out string path, out int index);
        [PreserveSig] int IsRecommended();
        void MakeDefault([MarshalAs(UnmanagedType.LPWStr)] string description);
        void Invoke(nint data);
    }

    [ComImport, Guid("36DB0196-9665-46D1-9BA7-D3709EECF9ED"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IObjectWithAppUserModelId {
        void SetAppId([MarshalAs(UnmanagedType.LPWStr)] string id);
        void GetAppId([MarshalAs(UnmanagedType.LPWStr)] out string? id);
    }

    [ComImport, Guid("B63EA76D-1F85-456F-A19C-48159EFA858B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemArray {
        void BindToHandler(nint context, ref Guid handlerId, ref Guid interfaceId, out nint result);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHAssocEnumHandlers(string extension, uint filter, out IEnumAssocHandlers handlers);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(string path, nint context, ref Guid interfaceId, out nint item);
    [DllImport("shell32.dll", PreserveSig = false)]
    private static extern void SHCreateShellItemArrayFromShellItem(nint item, ref Guid interfaceId, out IShellItemArray array);
}
