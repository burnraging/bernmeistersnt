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
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace common_dll
{

    public enum XmlComponentEnum
    {
        ELEMENT,
        CLOSED_ELEMENT,
        TEXT,
    }

#pragma warning disable CS8618
    public class XmlComponent
    {
        // All componenets
        public XmlComponentEnum Type;
        public int SingleStringStartIndex;     // reference to where this came from. only used for console/errors.
        public int SelfHeapIndex;              // self-reference to where it's stored on heap
        public int ParentHeapIndex;
        public int PreviousSiblingHeapIndex;
        public int NextSiblingHeapIndex;
        public int TreeLevel;
        public int[] Children;
        // NOTE: text contained in an element ("<tag1>some-text</tag1>") is
        //       spun off to another component

        public string ElementName;
        public string[] AttributeName;
        public string[] AttributeValue;  // 1-to-1 with 'AttributeName'

        // TEXT only
        public string Text;              // 
        public bool IsTextAllWhitespace; //

        // internal use
        public bool Closed;
        public int ChildrenCount;        // current size of 'Children'

    }
#pragma warning restore

    // Extras like xml namespaces, DOCTYPEs, ENTITYs are not supported
    // Assumes UTF-8 encoding
    public static class XmlScanner
    {
        public static bool DEBUG_PRINT_DEBUG_FILES = false;
        public static string DEBUG_FOLDER_NAME = "???";

        private const string COMMENT_OPEN = "<!--";
        private const string COMMENT_CLOSE = "-->";
        private const string XML_DECLARATION_OPEN = "<?";
        private const string XML_DECLARATION_CLOSE = "?>";
        private const string DOCTYPE_DECLARATION_OPEN = "<!DOCTYPE";
        private const string ENTITY_OPEN = "<!ENTITY";
        private const string END_CONTAINER_ELEMENT = "</";
        private const string CLOSED_ELEMENT_END = "/>";

        private enum XmlParticleEnum
        {
            INVALID,
            START_CONTAINER_ELEMENT,
            END_CONTAINER_ELEMENT,
            CLOSED_ELEMENT,
            TEXT,
        }

#pragma warning disable CS8618
        private class XmlParticle
        {
            // All componenets
            public XmlParticleEnum Type;
            public int OuterStartIndex;    // index of Particle starting at open bracket
            public int OuterLength;        // length of Particle including start+end brackets

            // START_CONTAINER_ELEMENT, END_CONTAINER_ELEMENT, CLOSED_ELEMENT only
            public int InnerStartIndex;    // inner/tag contents (doesn't inc. open bracket) start index in scan-string
            public int InnerLength;        // inner/tag contents length (doesn't inc. open or close brackets)

            public string ElementName;
            public string[] AttributeName;
            public string[] AttributeValue;  // 1-to-1 with 'AttributeName'

            // TEXT only
            public string Text;              // 
            public bool IsTextAllWhitespace; //
        }
#pragma warning restore

        public static void ScanFile(string inputFileName,
                                    string inputFqFileName,
                                    out string singleStringLines,
                                    out XmlComponent[] heap)
        {
            Utils.ReadFileLines(inputFileName, inputFqFileName, out string[] lines);

            LinesToSingleString(lines, out singleStringLines);
            ScanSingleLine(singleStringLines, out heap);
        }


        // Convert a file in the form of an array of lines into a
        // single string.
        // In the process, strip comments and declarations.
        public static void LinesToSingleString(string[] lines,
                                     out string singleStringLines)
        {
            string[] trimmedLines = new string[lines.Length];
            int n = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                string thisLine = lines[i];

                // Remove spaces from start and end of line.
                // This is per xml spec.
                // If not done correctly, text will be corrupted.
                thisLine = thisLine.Trim();

                // Remove blank lines also
                if (thisLine.Length > 0)
                {
                    trimmedLines[n++] = thisLine;
                }
            }
            Array.Resize(ref trimmedLines, n);

            string singleHumungusString = "";

            for (int i = 0; i < trimmedLines.Length; i++)
            {
                if (i > 0)
                {
                    char lastCharPreviousLine = trimmedLines[i - 1][trimmedLines[i - 1].Length - 1];
                    char firstCharThisLine = trimmedLines[i][0];
                    bool breakBetweenTags = lastCharPreviousLine == '>' && firstCharThisLine == '<';
                    if (!breakBetweenTags)
                    {
                        singleHumungusString += ' ';
                    }
                }

                singleHumungusString += trimmedLines[i];
            }

            char[] singleHumungusArray = singleHumungusString.ToCharArray();
            char[] squeezedHumungusArray = new char[singleHumungusArray.Length];
            int squeezedHumungusArrayCount = 0;

            bool doingComment = false;
            bool doingDeclaration = false;

            for (int i = 0; i < singleHumungusArray.Length; i++)
            {
                char thisChar = singleHumungusArray[i];

                bool isCommentStart = Utils.FoundSubstringInCharArray(ref singleHumungusArray, i, COMMENT_OPEN);
                bool isCommentEnd = Utils.FoundSubstringInCharArray(ref singleHumungusArray, i - COMMENT_CLOSE.Length, COMMENT_CLOSE);
                bool isDeclarationStart = Utils.FoundSubstringInCharArray(ref singleHumungusArray, i, XML_DECLARATION_OPEN);
                bool isDeclarationEnd = Utils.FoundSubstringInCharArray(ref singleHumungusArray, i - XML_DECLARATION_CLOSE.Length, XML_DECLARATION_CLOSE);

                if (isCommentStart)
                    doingComment = true;
                else if (isCommentEnd)
                    doingComment = false;

                if (isDeclarationStart)
                    doingDeclaration = true;
                else if (isDeclarationEnd)
                    doingDeclaration = false;

                if (!(doingComment || doingDeclaration))
                {
                    squeezedHumungusArray[squeezedHumungusArrayCount++] = thisChar;
                }
            }
            Array.Resize(ref squeezedHumungusArray, squeezedHumungusArrayCount);


            // Squeeze whitespaces between start and end tags
            // (not sure about this)
            //   Ex:
            //     <tag1>  </tag1>
            //   Becomes...
            //     <tag1></tag1>
            bool isInterTags = false;
            int interTagStartIndex = -1;
            int interTagLength = -1;

            char[] resqueezedHumungusArray = new char[singleHumungusArray.Length];
            int resqueezedCount = 0;

            for (int i = 0; i < squeezedHumungusArray.Length; i++)
            {
                char thisChar = squeezedHumungusArray[i];

                bool isTagStart = thisChar == '<';
                bool isTagEnd = thisChar == '>';

                if (isTagEnd)
                {
                    isInterTags = true;
                    interTagStartIndex = i + 1;
                }
                else if (isTagStart)
                {
                    // Check chars between angle brackets
                    // If they're all whitespaces, delete them
                    if (isInterTags)
                    {
                        interTagLength = i - interTagStartIndex;

                        bool isAllWhitespace = true;
                        for (int k = interTagStartIndex; k < interTagStartIndex + interTagLength; k++)
                        {
                            char x = squeezedHumungusArray[k];
                            if (!char.IsWhiteSpace(x))
                            {
                                isAllWhitespace = false;
                                break;
                            }
                        }

                        // Squeeze whitespaces by rewinding 'resqueezedCount'
                        // This just means next few bytes will write over whitespaces
                        if (isAllWhitespace && interTagLength > 0)
                        {
                            resqueezedCount -= interTagLength;
                        }
                    }

                    isInterTags = false;
                }

                resqueezedHumungusArray[resqueezedCount++] = thisChar;
            }

            Array.Resize(ref resqueezedHumungusArray, resqueezedCount);
            singleStringLines = new string(resqueezedHumungusArray);
        }


        private static void ScanSingleLine(string singleStringLines,
                                           out XmlComponent[] heap)
        {
            if (DEBUG_PRINT_DEBUG_FILES)
            {
                Utils.ConcatenateFqFileName("single-string-xml.txt", DEBUG_FOLDER_NAME, out string debugSingleLineFqFileName);
                string[] debug_singleLines = new string[100000];
                int debug_singleLinesCount = 0;
                const int SINGLE_LINE_LENGTH = 80;
                int debugLength = singleStringLines.Length;
                int debugLengthWithoutTrailer = (debugLength / SINGLE_LINE_LENGTH) * SINGLE_LINE_LENGTH;
                for (int debug_i = 0; debug_i < debugLengthWithoutTrailer; debug_i += SINGLE_LINE_LENGTH)
                {
                    string debugSingleSubstring = singleStringLines.Substring(debug_i, SINGLE_LINE_LENGTH);
                    debug_singleLines[debug_singleLinesCount++] = string.Format("{0,4}  |{1}|", debug_i / SINGLE_LINE_LENGTH, debugSingleSubstring);
                }
                int debugLastLine = debugLength - debugLengthWithoutTrailer;
                string debugSingleSubstringTrailer = singleStringLines.Substring(debugLengthWithoutTrailer, debugLastLine);
                debug_singleLines[debug_singleLinesCount++] = string.Format("{0,4}  |{1}|", debugLengthWithoutTrailer / SINGLE_LINE_LENGTH + 1, debugSingleSubstringTrailer);
                Array.Resize(ref debug_singleLines, debug_singleLinesCount);
                File.WriteAllLines(debugSingleLineFqFileName, debug_singleLines);
            }

            ScanXmlParticlesFromSingleString(singleStringLines, out XmlParticle[] linearXmlParticleArray);
            FillInTagInnards(ref singleStringLines, ref linearXmlParticleArray);
            BuildComponentsFromParticles(ref singleStringLines, ref linearXmlParticleArray, out heap);
        }


        public static bool RetrieveAttributeValue(string whichAttributeToRetrieve,
                                                  ref XmlComponent component,
                                                  out string thisAttributeValueText,
                                                  out int thisAttributeValueInt)
        {
            thisAttributeValueText = "";
            thisAttributeValueInt = -1;

            if (component.AttributeName != null)
            {
                for (int i = 0; i < component.AttributeName.Length; i++)
                {
                    if (component.AttributeName[i] == whichAttributeToRetrieve)
                    {
                        thisAttributeValueText = component.AttributeValue[i];
                        // Will fail quietly and keep default
                        Int32.TryParse(thisAttributeValueText, out thisAttributeValueInt);

                        return true;
                    }
                }
            }

            return false;
        }

        // A simple xml element has text as content, for example "<my-element>content</my-element>".
        // In this scanner object, the content appears as a separate component, a child of the element.
        // Ignoring the child componenet and retrieving it this way is more convenient.
        public static bool RetrieveTextComponentAsContent(ref XmlComponent[] heap, int index, out string componentText)
        {
            componentText = "";

            if (heap[index].ChildrenCount == 1)
            {
                int childIndex = heap[index].Children[0];
                if (heap[childIndex].Type == XmlComponentEnum.TEXT && heap[childIndex].Text != null)
                {
                    componentText = heap[childIndex].Text;
                    return true;
                }
            }

            return false;
        }


        //******    Working methods


        // Scan an xml file/xml fragment consisting of a single string.
        // This string must have no xml declarations or comments.
        private static void ScanXmlParticlesFromSingleString(string singleString,
                                                             out XmlParticle[] linearXmlObjects)
        {
            linearXmlObjects = new XmlParticle[100000];
            int linearXmlObjectCount = 0;

            int nextTagStartIndex = 0;

            for (int singleStringIndex = 0; singleStringIndex < singleString.Length; singleStringIndex++)
            {
                int leftAngleBracketIndex = singleString.IndexOf("<", singleStringIndex);

                // start of any kind of tag?
                if (leftAngleBracketIndex != -1)
                {
                    // Was there text before this tag?
                    // Create a text Particle for it
                    if (leftAngleBracketIndex > nextTagStartIndex)
                    {
                        // Collect any text between last tag and this tag
                        // Ex: 'The First Book of the Chronicles'
                        //   in '<para style="toc1">The First Book of the Chronicles</para>'
                        if (linearXmlObjectCount > 0)
                        {
                            int textLength = leftAngleBracketIndex - nextTagStartIndex;
                            string textString = singleString.Substring(nextTagStartIndex, textLength);
                            bool isAllWhspc = Utils.IndexOfNonWhiteSpace(ref textString, 0) == -1;

                            XmlParticle textParticle = new XmlParticle
                            {
                                Type = XmlParticleEnum.TEXT,
                                OuterStartIndex = nextTagStartIndex,
                                OuterLength = textLength,
                                Text = textString,
                                IsTextAllWhitespace = isAllWhspc,
                            };
                            linearXmlObjects[linearXmlObjectCount++] = textParticle;
                        }
                        else
                        {
                            int firstNonWhspcIndex = Utils.IndexOfNonWhiteSpace(ref singleString, singleStringIndex);
                            if (firstNonWhspcIndex != -1 && firstNonWhspcIndex < leftAngleBracketIndex)
                                Utils.LogFatal("Extraneous text before first xml tag!");
                        }

                    }

                    XmlParticleEnum thisType = XmlParticleEnum.INVALID;
                    int innerParticleStartIndex = -1;
                    int closeBracketIndex = -1;
                    int closeBracketLength = -1;

                    // "</ tag-name >"
                    if (string.Compare(singleString, leftAngleBracketIndex, END_CONTAINER_ELEMENT, 0, END_CONTAINER_ELEMENT.Length) == 0)
                    {
                        thisType = XmlParticleEnum.END_CONTAINER_ELEMENT;
                        innerParticleStartIndex = leftAngleBracketIndex + END_CONTAINER_ELEMENT.Length;
                        closeBracketLength = ">".Length;
                        closeBracketIndex = singleString.IndexOf(">", leftAngleBracketIndex + ">".Length);
                    }
                    // "< tag-name elements >" or "< tag-name elements />"
                    else
                    {
                        innerParticleStartIndex = leftAngleBracketIndex + 1;

                        // Search for both closing types
                        int closeIndex1 = singleString.IndexOf(CLOSED_ELEMENT_END, leftAngleBracketIndex + CLOSED_ELEMENT_END.Length);
                        int closeIndex2 = singleString.IndexOf(">", leftAngleBracketIndex + ">".Length);
                        bool closeIndex1Valid = closeIndex1 != -1;
                        bool closeIndex2Valid = closeIndex2 != -1;

                        // Found "/>" before ">"? Or found "/>" but didn't find ">"?
                        if (closeIndex1Valid && (!closeIndex2Valid || closeIndex1 < closeIndex2))
                        {
                            thisType = XmlParticleEnum.CLOSED_ELEMENT;
                            closeBracketIndex = closeIndex1;
                            closeBracketLength = CLOSED_ELEMENT_END.Length;
                        }
                        // Found ">" but may or may not have found "/>"?
                        else if (closeIndex2Valid)
                        {
                            thisType = XmlParticleEnum.START_CONTAINER_ELEMENT;
                            closeBracketIndex = closeIndex2;
                            closeBracketLength = 1;
                        }
                        else
                        {
                            Utils.LogFatal("Missing right-bracket! Context: {0}", singleString.Substring(leftAngleBracketIndex, 20));
                        }
                    }

                    int particleLength = closeBracketIndex + closeBracketLength - leftAngleBracketIndex;
                    int innerParticleLength = closeBracketIndex - innerParticleStartIndex;

                    XmlParticle newParticle = new XmlParticle
                    {
                        Type = thisType,
                        OuterStartIndex = leftAngleBracketIndex,
                        OuterLength = particleLength,
                        InnerStartIndex = innerParticleStartIndex,
                        InnerLength = innerParticleLength,
                    };
                    linearXmlObjects[linearXmlObjectCount++] = newParticle;

                    nextTagStartIndex = closeBracketIndex + closeBracketLength;
                    singleStringIndex = nextTagStartIndex - 1;        // -1 b/c loop counter will inc.
                }
                else
                {
                    Utils.LogFatal("Dangling text at end of file/string!");
                }
            }

            Array.Resize(ref linearXmlObjects, linearXmlObjectCount);
        }

        private static void FillInTagInnards(ref string singleString, ref XmlParticle[] xmlParticleArray)
        {
            for (int i = 0; i < xmlParticleArray.Length; i++)
            {
                if (xmlParticleArray[i].Type == XmlParticleEnum.START_CONTAINER_ELEMENT ||
                    xmlParticleArray[i].Type == XmlParticleEnum.END_CONTAINER_ELEMENT ||
                    xmlParticleArray[i].Type == XmlParticleEnum.CLOSED_ELEMENT)
                {
                    ScanTagInnards(ref singleString, ref xmlParticleArray[i]);
                }
            }

#pragma warning disable CS0162
            if (DEBUG_PRINT_DEBUG_FILES)
            {
                string[] debug_line = new string[xmlParticleArray.Length * 50];
                int debug_n = 0;
                for (int debug_i = 0; debug_i < xmlParticleArray.Length; debug_i++)
                {
                    debug_line[debug_n++] = string.Format("*** {0}: Particle {1}", debug_i, xmlParticleArray[debug_i].Type.ToString());
                    debug_line[debug_n++] = string.Format("  *{0}*", singleString.Substring(xmlParticleArray[debug_i].OuterStartIndex, xmlParticleArray[debug_i].OuterLength));
                    debug_line[debug_n++] = string.Format("  -{0}-", singleString.Substring(xmlParticleArray[debug_i].InnerStartIndex, xmlParticleArray[debug_i].InnerLength));
                    debug_line[debug_n++] = string.Format("   |{0}| {1}", xmlParticleArray[debug_i].Text != null ? xmlParticleArray[debug_i].Text : "[no-text]",
                                                           xmlParticleArray[debug_i].IsTextAllWhitespace? "ALL-WHSPC" : "");
                    debug_line[debug_n++] = string.Format("   Element-Name {0}", xmlParticleArray[debug_i].ElementName);
                    if (xmlParticleArray[debug_i].AttributeName != null)
                    {
                        for (int debug_k = 0; debug_k < xmlParticleArray[debug_i].AttributeName.Length; debug_k++)
                        {
                            debug_line[debug_n++] = string.Format("   {0}:  {1}=\"{2}\"", debug_k, xmlParticleArray[debug_i].AttributeName[debug_k], xmlParticleArray[debug_i].AttributeValue[debug_k]);
                        }
                    }
                }
                Array.Resize(ref debug_line, debug_n);
                Utils.ConcatenateFqFileName("xml-scanner-particles.txt", DEBUG_FOLDER_NAME, out string particlesFqFileName);
                File.WriteAllLines(particlesFqFileName, debug_line);
            }
        }
#pragma warning restore

        // Scan tag name and attributes" my-tag attr1="a" attr2="b" "
        private static void ScanTagInnards(ref string singleString, ref XmlParticle xmlParticle)
        {
            string innards = singleString.Substring(xmlParticle.InnerStartIndex, xmlParticle.InnerLength);

            if (innards.Contains("xmlns:"))
            {
                Utils.LogFatal("Namespace declarations are not supported! Context: {0}", singleString.Substring(xmlParticle.InnerStartIndex, 20));
            }

            //char[] delimiters = new char[] { ' ' };
            //string[] delimitedInnards = innards.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
            string[] delimitedInnards = Utils.SplitStringWhileRetainingQuotes(innards);

            if (delimitedInnards.Length == 0)
            {
                Utils.LogFatal("Tag name missing! Context: {0}", singleString.Substring(xmlParticle.InnerStartIndex, 20));
            }

            xmlParticle.ElementName = delimitedInnards[0];
            xmlParticle.AttributeName = new string[0];
            xmlParticle.AttributeValue = new string[0];

            if (xmlParticle.ElementName.Contains(":"))
            {
                Utils.LogFatal("Namespaces in element names are not supported! Context: {0}", singleString.Substring(xmlParticle.InnerStartIndex, 20));
            }

            for (int i = 1; i < delimitedInnards.Length; i++)
            {
                string attributeDeclaration = delimitedInnards[i];

                int equalsSignIndex = attributeDeclaration.IndexOf('=');
                int attributeValuePlusQuotesLength = attributeDeclaration.Length - equalsSignIndex - 1;
                if (equalsSignIndex == -1)
                {
                    Utils.LogFatal("Attribute missing equals sign! Context: {0}", singleString.Substring(xmlParticle.InnerStartIndex, 20));
                }
                else if (equalsSignIndex == 0)
                {
                    Utils.LogFatal("Attribute name missing! Context: {0}", singleString.Substring(xmlParticle.InnerStartIndex, 20));
                }
                // Must not only contain a single letter but start and end quotes
                else if (attributeValuePlusQuotesLength < 3)
                {
                    Utils.LogFatal("Attribute value missing/missing quotes! Context: {0}", singleString.Substring(xmlParticle.InnerStartIndex, 20));
                }

                string attributeName = attributeDeclaration.Substring(0, equalsSignIndex);
                string attributeValuePlusQuotes = attributeDeclaration.Substring(equalsSignIndex + 1);
                string attributeValue = attributeValuePlusQuotes.Substring(1, attributeValuePlusQuotesLength - 2);

                bool isDoubleQuoted = attributeValuePlusQuotes.StartsWith("\"") && attributeValuePlusQuotes.EndsWith("\"");
                bool isSingleQuoted = attributeValuePlusQuotes.StartsWith("'") && attributeValuePlusQuotes.EndsWith("'");

                if (!(isDoubleQuoted || isSingleQuoted))
                {
                    Utils.LogFatal("Attribute value isn't quoted! Context: {0}", singleString.Substring(xmlParticle.InnerStartIndex, 20));
                }
                else if (attributeName.Contains(":"))
                {
                    Utils.LogFatal("Namespaces in attribute names are not supported! Context: {0}", singleString.Substring(xmlParticle.InnerStartIndex, 20));
                }

                int existingIndex = Array.IndexOf(xmlParticle.AttributeName, attributeName);
                if (existingIndex != -1)
                {
                    Utils.LogFatal("Duplication of attribute name {0}! Context: {1}", attributeName, singleString.Substring(xmlParticle.InnerStartIndex, 20));
                }

                Array.Resize(ref xmlParticle.AttributeName, xmlParticle.AttributeName.Length + 1);
                Array.Resize(ref xmlParticle.AttributeValue, xmlParticle.AttributeValue.Length + 1);
                xmlParticle.AttributeName[^1] = attributeName;
                xmlParticle.AttributeValue[^1] = attributeValue;
            }
        }

        private static void BuildComponentsFromParticles(ref string singleString,
                                                         ref XmlParticle[] particleArray,
                                                         out XmlComponent[] xmlHeap)
        {
            xmlHeap = new XmlComponent[particleArray.Length];
            int xmlHeapCount = 0;

            int level = 0;
            int lastParentElementIndex = -1;
            int currentLevelCount = 0;

            for (int particleIndex = 0; particleIndex < particleArray.Length; particleIndex++)
            {
                XmlParticleEnum thisParticleEnum = particleArray[particleIndex].Type;

                // Begin top level
                if (level == 0 && currentLevelCount == 0)
                {
                    if (particleIndex != 0)
                    {
                        Utils.LogFatal("BuildComponentsFromParticles(): Out of wack somehow!");
                    }
                    else if (thisParticleEnum == XmlParticleEnum.CLOSED_ELEMENT)
                    {
                        Utils.LogFatal("BuildComponentsFromParticles(): Don't support roots which are closed-elements!");
                    }

                    lastParentElementIndex = xmlHeapCount;

                    XmlComponent newComponent = new XmlComponent
                    {
                        Type = XmlComponentEnum.ELEMENT,
                        SingleStringStartIndex = particleArray[particleIndex].OuterStartIndex,
                        SelfHeapIndex = xmlHeapCount,
                        ParentHeapIndex = -1,           // "I'm the root component"
                        PreviousSiblingHeapIndex = -1,
                        NextSiblingHeapIndex = -1,
                        TreeLevel = 0,
                        Children = new int[100000],
                        ChildrenCount = 0,

                        ElementName = particleArray[particleIndex].ElementName,
                        AttributeName = particleArray[particleIndex].AttributeName,
                        AttributeValue = particleArray[particleIndex].AttributeValue,
                    };
                    xmlHeap[xmlHeapCount++] = newComponent;

                    level++;
                }
                // Not top-level particle?
                else
                {
                    string particleElementName = particleArray[particleIndex].ElementName;

                    // Can this particle be/start a child?
                    if (thisParticleEnum == XmlParticleEnum.START_CONTAINER_ELEMENT ||
                        thisParticleEnum == XmlParticleEnum.CLOSED_ELEMENT ||
                        thisParticleEnum == XmlParticleEnum.TEXT)
                    {
                        if (xmlHeap[lastParentElementIndex].Closed)
                        {
                            Utils.LogFatal("BuildComponentsFromParticles(): No open xml parent element for this new componenent! Context: {0}",
                                          singleString.Substring(particleArray[particleIndex].OuterStartIndex, 20));
                        }

                        int thisComponentHeapIndex = xmlHeapCount;    // index this new component will be assigned

                        int previousSiblingHeapIndex = -1;
                        int parentChildCount = xmlHeap[lastParentElementIndex].ChildrenCount;
                        if (parentChildCount > 0)
                        {
                            // Update previous sibling's link
                            previousSiblingHeapIndex = xmlHeap[lastParentElementIndex].Children[parentChildCount - 1];
                            xmlHeap[previousSiblingHeapIndex].NextSiblingHeapIndex = thisComponentHeapIndex;
                        }

                        if (xmlHeap[lastParentElementIndex].Type != XmlComponentEnum.ELEMENT ||
                            xmlHeap[lastParentElementIndex].Closed)
                        {
                            Utils.LogFatal("BuildComponentsFromParticles(): This component's parent is closed or an invalid element! Context: {0}",
                                          singleString.Substring(particleArray[particleIndex].OuterStartIndex, 20));
                        }

                        // Add this child to parent
                        xmlHeap[lastParentElementIndex].Children[parentChildCount] = thisComponentHeapIndex;
                        xmlHeap[lastParentElementIndex].ChildrenCount++;

                        XmlComponentEnum newType;
                        if (thisParticleEnum == XmlParticleEnum.START_CONTAINER_ELEMENT)
                            newType = XmlComponentEnum.ELEMENT;
                        else if (thisParticleEnum == XmlParticleEnum.CLOSED_ELEMENT)
                            newType = XmlComponentEnum.CLOSED_ELEMENT;
                        else // else if (thisParticleEnum == XmlParticleEnum.TEXT)
                            newType = XmlComponentEnum.TEXT;

                        int[] childrenArray;
                        if (newType == XmlComponentEnum.TEXT || newType == XmlComponentEnum.CLOSED_ELEMENT)
                            childrenArray = new int[0];
                        else
                            childrenArray = new int[100000];

                        XmlComponent newComponent = new XmlComponent
                        {
                            Type = newType,
                            SingleStringStartIndex = particleArray[particleIndex].OuterStartIndex,
                            SelfHeapIndex = thisComponentHeapIndex,
                            ParentHeapIndex = lastParentElementIndex,
                            PreviousSiblingHeapIndex = previousSiblingHeapIndex,
                            NextSiblingHeapIndex = -1,
                            TreeLevel = level,
                            Children = childrenArray,
                            ChildrenCount = 0,

                            ElementName = particleElementName,
                            AttributeName = particleArray[particleIndex].AttributeName,
                            AttributeValue = particleArray[particleIndex].AttributeValue,

                            Text = particleArray[particleIndex].Text,
                            IsTextAllWhitespace = particleArray[particleIndex].IsTextAllWhitespace,
                        };
                        xmlHeap[xmlHeapCount++] = newComponent;

                        if (newType == XmlComponentEnum.ELEMENT)
                        {
                            // We are the new parent
                            lastParentElementIndex = thisComponentHeapIndex;
                            level++;
                        }
                    }
                    // Close off previously opened element
                    else if (thisParticleEnum == XmlParticleEnum.END_CONTAINER_ELEMENT)
                    {
                        if (xmlHeap[lastParentElementIndex].Type != XmlComponentEnum.ELEMENT)
                        {
                            Utils.LogFatal("BuildComponentsFromParticles(): No open xml element for this close! Context: {0}",
                                          singleString.Substring(particleArray[particleIndex].OuterStartIndex, 20));
                        }
                        else if (particleElementName != xmlHeap[lastParentElementIndex].ElementName)
                        {
                            Utils.LogFatal("BuildComponentsFromParticles(): Close xml element mismatch! Context: {0}",
                                          singleString.Substring(xmlHeap[^1].SingleStringStartIndex, 20));
                        }

                        // Close off parent element
                        xmlHeap[lastParentElementIndex].Closed = true;
                        Array.Resize(ref xmlHeap[lastParentElementIndex].Children, xmlHeap[lastParentElementIndex].ChildrenCount);

                        // Move up one level
                        lastParentElementIndex = xmlHeap[lastParentElementIndex].ParentHeapIndex;
                        level--;
                    }
                    else
                    {
                        Utils.LogFatal("BuildComponentsFromParticles(): Invalid particle! Context: {0}",
                                      singleString.Substring(particleArray[particleIndex].OuterStartIndex, 20));
                    }
                }
            }

            Array.Resize(ref xmlHeap, xmlHeapCount);

            if (level != 0)
            {
                Utils.LogFatal("BuildComponentsFromParticles(): Unfinished tree! Level remains at {0}",
                              level);
            }
            else if (xmlHeap[xmlHeapCount - 1].Type == XmlComponentEnum.ELEMENT && xmlHeap[xmlHeapCount - 1].Closed == false)
            {
                Utils.LogFatal("BuildComponentsFromParticles(): Dangling componenent! Context: {0}",
                              singleString.Substring(xmlHeap[xmlHeapCount - 1].SingleStringStartIndex, 20));
            }

#pragma warning disable CS0162
            if (DEBUG_PRINT_DEBUG_FILES)
            {
                string[] debug_line = new string[xmlHeap.Length * 50];
                int debug_n = 0;
                for (int debug_i = 0; debug_i < xmlHeap.Length; debug_i++)
                {
                    debug_line[debug_n++] = string.Format("*** {0}: Component {1}", debug_i, xmlHeap[debug_i].Type.ToString());
                    debug_line[debug_n++] = string.Format("  SingleStringStartIndex={0} SelfHeapIndex={1}", xmlHeap[debug_i].SingleStringStartIndex, xmlHeap[debug_i].SelfHeapIndex);
                    debug_line[debug_n++] = string.Format("  ParentHeapIndex={0} PreviousSiblingHeapIndex={1} NextSiblingHeapIndex={2}",
                                                  xmlHeap[debug_i].ParentHeapIndex, xmlHeap[debug_i].PreviousSiblingHeapIndex, xmlHeap[debug_i].NextSiblingHeapIndex);
                    debug_line[debug_n++] = string.Format("  TreeLevel={0}   [Closed={1}, ChildrenCount={2}]",
                                                  xmlHeap[debug_i].TreeLevel, xmlHeap[debug_i].Closed, xmlHeap[debug_i].ChildrenCount);
                    if (xmlHeap[debug_i].Children != null && xmlHeap[debug_i].Children.Length > 0)
                    {
                        for (int k = 0; k < xmlHeap[debug_i].Children.Length; k++)
                        {
                            debug_line[debug_n++] = string.Format("  {0}: {1}", k, xmlHeap[debug_i].Children[k]);
                        }
                    }
                    else
                        debug_line[debug_n++] = string.Format("  [no children]");
                    debug_line[debug_n++] = string.Format("  ElementName={0}", xmlHeap[debug_i].ElementName);
                    if (xmlHeap[debug_i].AttributeName != null && xmlHeap[debug_i].AttributeName.Length > 0)
                    {
                        for (int k = 0; k < xmlHeap[debug_i].AttributeName.Length; k++)
                        {
                            debug_line[debug_n++] = string.Format("  {0}: {1}={2}", k, xmlHeap[debug_i].AttributeName[k], xmlHeap[debug_i].AttributeValue[k]);
                        }
                    }
                    else
                        debug_line[debug_n++] = string.Format("  [no attributes]");
                    if (xmlHeap[debug_i].Text != null && xmlHeap[debug_i].Text.Length > 0)
                    {
                        debug_line[debug_n++] = string.Format("  |{0}| {1}", xmlHeap[debug_i].Text, xmlHeap[debug_i].IsTextAllWhitespace? "ALL-WHSPC" : "");
                    }
                    else
                        debug_line[debug_n++] = string.Format("  [no text]");
                }
                Array.Resize(ref debug_line, debug_n);
                Utils.ConcatenateFqFileName("xml-scanner-components.txt", DEBUG_FOLDER_NAME, out string componenentsFqFileName);
                File.WriteAllLines(componenentsFqFileName, debug_line);
            }
#pragma warning restore
        }
    }
}