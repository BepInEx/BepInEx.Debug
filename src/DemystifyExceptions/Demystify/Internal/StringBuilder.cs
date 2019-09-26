// Original author: Nicolas Gadenne (contact@gaddygames.com) 
// https://github.com/snozbot/StringBuilder

// MIT License
// 
// Copyright (c) 2017 snozbot
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DemystifyExceptions.Demystify.Internal
{
    public sealed class StringBuilder : IList<char>
    {
        private char[] _buffer;
        private string _cachedString = "";
        private List<char> _temp;

        public StringBuilder(int capacity = 64)
        {
            _buffer = new char[capacity];
        }

        public int Length { get; private set; }

        public int Capacity => _buffer.Length;

        [IndexerName("Chars")]
        public char this[int index]
        {
            get => _buffer[index];
            set
            {
                _buffer[index] = value;
                _cachedString = null;
            }
        }

        public int IndexOf(char value)
        {
            return IndexOf(value, 0);
        }

        int ICollection<char>.Count => Length;

        bool ICollection<char>.IsReadOnly => false;

        void IList<char>.RemoveAt(int index)
        {
            Remove(index, 1);
        }

        void IList<char>.Insert(int index, char item)
        {
            Insert(index, item);
        }

        void ICollection<char>.Clear()
        {
            Clear();
        }

        void ICollection<char>.Add(char character)
        {
            Append(character);
        }

        bool ICollection<char>.Contains(char item)
        {
            return IndexOf(item) != -1;
        }

        public void CopyTo(char[] array, int arrayIndex)
        {
            Array.Copy(
                _buffer,
                0,
                array,
                arrayIndex,
                Length);
        }

        bool ICollection<char>.Remove(char item)
        {
            var index = IndexOf(item);
            if (index == -1)
                return false;
            Remove(index, 1);
            return true;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return (this as IEnumerable<char>).GetEnumerator();
        }

        IEnumerator<char> IEnumerable<char>.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool IsEmpty()
        {
            return Length == 0;
        }

        /// <summary> Return the string result </summary>
        public override string ToString()
        {
            if (_cachedString == null)
                _cachedString = new string(_buffer, 0, Length);

            return _cachedString;
        }

        /// <summary> Clears the StringBuilder instance (preserving allocated capacity) </summary>
        public StringBuilder Clear()
        {
            Length = 0;
            _cachedString = null;
            return this;
        }

        /// <summary> Insert string at given index </summary>
        public StringBuilder Insert(int index, string text)
        {
            AddLength(text.Length);
            _temp = _temp ?? new List<char>(Capacity);
            _temp.Clear();
            _temp.AddRange(_buffer);
            _temp.RemoveRange(Length, _temp.Count - Length);
            for (var i = 0; i < text.Length; ++i)
                _temp.Insert(index + i, text[i]);
            _temp.CopyTo(_buffer);
            Length += text.Length;
            _cachedString = null;

            return this;
        }

        /// <summary> Insert character at given index </summary>
        public StringBuilder Insert(int index, char character)
        {
            AddLength(1);
            _temp = _temp ?? new List<char>(Capacity);
            _temp.Clear();
            _temp.AddRange(_buffer);
            _temp.RemoveRange(Length, _temp.Count - Length);
            _temp.Insert(index, character);
            _temp.CopyTo(_buffer);
            Length += 1;
            _cachedString = null;

            return this;
        }

        /// <summary> Remove characters starting from specified index </summary>
        public StringBuilder Remove(int index, int count)
        {
            _temp = _temp ?? new List<char>(Capacity);
            _temp.Clear();
            _temp.AddRange(_buffer);
            _temp.RemoveRange(Length, _temp.Count - Length);
            _temp.RemoveRange(index, count);
            _temp.CopyTo(_buffer);
            Length -= count;
            _cachedString = null;

            return this;
        }

        /// <summary> Append the content of another StringBuilder instance </summary>
        public StringBuilder Append(StringBuilder other)
        {
            if (other.Length == 0)
                return this;

            AddLength(other._buffer.Length);
            other._buffer.CopyTo(_buffer, Length);
            _cachedString = null;
            Length += other.Length;

            return this;
        }

        /// <summary> Append a string </summary>
        public StringBuilder Append(string value)
        {
            if (string.IsNullOrEmpty(value))
                return this;

            var n = value.Length;
            AddLength(n);
            for (var i = 0; i < n; i++)
                _buffer[Length + i] = value[i];
            Length += n;
            _cachedString = null;

            return this;
        }

        /// <summary> Append a substring of a string </summary>
        public StringBuilder Append(string value, int valueStartIndex = 0, int? valueLength = default)
        {
            if (string.IsNullOrEmpty(value))
                return this;

            if (valueLength == 0)
                return this;

            var n = Math.Min(value.Length, valueLength ?? value.Length - valueStartIndex);

            if (valueStartIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(valueStartIndex));

            if (valueLength < 0)
                throw new ArgumentOutOfRangeException(nameof(valueLength));

            AddLength(n);
            for (var i = 0; i < n; ++i)
                _buffer[Length + i] = value[valueStartIndex + i];
            Length += n;
            _cachedString = null;

            return this;
        }

        /// <summary> Append a character </summary>
        public StringBuilder Append(char c)
        {
            AddLength(1);
            _buffer[Length] = c;
            Length += 1;
            _cachedString = null;
            return this;
        }

        /// <summary> Append a character </summary>
        public StringBuilder Append(char c, int repeat)
        {
            AddLength(repeat);
            for (var i = 0; i < repeat; i++)
                _buffer[Length + i] = c;
            Length += repeat;
            _cachedString = null;
            return this;
        }

        /// <summary> Append an object (calls .ToString()) </summary>
        public StringBuilder Append(object value)
        {
            Append(value.ToString());
            return this;
        }

        /// <summary> Append an int without memory allocation </summary>
        public StringBuilder Append(int value)
        {
            // Allocate enough memory to handle any int number
            AddLength(16);

            // Handle the negative case
            if (value < 0)
            {
                value = -value;
                _buffer[Length++] = '-';
            }

            // Copy the digits in reverse order
            var nbChars = 0;
            do
            {
                _buffer[Length++] = (char) ('0' + value % 10);
                value /= 10;
                nbChars++;
            } while (value != 0);

            // Reverse the result
            for (var i = nbChars / 2 - 1; i >= 0; i--)
            {
                var c = _buffer[Length - i - 1];
                _buffer[Length - i - 1] = _buffer[Length - nbChars + i];
                _buffer[Length - nbChars + i] = c;
            }

            _cachedString = null;
            return this;
        }

        /// <summary> Append a float without memory allocation. </summary>
        public StringBuilder Append(float valueF)
        {
            double value = valueF;
            _cachedString = null;
            AddLength(32); // Check we have enough buffer allocated to handle any float number

            // Handle the 0 case
            if (value == 0)
            {
                _buffer[Length++] = '0';
                return this;
            }

            // Handle the negative case
            if (value < 0)
            {
                value = -value;
                _buffer[Length++] = '-';
            }

            // Get the 7 meaningful digits as a long
            var nbDecimals = 0;
            while (value < 1000000)
            {
                value *= 10;
                nbDecimals++;
            }

            var valueLong = (long) Math.Round(value);

            // Parse the number in reverse order
            var nbChars = 0;
            var isLeadingZero = true;
            while (valueLong != 0 || nbDecimals >= 0)
            {
                // We stop removing leading 0 when non-0 or decimal digit
                if (valueLong % 10 != 0 || nbDecimals <= 0)
                    isLeadingZero = false;

                // Write the last digit (unless a leading zero)
                if (!isLeadingZero)
                    _buffer[Length + nbChars++] = (char) ('0' + valueLong % 10);

                // Add the decimal point
                if (--nbDecimals == 0 && !isLeadingZero)
                    _buffer[Length + nbChars++] = '.';

                valueLong /= 10;
            }

            Length += nbChars;

            // Reverse the result
            for (var i = nbChars / 2 - 1; i >= 0; i--)
            {
                var c = _buffer[Length - i - 1];
                _buffer[Length - i - 1] = _buffer[Length - nbChars + i];
                _buffer[Length - nbChars + i] = c;
            }

            return this;
        }

        /// <summary> Replace all occurences of a character </summary>
        public StringBuilder Replace(char a, char b)
        {
            for (var i = 0; i < Length; ++i)
                if (_buffer[i] == a)
                    _buffer[i] = b;

            _cachedString = null;
            return this;
        }

        /// <summary> Replace all occurences of a string by another one </summary>
        public StringBuilder Replace(string oldStr, string newStr)
        {
            if (newStr == null)
                throw new ArgumentNullException(nameof(oldStr));

            if (newStr == null)
                throw new ArgumentNullException(nameof(newStr));

            if (Length == 0)
                return this;

            _temp = _temp ?? new List<char>(Capacity);
            _temp.Clear();

            // Create the new string into _temp
            for (var i = 0; i < Length; i++)
            {
                var isToReplace = false;
                if (_buffer[i] == oldStr[0]) // If first character found, check for the rest of the string to replace
                {
                    var k = 1;
                    while (k < oldStr.Length && _buffer[i + k] == oldStr[k])
                        k++;
                    isToReplace = k >= oldStr.Length;
                }

                if (isToReplace) // Do the replacement
                {
                    i += oldStr.Length - 1;
                    for (var k = 0; k < newStr.Length; k++)
                        _temp.Add(newStr[k]);
                }
                else // No replacement, copy the old character
                {
                    _temp.Add(_buffer[i]);
                }
            }

            // Copy back the new string into m_chars
            AddLength(_temp.Count - Length);
            _temp.CopyTo(_buffer);
            Length = _temp.Count;
            _cachedString = null;
            return this;
        }

        public bool StartsWith(string value)
        {
            return StartsWith(value, 0);
        }

        public bool StartsWith(string value, bool ignoreCase)
        {
            return StartsWith(value, 0, ignoreCase);
        }

        public bool StartsWith(string value, int startIndex = 0, bool ignoreCase = false)
        {
            var length = value.Length;
            var n = startIndex + length;
            if (ignoreCase == false)
            {
                for (var i = startIndex; i < n; i++)
                    if (_buffer[i] != value[i - startIndex])
                        return false;
            }
            else
            {
                for (var j = startIndex; j < n; j++)
                    if (char.ToLower(_buffer[j]) != char.ToLower(value[j - startIndex]))
                        return false;
            }

            return true;
        }

        /// <summary> Increase the buffer capacity if necessary </summary>
        private void AddLength(int charsToAdd)
        {
            if (Length + charsToAdd <= Capacity)
                return;

            var newCapacity = Math.Max(Length + charsToAdd, Capacity * 2);
            var newBuffer = new char[newCapacity];
            _buffer.CopyTo(newBuffer, 0);
            _buffer = newBuffer;
        }

        public int IndexOf(char value, int startIndex)
        {
            for (int i = startIndex, n = Length; i < n; i++)
                if (_buffer[i] == value)
                    return i;
            return -1;
        }

        public int IndexOf(string value)
        {
            return IndexOf(value, 0, false);
        }

        public int IndexOf(string value, int startIndex)
        {
            return IndexOf(value, startIndex, false);
        }

        public int IndexOf(string value, bool ignoreCase)
        {
            return IndexOf(value, 0, ignoreCase);
        }

        public int IndexOf(string value, int startIndex, bool ignoreCase)
        {
            var length = value.Length;
            var lengthDelta = Length - length + 1;
            if (ignoreCase == false)
                for (var i = startIndex; i < lengthDelta; i++)
                    if (_buffer[i] == value[0])
                    {
                        var n = 1;
                        while (n < length && _buffer[i + n] == value[n])
                            n++;

                        if (n == length)
                            return i;
                    }
                    else
                    {
                        for (var j = startIndex; j < lengthDelta; j++)
                            if (char.ToLower(_buffer[j]) == char.ToLower(value[0]))
                            {
                                var n = 1;
                                while (n < length && char.ToLower(_buffer[j + n]) == char.ToLower(value[n]))
                                    n++;

                                if (n == length)
                                    return j;
                            }
                    }

            return -1;
        }

        public List<char>.Enumerator GetEnumerator()
        {
            _temp = _temp ?? new List<char>(Capacity);
            _temp.Clear();
            _temp.AddRange(_buffer);
            _temp.RemoveRange(Length, _temp.Count - Length);
            return _temp.GetEnumerator();
        }

        public static implicit operator string(StringBuilder builder)
        {
            return builder.ToString();
        }
    }
}