using System.Net;
using System.Runtime.InteropServices;

namespace AisPublishHelper;

internal static class SmbDiagnostics
{
    public static void PrintHostLookup(string host)
    {
        try
        {
            var addresses = Dns.GetHostAddresses(host);
            var text = string.Join(", ", addresses.Select(a => a.ToString()));
            ConsoleUi.Info($"DNS {host} -> {text}");
        }
        catch (Exception ex)
        {
            ConsoleUi.Info($"DNS lookup failed for {host}: {ex.Message}");
        }
    }

    public static IReadOnlyList<string> ListShares(string host)
    {
        var server = host.StartsWith(@"\\", StringComparison.Ordinal) ? host : @"\\" + host;
        var resume = 0;
        var status = NativeMethods.NetShareEnum(
            server,
            1,
            out var buffer,
            -1,
            out var entriesRead,
            out _,
            ref resume);

        if (status != 0 || buffer == IntPtr.Zero)
        {
            ConsoleUi.Info($"Could not list shares on {server} ({new System.ComponentModel.Win32Exception(status).Message}).");
            ConsoleUi.Info("Ask the admin to run this on the updater and send you the left column:");
            ConsoleUi.Info("  net share");
            return [];
        }

        try
        {
            var names = new List<string>();
            var size = Marshal.SizeOf<NativeMethods.ShareInfo1>();
            for (var i = 0; i < entriesRead; i++)
            {
                var item = Marshal.PtrToStructure<NativeMethods.ShareInfo1>(buffer + (i * size));
                if (!string.IsNullOrWhiteSpace(item.NetName))
                {
                    names.Add(item.NetName);
                }
            }

            return names;
        }
        finally
        {
            NativeMethods.NetApiBufferFree(buffer);
        }
    }

    public static string Explain(int error)
    {
        return error switch
        {
            5 => "Access denied: the share exists, but this account cannot use it (C$ and ADMIN$ need Administrators).",
            53 => "Network path not found: host name/IP is wrong, or SMB (port 445) is blocked.",
            67 => "The share name does not exist on that server. NTFS write permission on a folder is not enough — there must be an SMB share (check `net share` on the server). The name may also differ (master vs master$, or a new share name).",
            86 or 1326 => "Wrong user or password.",
            1219 => "Windows already has a connection to this server with different credentials. Disconnect it (net use /delete) and retry.",
            _ => ""
        };
    }

    private static class NativeMethods
    {
        [DllImport("Netapi32.dll", CharSet = CharSet.Unicode)]
        public static extern int NetShareEnum(
            string serverName,
            int level,
            out IntPtr bufPtr,
            int prefMaxLen,
            out int entriesRead,
            out int totalEntries,
            ref int resumeHandle);

        [DllImport("Netapi32.dll")]
        public static extern int NetApiBufferFree(IntPtr buffer);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct ShareInfo1
        {
            public string NetName;
            public uint Type;
            public string Remark;
        }
    }
}
