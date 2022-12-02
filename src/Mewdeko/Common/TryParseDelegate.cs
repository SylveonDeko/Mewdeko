namespace Mewdeko.Common;

public delegate bool TryParseDelegate<T>(string input, out T result);

public delegate bool EnumTryParseDelegate<T>(string input, bool ignoreCase, out T result);