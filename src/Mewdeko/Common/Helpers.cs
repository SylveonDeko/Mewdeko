namespace Mewdeko.Common;

/// <summary>
///     Provides helper methods for common tasks.
/// </summary>
public static class Helpers
{
    /// <summary>
    ///     Reads an error message from the console and exits the application with the specified exit code.
    /// </summary>
    /// <param name="exitCode">The exit code to be returned when exiting the application.</param>
    public static void ReadErrorAndExit(int exitCode)
    {
        if (!Console.IsInputRedirected)
            Console.ReadKey();

        Environment.Exit(exitCode);
    }
}