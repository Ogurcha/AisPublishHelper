using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AisPublishHelper;

internal sealed class WindowsShareSession : IDisposable
{
    private const int ResourceTypeAny = 0;
    private const int ResourceTypeDisk = 1;
    private const int ErrorSuccess = 0;
    private const int ErrorAlreadyAssigned = 85;
    private const int ErrorSessionCredentialConflict = 1219;

    private readonly string _remoteName;
    private readonly int _resourceType;
    private bool _connected;

    public static WindowsShareSession ForIpc(string host)
    {
        return new WindowsShareSession($@"\\{host}\IPC$", ResourceTypeAny);
    }

    public WindowsShareSession(string remoteName, int resourceType = ResourceTypeDisk)
    {
        _remoteName = remoteName;
        _resourceType = resourceType;
    }

    public void Connect(string userName, string password)
    {
        var result = TryConnect(userName, password);
        if (result is not ErrorSuccess and not ErrorAlreadyAssigned)
        {
            var hint = SmbDiagnostics.Explain(result);
            var extra = string.IsNullOrEmpty(hint) ? "" : " " + hint;
            throw new InvalidOperationException(
                $"Could not connect to '{_remoteName}' as '{userName}'. {FormatWin32(result)}.{extra}");
        }
    }

    public int TryConnect(string userName, string password)
    {
        var result = AddConnection(userName, password);
        if (result == ErrorSessionCredentialConflict)
        {
            NativeMethods.WNetCancelConnection2(_remoteName, 0, true);
            result = AddConnection(userName, password);
        }

        if (result is ErrorSuccess or ErrorAlreadyAssigned)
        {
            _connected = true;
        }

        return result;
    }

    public void Dispose()
    {
        if (_connected)
        {
            NativeMethods.WNetCancelConnection2(_remoteName, 0, true);
            _connected = false;
        }
    }

    private int AddConnection(string userName, string password)
    {
        var resource = new NativeMethods.NetResource
        {
            dwType = _resourceType,
            lpRemoteName = _remoteName
        };

        return NativeMethods.WNetAddConnection2(ref resource, password, userName, 0);
    }

    private static string FormatWin32(int error)
    {
        var message = new Win32Exception(error).Message;
        return $"Win32 error {error}: {message}";
    }

    private static class NativeMethods
    {
        [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
        public static extern int WNetAddConnection2(ref NetResource netResource, string? password, string? username, int flags);

        [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
        public static extern int WNetCancelConnection2(string name, int flags, bool force);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct NetResource
        {
            public int dwScope;
            public int dwType;
            public int dwDisplayType;
            public int dwUsage;
            public string? lpLocalName;
            public string? lpRemoteName;
            public string? lpComment;
            public string? lpProvider;
        }
    }
}
