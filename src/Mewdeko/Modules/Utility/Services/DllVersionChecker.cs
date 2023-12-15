using System.Diagnostics;
using System.IO;
using System.Reflection;
using Serilog;

namespace Mewdeko.Modules.Utility.Services;

public class DllVersionChecker
{
    public static string? GetDllVersion(string? dllName = null)
    {
        try
        {
            var dllPath = Convert.ToString(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            var testPath = $"{dllPath}/Discord.Net.WebSocket.dll";
            if (dllName != null) testPath = $"{dllPath}/{dllName}";

            var myFileVersionInfo = FileVersionInfo.GetVersionInfo(testPath);
            return
                $"{myFileVersionInfo.FileMajorPart}.{myFileVersionInfo.FileMinorPart}.{myFileVersionInfo.FileBuildPart}";
        }
        catch
        {
            Log.Error("Unable to find version number of requested dll");
            return null;
        }
    }
    public static Dictionary<string, string?> GetDllVersions(List<string> dllNames)
    {
        var versions = new Dictionary<string, string?>();
        var dllPath = Convert.ToString(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

        foreach (var dllName in dllNames)
        {
            try
            {
                var testPath = $"{dllPath}/{dllName}";
                var myFileVersionInfo = FileVersionInfo.GetVersionInfo(testPath);
                var version =
                    $"{myFileVersionInfo.FileMajorPart}.{myFileVersionInfo.FileMinorPart}.{myFileVersionInfo.FileBuildPart}";
                versions.Add(dllName, version);
            }
            catch (Exception ex)
            {
                Log.Error($"Unable to find version number for {dllName}: {ex.Message}");
                versions.Add(dllName, null);
            }
        }

        return versions;
    }
}