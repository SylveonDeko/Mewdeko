using System;
using System.Collections.Generic;
using System.Linq;

namespace Mewdeko.Core.Common
{
    public readonly struct kwum
    {
        private readonly char[] _data;
        private const string ValidCharacters = "23456789abcdefghijkmnpqrstuvwxyz";
        private static readonly HashSet<char> validCharacterSet = ValidCharacters.ToHashSet();

        public kwum(in char c)
        {
            if (!IsValidChar(c))
                throw new ArgumentException("Character needs to be a valid kwum character.", nameof(c));
            _data = new[] { c };
        }

        public kwum(in ReadOnlySpan<char> input)
        {
            foreach (var c in input)
                if (!IsValidChar(c))
                    throw new ArgumentException("All characters need to be a valid kwum characters.", nameof(c));

            _data = input.ToArray();
        }

        /// <summary>
        ///     Unsafe, used only internally
        /// </summary>
        /// <param name="data">Array of characters</param>
        private kwum(char[] data)
        {
            _data = data;
        }


        public static bool TryParse(in ReadOnlySpan<char> input, out kwum value)
        {
            value = default;
            foreach (var c in input)
                if (!IsValidChar(c))
                    return false;

            value = new kwum(input.ToArray());
            return true;
        }

        public static kwum operator +(kwum left, kwum right)
        {
            throw new NotImplementedException();
        }

        public static bool operator ==(kwum left, kwum right)
        {
            if (left._data is null)
                return right._data is null;

            if (right._data is null)
                return false;

            if (left._data.Length != right._data.Length)
                return false;

            for (var i = 0; i < left._data.Length; i++)
                if (left._data[i] != right._data[i])
                    return false;

            return true;
        }

        public static bool operator !=(kwum left, kwum right)
        {
            return !(left == right);
        }

        public static bool IsValidChar(char c)
        {
            return validCharacterSet.Contains(c);
        }

        public override string ToString()
        {
            return new string(_data);
        }

        public override bool Equals(object obj)
        {
            return obj is kwum kw && kw == this;
        }

        public override int GetHashCode()
        {
            HashCode hashCode = default;
            if (_data is null)
                return 0;

            for (var i = _data.Length >= 8 ? _data.Length - 8 : 0; i < _data.Length; i++)
                hashCode.Add(_data[i].GetHashCode());

            return hashCode.ToHashCode();
        }
    }
}