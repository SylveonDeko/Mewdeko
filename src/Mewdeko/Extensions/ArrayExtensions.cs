namespace Mewdeko.Extensions;

// made for customreactions because they almost never get added
// and they get looped through constantly
public static class ArrayExtensions
{
    /// <summary>
    ///     Create a new array from the old array + new element at the end
    /// </summary>
    /// <param name="input">Input array</param>
    /// <param name="added">Item to add to the end of the output array</param>
    /// <typeparam name="T">Type of the array</typeparam>
    /// <returns>A new array with the new element at the end</returns>
    public static T[] With<T>(this T[] input, T added)
    {
        var newCrs = new T[input.Length + 1];
        Array.Copy(input, 0, newCrs, 0, input.Length);
        newCrs[input.Length] = added;
        return newCrs;
    }
}