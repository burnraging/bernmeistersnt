/*
Copyright (c) 2024 Bernard M. Woodland

Permission is hereby granted, free of charge, to any person
obtaining a copy of this software and associated documentation
files (the "Software"), to deal in the Software without
restriction, including without limitation the rights to use, copy,
modify, merge, publish, distribute, sublicense, and/or sell copies
of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace common_dll
{
    public class Utils
    {

        //**********    Scanning Utilities

        // Scans only in range from 0 to 999, preferring the larger number of contiguous digits
        public static bool ScanDecimalFromStream(ref string inString, int offset, out int scanValue, out int numCharsConsumed)
        {
            scanValue = 0;
            numCharsConsumed = 0;

            for (int i = offset; i < inString.Length; i++)
            {
                char x = inString[i];
                if (x < '0' || x > '9')
                {
                    break;
                }
                numCharsConsumed++;
            }

            if (numCharsConsumed > 0)
            {
                string scanString = inString.Substring(offset, numCharsConsumed);
                return Int32.TryParse(scanString, out scanValue);
            }

            return false;
        }

        // Equivalent of library string.Split() call, but ignores whitespaces
        //   inside quotations.
        public static string[] SplitStringWhileRetainingQuotes(string inString)
        {
            char[] candidateSubChars = new char[] { '@', '^', '`', '*' };
            char subChar = 'x';
            for (int i = 0; i < candidateSubChars.Length; i++)
            {
                if (inString.IndexOf(candidateSubChars[i]) == -1)
                {
                    subChar = candidateSubChars[i];
                    break;
                }
                if (i == candidateSubChars.Length - 1)
                    LogFatal("SplitStringWhileRetainingQuotes() Hack was broken :( !");
            }

            char[] subStringChars = inString.ToCharArray();
            int inStringLength = inString.Length;
            bool doingSingleQuote = false;
            bool doingDoubleQuote = false;

            for (int i = 0; i < inStringLength; i++)
            {
                char x = inString[i];
                bool isSingleQuote = x == '\'';
                bool isDoubleQuote = x == '"';

                if (isSingleQuote)
                    doingSingleQuote = !doingSingleQuote;
                if (isDoubleQuote)
                    doingDoubleQuote = !doingDoubleQuote;

                if ((doingSingleQuote || doingDoubleQuote) && (x == ' '))
                    subStringChars[i] = subChar;
                else
                    subStringChars[i] = x;
            }

            string subString = new string(subStringChars);
            char[] delimiters = new char[] { ' ' };
            string[] splitStrings = subString.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < splitStrings.Length; i++)
            {
                splitStrings[i] = splitStrings[i].Replace(subChar, ' ');
            }

            return splitStrings;
        }

        public static int IndexOfNonWhiteSpace(ref string inString, int startIndex)
        {
            if (inString == null || inString.Length == 0)
                return -1;

            for (int i = startIndex; i < inString.Length; i++)
            {
                char x = inString[i];
                if (!char.IsWhiteSpace(x))
                {
                    return i;
                }
            }

            return -1;
        }

        public static int IndexOfWhiteSpace(ref string inString, int startIndex)
        {
            if (inString == null || inString.Length == 0)
                return -1;

            for (int i = startIndex; i < inString.Length; i++)
            {
                char x = inString[i];
                if (char.IsWhiteSpace(x))
                {
                    return i;
                }
            }

            return -1;
        }

        public static bool FoundSubstringInString(ref string inString, int inStringOffset, string matchString)
        {
            if (inStringOffset < 0 || inStringOffset + matchString.Length > inString.Length)
                return false;

            for (int i = 0; i < matchString.Length; i++)
            {
                if (matchString[i] != inString[inStringOffset + i])
                    return false;
            }

            return true;
        }

        public static bool FoundSubstringInCharArray(ref char[] inCharArray, int inCharArrayOffset, string matchString)
        {
            if (inCharArrayOffset < 0 || inCharArrayOffset + matchString.Length > inCharArray.Length)
                return false;

            for (int i = 0; i < matchString.Length; i++)
            {
                if (matchString[i] != inCharArray[inCharArrayOffset + i])
                    return false;
            }

            return true;
        }

        // Returns next index.
        // If no next index, returns -1       // 'last-index + 1' (string length)
        public static int NextPlaceToBreakXmlLine(ref string singleString, int startIndex)
        {
            int singleStringLength = singleString.Length;

            if (startIndex >= singleStringLength)
                return -1;

            // XML Relax NG validator doesn't like when you break for a new line in the middle
            // of a double-quoted (or single-quoted, but we'll ignore that) string.
            // I don't know what XML standard specifies, but who cares if validator won't work?
            bool processingDoubleQuotes = false;

            for (int i = 0; i < singleStringLength; i++)
            {
                char x = singleString[i];

                if (x == '"')
                {
                    processingDoubleQuotes = !processingDoubleQuotes;
                }

                if (i >= startIndex)
                {
                    if (x == ' ' && !processingDoubleQuotes)
                        return i;
                }
            }

            return -1;
        }

        public static string BuildStringWithThisManySpaces(int numberOfSpaces)
        {
            string returnString = "";

            for (int i = 0; i < numberOfSpaces; i++)
            {
                returnString += " ";
            }

            return returnString;
        }

        // Returns an array which has as many spaces as indentation level (index) selects
        public static string[] BuildIndentationArray(int spacesToIndent = 2, int maxIndentationLevel = 10)
        {
            string[] indentations = new string[maxIndentationLevel];
            indentations[0] = "";

            // is spacesToIndent == 2, then
            // indentation[1] = 2 spaces, indentations[2] = 4 spaces, 6 spaces, 8 spaces, etc.
            indentations[1] = "";
            for (int i = 0; i < spacesToIndent; i++)
            {
                indentations[1] += " ";
            }
            for (int i = 2; i < maxIndentationLevel; i++)
            {
                indentations[i] = indentations[i - 1];
                indentations[i] += indentations[1];
            }

            return indentations;
        }

        //*******    Path and File Utilitities

        // Make a fully-qualified file path+name out of individual componenents
        //    fqFileName = rootDirectory + subDirectory + fileName
        public static void ConcatenateFqFileName(string fileName, string rootDirectory, out string fqFileName, string subDirectory = null!)
        {
            if (rootDirectory == null)
                rootDirectory = "";
            if (subDirectory == null)
                subDirectory = "";

            string directoryName = Path.Combine(rootDirectory, subDirectory);
            fqFileName = Path.Combine(directoryName, fileName);
        }

        public static void ReadFileLines(string fileName, string fqFileName, out string[] lines)
        {
            lines = null!;

            if (fileName == null || fqFileName == null)
            {
                Utils.LogFatal("ReadFileLines(): Null parm(s)!");
            }

#pragma warning disable CS8600
            string folderOnly = Path.GetDirectoryName(fqFileName);
#pragma warning restore

#pragma warning disable CS8602, CS8604
            if (!Path.Exists(folderOnly))
            {
                Utils.LogFatal("ReadFileLines():{0}: folder {1} doesn't exist!", fileName, folderOnly);
            }

            if (!File.Exists(fqFileName))
            {
                Utils.LogFatal("ReadFileLines():{0}: file doesn't exist!", fileName);
            }

            lines = File.ReadAllLines(fqFileName);

            if (lines == null)
            {
                Utils.LogFatal("ReadFileLines():{0}: file can't be opened!", fileName);
            }

            if (lines.Length == 0 || (lines.Length == 1 && string.IsNullOrEmpty(lines[0])))
            {
                Utils.LogFatal("ReadFileLines(): {0}: file is empty!", fileName);
            }
#pragma warning restore
        }

        public static void WriteFileLines(string fileName, string fqFileName, ref string[] lines, bool overriteIfExists = false)
        {
            if (fileName == null || fqFileName == null)
            {
                Utils.LogFatal("WriteFileLines(): Null parm(s)!");
            }

#pragma warning disable CS8600
            string folderOnly = Path.GetDirectoryName(fqFileName);
#pragma warning restore

#pragma warning disable CS8602, CS8604
            if (!Path.Exists(folderOnly))
            {
                Utils.LogFatal("WriteFileLines(): {0}: folder {1} doesn't exist!", fileName, folderOnly);
            }

            if (File.Exists(fqFileName) && !overriteIfExists)
            {
                Utils.LogFatal("WriteFileLines(): {0}: file exists, but we must not overwrite it!", fileName);
            }

            File.WriteAllLines(fqFileName, lines);
#pragma warning restore
        }

        //**************   Misc Utilities     ********/

        // Does 'matchToArray' appear in 'inputCharArray' at 'offset'?
        public static bool CharArrayMatch(ref char[] inputCharArray, int offset, ref char[] matchToArray, bool caseInsensitive = false)
        {
            if (matchToArray.Length + offset <= inputCharArray.Length)
            {
                for (int i = 0; i < matchToArray.Length; i++)
                {
                    if (caseInsensitive)
                    {
                        if (Char.ToLower(inputCharArray[i + offset]) != Char.ToLower(matchToArray[i]))
                            return false;
                    }
                    else
                    {
                        if (inputCharArray[i + offset] != matchToArray[i])
                            return false;
                    }
                }

                return true;
            }

            return false;
        }

        public static void BitMaskToIndividualBits(int bitMask, out int[] individualBits)
        {
            individualBits = new int[32];
            int individualBitsSize = 0;

            int movingMask = 1;

            for (int i = 0; i < 32; i++)
            {
                if ((bitMask & movingMask) != 0)
                {
                    individualBits[individualBitsSize++] = movingMask;
                    bitMask &= ~movingMask;

                    if (bitMask == 0)
                        break;
                }

                movingMask <<= 1;
            }

            Array.Resize(ref individualBits, individualBitsSize);
        }

        //**************   Log/Console Utilities    ********/

        public static void LogFatal(string formatString, params object[] parameterList)
        {
            string messageText;
            if (formatString == null || formatString.Length == 0)
            {
                messageText = "LogFatal() fix code!";
            }
            else
            {
                messageText = string.Format(formatString, parameterList);
            }

#if DEBUG
            Console.WriteLine(messageText);
#else
#endif
            Environment.Exit(1);
        }

        public static void LogEntry(string formatString, params object[] parameterList)
        {
            string messageText;
            if (formatString == null || formatString.Length == 0)
            {
                messageText = "LogEntry() fix code!";
            }
            else
            {
                messageText = string.Format(formatString, parameterList);
            }

#if DEBUG
            Console.WriteLine(messageText);
#else
#endif
        }

    }
}
