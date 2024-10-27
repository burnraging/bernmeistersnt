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

namespace common_dll
{
    public static partial class SusxConverter
    {

        public static bool ElementNameToEnum(string name, out ElementEnum elementEnum, out bool isSupportedInV2)
        {
            elementEnum = ElementEnum.INVALID;
            isSupportedInV2 = true;

            if (name == "usx")
                elementEnum = ElementEnum.USX_WRAPPER;
            else if (name == "book")
                elementEnum = ElementEnum.BOOK;
            else if (name == "chapter")
                elementEnum = ElementEnum.CHAPTER;
            else if (name == "par")
                elementEnum = ElementEnum.PARA;
            else if (name == "verse")
                elementEnum = ElementEnum.VERSE;
            else if (name == "char")
                elementEnum = ElementEnum.CHAR;
            else if (name == "text")
                elementEnum = ElementEnum.TEXT;
            else if (name == "optbreak")
                elementEnum = ElementEnum.BREAK;

            return elementEnum != ElementEnum.INVALID;
        }

        public static bool IsBookHeaderParaStyle(string style)
        {
            if (style == null)
                return false;
            string[] headerParaStyles = new string[]
                  { "ide", "h", "h1", "h2", "h3", "toc1", "toc2", "toc3", "toca1", "toca2", "toca3", "rem" };
            return Array.IndexOf(headerParaStyles, style) != -1;
        }

        public static bool IsBookTitleParaStyle(string style)
        {
            if (style == null)
                return false;
            string[] bookTitleStyles = new string[] { "mt", "mt1", "mt2", "mt3", "mt4", "imt", "imt1", "imt2" };
            return Array.IndexOf(bookTitleStyles, style) != -1;
        }

        public static bool IsBookIntroductoryStyle(string style)
        {
            if (style == null)
                return false;

            // the schema shows that "imt", "imt1", "imt2" overlap with book title styles
            // Any para style beginning with 'i' is an introductory
            return style.Length > 1 && style[0] == 'i';
        }


        // If 'paraStyle' is "pi1", then
        // 'paraStyleWithoutNumber'="pi", 'numberAtEndOfStyle'=1
        // Returns 'true' if 'numberAtEndOfStyle' is valid
        public static bool SplitNumberedParaStyle(string paraStyle,
                                                  out string paraStyleWithoutNumber,
                                                  out int numberAtEndOfStyle)
        {
            char lastLetter = paraStyle[paraStyle.Length - 1];
            if (char.IsDigit(lastLetter))
            {
                paraStyleWithoutNumber = paraStyle.Substring(0, paraStyle.Length - 1);
                string lastLetterString = paraStyle.Substring(paraStyle.Length - 1, 1);
                Int32.TryParse(lastLetterString, out numberAtEndOfStyle);
                return true;
            }

            paraStyleWithoutNumber = paraStyle;
            numberAtEndOfStyle = 0;
            return false;
        }

        // Take next step in usx tree.
        // Go down and right. Prefer down over right.
        // Return next index. -1 if tree walk is done.
        public static int TreeStep(ref TreeObject[] tree, int startIndex)
        {
            int index = startIndex;

            bool hasChildren = tree[index].ChildrenCount > 0;
            int siblingToTheRightIndex = tree[index].NextSiblingHeapIndex;
            int parentIndex = tree[index].ParentHeapIndex;

            // Prefer down over right
            if (hasChildren)
            {
                int nextIndex = tree[index].ChildrenHeapIndices[0];
                return nextIndex;
            }
            // Go right if you can
            else if (siblingToTheRightIndex != -1)
            {
                int nextIndex = siblingToTheRightIndex;
                return nextIndex;
            }
            // Else, walk up parents until you find one which has a sibling
            // to the right. Stop at that parent: use its sibling.
            else
            {
                while (parentIndex != -1)
                {
                    int parentsSiblingToTheRightIndex = tree[parentIndex].NextSiblingHeapIndex;
                    if (parentsSiblingToTheRightIndex != -1)
                    {
                        index = parentsSiblingToTheRightIndex;
                        return index;
                    }

                    parentIndex = tree[parentIndex].ParentHeapIndex;
                }

                return -1;
            }
        }

        // Same as TreeStep() except you skip over children.
        // Return next index. -1 if tree walk is done.
        public static int TreeStepBlowByChildren(ref TreeObject[] tree, int startIndex)
        {
            int index = startIndex;

            int siblingToTheRightIndex = tree[index].NextSiblingHeapIndex;
            int parentIndex = tree[index].ParentHeapIndex;

            // Go right if you can
            if (siblingToTheRightIndex != -1)
            {
                int nextIndex = siblingToTheRightIndex;
                return nextIndex;
            }
            // Else, walk up parents until you find one which has a sibling
            // to the right. Stop at that parent: use its sibling.
            else
            {
                while (parentIndex != -1)
                {
                    int parentsSiblingToTheRightIndex = tree[parentIndex].NextSiblingHeapIndex;
                    if (parentsSiblingToTheRightIndex != -1)
                    {
                        index = parentsSiblingToTheRightIndex;
                        return index;
                    }

                    parentIndex = tree[parentIndex].ParentHeapIndex;
                }

                return -1;
            }
        }

        // 'versionString' can be of the form a.b.c or simply a.b
        private static void ScanMajorUsxVersion(string versionString, out int majorVersion)
        {
            bool ok = Utils.ScanDecimalFromStream(ref versionString, 0, out majorVersion, out int throwAway);

            if (!ok)
            {
                Utils.LogFatal("ScanMajorUsxVersion(): Failed scan! {0}", versionString);
            }
        }

    }
}
