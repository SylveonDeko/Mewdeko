using System.Diagnostics;
using System.IO;
using System.Reflection;
using Serilog;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Provides functionality to check the version of a DLL file in the application's directory.
/// </summary>
public class DllVersionChecker
{
    /// <summary>
    ///     Gets the version of a specified DLL file.
    /// </summary>
    /// <param name="dllName">The name of the DLL file. If null, checks the version of 'Discord.Net.WebSocket.dll'.</param>
    /// <returns>The version of the DLL file as a string, or null if the version cannot be found.</returns>
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
}