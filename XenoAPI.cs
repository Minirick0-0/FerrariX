using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace FerrariX;

internal static class XenoAPI
{
    // Struct layout exacto del código fuente de Xeno
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct ClientInfo
    {
        public string version;
        public string name;
        public int id;
    }

    [DllImport("Xeno.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "Initialize")]
    private static extern void _Initialize();

    [DllImport("Xeno.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "GetClients")]
    private static extern IntPtr _GetClients();

    [DllImport("Xeno.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "Execute")]
    private static extern void _Execute(byte[] script, string[] clients, int clientCount);

    [DllImport("Xeno.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "Compilable")]
    private static extern bool _Compilable(byte[] script);

    public static bool DllFound => File.Exists("Xeno.dll");

    public static bool RobloxRunning =>
        Process.GetProcessesByName("RobloxPlayerBeta").Length > 0 ||
        Process.GetProcessesByName("Windows10Universal").Length > 0;

    public static void Init() => _Initialize();

    public static List<ClientInfo> GetClientList()
    {
        var result = new List<ClientInfo>();
        try
        {
            IntPtr ptr = _GetClients();
            if (ptr == IntPtr.Zero) return result;

            // Array terminado en null — iteramos hasta que name == null
            int structSize = Marshal.SizeOf<ClientInfo>();
            for (int i = 0; i < 64; i++) // límite de seguridad
            {
                var client = Marshal.PtrToStructure<ClientInfo>(ptr);
                if (client.name == null) break;
                result.Add(client);
                ptr += structSize;
            }
        }
        catch { /* Xeno.dll no cargado o Roblox no attachado */ }
        return result;
    }

    public static void ExecuteScript(string script, string[] clientNames)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(script);
        _Execute(bytes, clientNames, clientNames.Length);
    }

    public static bool IsCompilable(string script)
    {
        try
        {
            return _Compilable(Encoding.UTF8.GetBytes(script));
        }
        catch { return false; }
    }
}
