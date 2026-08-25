using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Aeziol.App.Services;

internal static partial class ProcessMemoryTrimmer
{
    public static bool TryTrimWorkingSet()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return EmptyWorkingSet(process.Handle);
        }
        catch (Exception)
        {
            return false;
        }
    }

    [LibraryImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EmptyWorkingSet(nint processHandle);
}
