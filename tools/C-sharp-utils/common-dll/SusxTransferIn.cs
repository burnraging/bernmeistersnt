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

    // Susx= "Scanned USX". Scanned from XML usx source.
    public static partial class SusxConverter
    {
        public enum ElementEnum
        {
            // Named according to xml tag name, not according to usx schema name
            INVALID,
            USX_WRAPPER,
            BOOK,
            CHAPTER,
            PARA,
            NOTE,
            VERSE,
            CHAR,             // <char>...</char>
            TEXT,             // <xyz>some-text</xyz>
            BREAK,            // <optbreak />
        }

#pragma warning disable CS8618
        public class TreeObject
        {
            // Tree management
            public int Level;
            public int ParentHeapIndex;
            public int PreviousSiblingHeapIndex;
            public int NextSiblingHeapIndex;
            public int[] ChildrenHeapIndices;

            // per-node values
            public ElementEnum ElementType;
            public string Style;
            public string Code;   // "book" only
            public string Caller; // "note" only  ("Category" not supported)
            public int Number;    // "chapter", "verse"; "para" but only when para element has an sid attribute
            public string Sid;    // "chapter", "verse" only
            public string Eid;    // "chapter", "verse" only
            public string Vid;    // "para" only
            public string Text;   // TEXT, "book", "para" header styles "h", "toc1", "toc2", toc3"

            // internal use
            public int ChildrenCount;
        }
#pragma warning restore


        // Check that first tag is a "usx" and that it specifies a version we support.
        // Abort if any checks fail
        public static void SuperficialScanUsxSanity(ref XmlComponent[] xmlHeap,
                                                    out int majorVersionNumber)
        {
            majorVersionNumber = 0;

            if (xmlHeap == null || xmlHeap.Length < 4)
            {
                Utils.LogFatal("Empty file!");
            }

#pragma warning disable CS8602
            XmlComponent usxComponent = xmlHeap[0];
#pragma warning restore

            if (!ElementNameToEnum(usxComponent.ElementName, out ElementEnum elementEnum, out bool isValidV2ElementName))
            {
                Utils.LogFatal("Invalid or unsupported element name '{0}'! Is this a USX file?", usxComponent.ElementName);
            }

            if (elementEnum != ElementEnum.USX_WRAPPER)
            {
                Utils.LogFatal("Failed to find usx wrapper! Is this a USX file?");
            }

            XmlScanner.RetrieveAttributeValue("version", ref usxComponent, out string versionString, out int versionIntThrowAway);
            ScanMajorUsxVersion(versionString, out majorVersionNumber);

            if (majorVersionNumber < 2 && majorVersionNumber > 3)
            {
                Utils.LogFatal("USX version not supported! {0}", versionString);
            }
        }

        // When a TreeObject.ElementType==ElementEnum.PARA, if
        // TreeObject.Style matches one found in this method, then
        // the TreeObject object cheats and attaches its text to the para
        // object instead of creating a separate ElementEnum.TEXT object
        // like it should.
        public static bool IsHeadingLikeParagraphStyle(string style)
        {
            return style == "h" || style == "mt" || style.StartsWith("mt") || style.StartsWith("mte") ||
                        style.StartsWith("ms") || style == "toc1" || style == "toc2" || style == "toc3";
        }

    }
}
