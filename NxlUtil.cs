using Microsoft.Management.Infrastructure;
using System.Diagnostics.Contracts;
using System.Security.Cryptography;
using System.Text;

namespace QuickLaunch;

public static class NxlUtil
{
    // These will need updating from time to time.
    public const string NxlUserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) NexonLauncher/4.0.0 Chrome/91.0.4472.164 Electron/13.1.8 Safari/537.36";
    public const string NxlFeVersion = "nxl-v2.28-a2f355a5";
    public const string NxlCdnUri = "https://nxl.nxfs.nexon.com/";

    /// <summary>
    /// Generates a random session id (32 hex chars).
    /// </summary>
    [Pure]
    public static string GenSessionId()
    {
        byte[] buffer = new byte[16];
        new Random().NextBytes(buffer);
        return string.Concat(buffer.Select(x => x.ToString("x2")).ToArray());
    }

    /// <summary>
    /// Gets the NXL device id for this computer. We use the same algorithm
    /// so there is less hassle with device trust, verification, etc.
    /// </summary>
    [Pure]
    public static string GetDeviceId()
    {
        // Collect.
        var uuid = "";
        var cimSession = CimSession.Create(null);
        foreach (var instance in cimSession.EnumerateInstances(@"root\cimv2", "win32_computersystemproduct"))
        {
            uuid = (string)instance.CimInstanceProperties["uuid"].Value;
            break;
        }

        var machineId = Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography", "MachineGuid", "");

        // Compute.
        var idBytes = Encoding.ASCII.GetBytes(uuid + machineId);
        var hash = HashAlgorithm.Create("SHA256")!.ComputeHash(idBytes);
        return string.Concat(hash.Select(x => x.ToString("x2")).ToArray());
    }
}
