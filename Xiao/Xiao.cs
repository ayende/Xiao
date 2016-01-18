using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;

namespace Xiao
{
    public class Xiao
    {
        private string[] _termsTable =
        {
            " ", "the", "e", "t", "a", "of", "o", "and", "i", "n", "s", "e ", "r", " th",
            " t", "in", "he", "th", "h", "he ", "to", "\r\n", "l", "s ", "d", " a", "an",
            "er", "c", " o", "d ", "on", " of", "re", "of ", "t ", ", ", "is", "u", "at",
            "   ", "n ", "or", "which", "f", "m", "as", "it", "that", "\n", "was", "en",
            "  ", " w", "es", " an", " i", "f ", "g", "p", "nd", " s", "nd ", "ed ",
            "w", "ed", "http://","https://", "for", "te", "ing", "y ", "The", " c", "ti", "r ", "his",
            "st", " in", "ar", "nt", ",", " to", "y", "ng", " h", "with", "le", "al", "to ",
            "b", "ou", "be", "were", " b", "se", "o ", "ent", "ha", "ng ", "their", "\"",
            "hi", "from", " f", "in ", "de", "ion", "me", "v", ".", "ve", "all", "re ",
            "ri", "ro", "is ", "co", "f t", "are", "ea", ". ", "her", " m", "er ", " p",
            "es ", "by", "they", "di", "ra", "ic", "not", "s, ", "d t", "at ", "ce", "la",
            "h ", "ne", "as ", "tio", "on ", "n t", "io", "we", " a ", "om", ", a", "s o",
            "ur", "li", "ll", "ch", "had", "this", "e t", "g ", " wh", "ere",
            " co", "e o", "a ", "us", " d", "ss", " be", " e",
            "s a", "ma", "one", "t t", "or ", "but", "el", "so", "l ", "e s", "s,", "no",
            "ter", " wa", "iv", "ho", "e a", " r", "hat", "s t", "ns", "ch ", "wh", "tr",
            "ut", "/", "have", "ly ", "ta", " ha", " on", "tha", "-", " l", "ati", "en ",
            "pe", " re", "there", "ass", "si", " fo", "wa", "ec", "our", "who", "its", "z",
            "fo", "rs", "ot", "un", "im", "th ", "nc", "ate", "ver", "ad",
            " we", "ly", "ee", " n", "id", " cl", "ac", "il", "rt", " wi",
            "e, ", " it", "whi", " ma", "ge", "x", "e c", "men", ".com"
        };

        private byte[][] _termsTableBytes;

        private byte[][] _hashTable;

        private int _maxTermSize;
        private int _maxVerbatimLen;

        public Xiao()
        {
            if (_termsTable.Length + 8 > byte.MaxValue)
                throw new InvalidOperationException("Too many terms defined");

            _termsTableBytes = new byte[_termsTable.Length][];
            _maxVerbatimLen = byte.MaxValue - _termsTable.Length;
            _hashTable = new byte[byte.MaxValue][];
            for (int i = 0; i < _termsTable.Length; i++)
            {
                var bytes = Encoding.UTF8.GetBytes(_termsTable[i]);
                if (bytes.Length > byte.MaxValue)
                    throw new InvalidOperationException("Term " + _termsTable[i] + " is too big");
                _termsTableBytes[i] = bytes;
                var buffer = new byte[bytes.Length + 2];// 1 for size, 1 for index
                buffer[0] = (byte)bytes.Length;
                buffer[buffer.Length - 1] = (byte)i;
                Buffer.BlockCopy(bytes, 0, buffer, 1, bytes.Length);
                _maxTermSize = Math.Max(_maxTermSize, bytes.Length);

                int h = bytes[0] << 3;
                AddToHash(h, buffer);
                if (bytes.Length == 1)
                    continue;
                h += bytes[1];
                AddToHash(h, buffer);
                if (bytes.Length == 2)
                    continue;
                h ^= bytes[2];
                AddToHash(h, buffer);
            }
            var empty = new byte[0];

            for (int i = 0; i < _hashTable.Length; i++)
            {
                if (_hashTable[i] == null)
                    _hashTable[i] = empty;
            }
        }

