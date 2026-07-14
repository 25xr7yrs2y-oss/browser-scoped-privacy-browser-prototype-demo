using System.Net;
using System.Runtime.InteropServices;

namespace PrivacyBrowser.App;

public sealed record TcpListenerOwner(IPAddress Address, int Port, int ProcessId);

internal static class TcpListenerInspector
{
    private const int AfInet = 2;
    private const int InsufficientBuffer = 122;

    public static IReadOnlyList<TcpListenerOwner> GetListeners(int port)
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        var size = 0;
        var result = GetExtendedTcpTable(IntPtr.Zero, ref size, true, AfInet,
            TcpTableClass.TcpTableOwnerPidListener, 0);
        if (result != InsufficientBuffer)
        {
            throw new InvalidOperationException($"Unable to inspect TCP listeners (Win32 error {result}).");
        }

        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            result = GetExtendedTcpTable(buffer, ref size, true, AfInet,
                TcpTableClass.TcpTableOwnerPidListener, 0);
            if (result != 0)
            {
                throw new InvalidOperationException($"Unable to inspect TCP listeners (Win32 error {result}).");
            }

            var count = Marshal.ReadInt32(buffer);
            var rowPointer = IntPtr.Add(buffer, sizeof(int));
            var rowSize = Marshal.SizeOf<MibTcpRowOwnerPid>();
            var listeners = new List<TcpListenerOwner>();
            for (var i = 0; i < count; i++)
            {
                var row = Marshal.PtrToStructure<MibTcpRowOwnerPid>(rowPointer);
                var localPort = (ushort)IPAddress.NetworkToHostOrder((short)row.LocalPort);
                if (localPort == port)
                {
                    listeners.Add(new TcpListenerOwner(new IPAddress(row.LocalAddress), localPort, (int)row.OwningPid));
                }
                rowPointer = IntPtr.Add(rowPointer, rowSize);
            }
            return listeners;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr tcpTable,
        ref int size,
        bool order,
        int ipVersion,
        TcpTableClass tableClass,
        uint reserved);

    private enum TcpTableClass
    {
        TcpTableOwnerPidListener = 3,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcpRowOwnerPid
    {
        public uint State;
        public uint LocalAddress;
        public uint LocalPort;
        public uint RemoteAddress;
        public uint RemotePort;
        public uint OwningPid;
    }
}
