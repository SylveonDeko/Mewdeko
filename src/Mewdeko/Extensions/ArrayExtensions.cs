namespace Mewdeko.Extensions;

/// <summary>
///     Provides extension methods for arrays.
/// </summary>
public static class ArrayExtensions
{
    /// <summary>
    ///     Creates a new array from the old array with a new element added at the end.
    /// </summary>
    /// <typeparam name="T">The type of the array elements.</typeparam>
    /// <param name="input">The input array.</param>
    /// <param name="added">The item to add to the end of the output array.</param>
    /// <returns>A new array with the new element added at the end.</returns>
    public static T[] With<T>(this T[] input, T added)
    {
        var newCrs = new T[input.Length + 1];
        Array.Copy(input, 0, newCrs, 0, input.Length);
        newCrs[input.Length] = added;
        return newCrs;
    }
}