        private void AddToHash(int hash, byte[] buffer)
        {
            var index = hash % _hashTable.Length;
            if (_hashTable[index] == null)
            {
                _hashTable[index] = buffer;
                return;
            }
            var newBuffer = new byte[_hashTable[index].Length + buffer.Length];
            Buffer.BlockCopy(_hashTable[index], 0, newBuffer, 0, _hashTable[index].Length);
            Buffer.BlockCopy(buffer, 0, newBuffer, _hashTable[index].Length, buffer.Length);
            _hashTable[index] = newBuffer;
        }

        public int Decompress(byte[] input, int inputLen, byte[] output)
        {
            var outPos = 0;
            for (int i = 0; i < inputLen; i++)
            {
                var slot = input[i];
                if (slot >= _termsTable.Length)
                {
                    // verbatim entry
                    var len = slot - _termsTable.Length;
                    Buffer.BlockCopy(input, i + 1, output, outPos, len);
                    outPos += len;
                    i += len;
                }
                else
                {
                    Buffer.BlockCopy(_termsTableBytes[slot], 0, output, outPos, _termsTableBytes[slot].Length);
                    outPos += _termsTableBytes[slot].Length;
                }
            }
            return outPos;
        }

        public int Compress(byte[] input, byte[] output)
        {
            var outPos = 0;
            var verbatimStart = 0;
            var verbatimLength = 0;
            for (int i = 0; i < input.Length; i++)
            {
                int size = _maxTermSize;
                int h1, h2 = 0, h3 = 0;
                h1 = input[i] << 3;
                if (i + 1 < input.Length)
                    h2 = h1 + input[i + 1];
                if (i + 2 < input.Length)
                    h3 = h2 ^ input[i + 2];
                if (i + size >= input.Length)
                    size = input.Length - i;
                var foundMatch = false;
                for (; size > 0 && foundMatch == false; size--)
                {
                    byte[] slot;
                    switch (size)
                    {
                        case 1: slot = _hashTable[h1 % _hashTable.Length]; break;
                        case 2: slot = _hashTable[h2 % _hashTable.Length]; break;
                        default: slot = _hashTable[h3 % _hashTable.Length]; break;
                    }
                    int pos = 0;
                    while (pos + 1 // has actual data, not length info
                        < slot.Length)
                    {
                        var termLegnth = slot[pos];
                        if (termLegnth != size ||
                            BufferEquals(slot, pos + 1, input, i, size) == false)
                        {
                            pos += termLegnth + 2;// skip len of string, the size and the index
                            continue;
                        }
                        if (verbatimLength > 0)
                        {
                            Flush(input, output, ref verbatimStart, ref verbatimLength, ref outPos);
                        }
                        output[outPos++] = slot[termLegnth + pos + 1];// get the index to write there
                        verbatimStart = i + termLegnth;
                        i += termLegnth - 1;// skip the length we just compressed
                        foundMatch = true;
                        break;
                    }
                }
                if (foundMatch == false)
                    verbatimLength++;
            }
            Flush(input, output, ref verbatimStart, ref verbatimLength, ref outPos);
            return outPos;
        }

        private void Flush(byte[] input, byte[] output, ref int verbatimStart, ref int verbatimLength, ref int outPos)
        {
            while (verbatimLength > 0)
            {
                var len = Math.Min(_maxVerbatimLen - 1, verbatimLength);
                output[outPos++] = (byte)(len + _termsTable.Length);
                Buffer.BlockCopy(input, verbatimStart, output, outPos, len);
                verbatimStart += len;
                verbatimLength -= len;
                outPos += len;
            }
        }

        public bool BufferEquals(byte[] x, int xStart, byte[] y, int yStart, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (y[yStart + i] != x[xStart + i])
                    return false;
            }
            return true;
        }
    }
}