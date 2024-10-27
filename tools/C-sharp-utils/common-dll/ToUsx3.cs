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
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static common_dll.SusxConverter;

namespace common_dll
{
    public class ToUsx3
    {
        public static string? TOUSX3_INPUT_FOLDER;
        public static string? TOUSX3_OUTPUT_FOLDER;
        public static string? TOUSX3_TRANSLATION_NAME;
        public static string[]? TOUSX3_COPYRIGHT_TEXT;

#pragma warning disable CS8618

        private static string SingleString;
        private static XmlComponent[] XmlHeap;
        private static int XmlHeapIndex;

        private static TreeObject[] UsxHeap;
        private static int UsxHeapSize;
        private static int CurrentUsxParent;

        private static string BookCode;
        private static int CurrentChapterNumber;
        private static int ChapterLevel;

        private static bool InTheMiddleOfAVerse;
        private static int CurrentVerseNumber;
#pragma warning restore

        public static void ConvertBible()
        {
            if (UsxDefinitions.BibleBookNamesText == null)
                UsxDefinitions.LoadBibleBookNames();

            int bookEnumSize = Enum.GetNames(typeof(BookEnum)).Length;
            for (int i = BibleBooks.NUM_BOOKS_IN_OLD_TESTAMENT; i < bookEnumSize; i++)
            {
                //BookEnum thisBookEnum = (BookEnum)i;
#pragma warning disable CS8602
                BookCode = UsxDefinitions.BibleBookNamesText[i];
#pragma warning restore

                string usx2FileName = string.Format("{0}.usx", BookCode);
#pragma warning disable CS8604
                Utils.ConcatenateFqFileName(usx2FileName, TOUSX3_INPUT_FOLDER, out string usx2FqFileName);
#pragma warning restore
                XmlScanner.ScanFile(usx2FileName, usx2FqFileName, out string localSingleString, out XmlComponent[] localXmlHeap);
                XmlHeapIndex = 0;

                ConvertUsx2ToUsx3(ref localSingleString, ref localXmlHeap);

                // write to file
#pragma warning disable CS8604
                SusxConverter.WriteBook(BookCode, ref UsxHeap, TOUSX3_OUTPUT_FOLDER, TOUSX3_COPYRIGHT_TEXT, TOUSX3_TRANSLATION_NAME);
#pragma warning restore
            }
        }

        public static void ConvertUsx2ToUsx3(ref string singleString,
                                             ref XmlComponent[] xmlHeap)
        {
            SingleString = singleString;
            XmlHeap = xmlHeap;

            SusxConverter.SuperficialScanUsxSanity(ref XmlHeap, out int majorVersionNumber);
            if (majorVersionNumber != 2)
            {
                Utils.LogEntry("Expecting a USX 2.x file and found somethings else!");
            }

            UsxHeap = new TreeObject[XmlHeap.Length * 2];
            UsxHeapSize = 0;

            MakeRootNode();

            CurrentUsxParent = 0;
            StepToNextXmlNode(XmlNodeStepMode.SKIP_INNER_TEXT_COMPONENT);

            // 2.x and 3.x are same: <book code="1CH" style="id">- American Standard Version</book>
            XmlScanner.RetrieveAttributeValue("code", ref XmlHeap[XmlHeapIndex], out BookCode, out int codeThrowAway);
            XmlScanner.RetrieveTextComponentAsContent(ref XmlHeap, XmlHeapIndex, out string bookName);
            int bookElementIndex = MakeNewChildNode(ElementEnum.BOOK, CurrentUsxParent);
            UsxHeap[bookElementIndex].Code = BookCode;
            UsxHeap[bookElementIndex].Style = "id";
            UsxHeap[bookElementIndex].Text = bookName;
            CloseOffThisNodesChildren(bookElementIndex);

            StepToNextXmlNode(XmlNodeStepMode.SKIP_INNER_TEXT_COMPONENT);

            ScanIntroductoryParagraphs();

            while (true)
            {
                ScanChapter();

                // Reached the last component/returned back to usx element?
                if (XmlHeapIndex == -1)
                {
                    // We're done
                    break;
                }
            }

            Array.Resize(ref UsxHeap, UsxHeapSize);

            // trim all children arrays
            for (int i = 0; i < UsxHeap.Length; i++)
            {
                Array.Resize(ref UsxHeap[i].ChildrenHeapIndices, UsxHeap[i].ChildrenCount);
            }

            if (UsxDefinitions.BibleBookNamesText == null)
                UsxDefinitions.LoadBibleBookNames();
        }


        private static void ScanIntroductoryParagraphs()
        {
            // book headers
            while (true)
            {
                string thisElementName = XmlHeap[XmlHeapIndex].ElementName;
                XmlScanner.RetrieveAttributeValue("style", ref XmlHeap[XmlHeapIndex], out string style, out int styleThrowaway);

                if (thisElementName == "para")
                {
                    if (IsHeadingLikeParagraphStyle(style))
                    {
                        int index = MakeNewChildNode(ElementEnum.PARA, CurrentUsxParent);
                        UsxHeap[index].Style = style;
                        CloseOffThisNodesChildren(index);

                        XmlScanner.RetrieveTextComponentAsContent(ref XmlHeap, XmlHeapIndex, out UsxHeap[index].Text);
                        StepToNextXmlNode(XmlNodeStepMode.SKIP_INNER_TEXT_COMPONENT);
                    }
                    else
                    {
                        // Risky...have no way of testing this...2.x schema is hard to find...don't have time
                        // ...hope it all just works and pulls in any introductory paragraphs
                        //ScanParaInChapter();
                        // On second thought, just drop it, instead of risking an error
                        BlowByThisNodesChildren();
                    }
                }
                else
                {
                    break;
                }
            }

            if (XmlHeapIndex == -1)
                Utils.LogFatal("ScanIntroductoryParagraphs() Ran out of xml elements!");
        }

        private static void ScanChapter()
        {
            if (XmlHeap[XmlHeapIndex].ElementName != "chapter")
            {
                Utils.LogFatal("Expected chapter tag!");
            }

            XmlScanner.RetrieveAttributeValue("number", ref XmlHeap[XmlHeapIndex], out string chapterNumberText, out CurrentChapterNumber);
            ChapterLevel = XmlHeap[XmlHeapIndex].TreeLevel;
            StepToNextXmlNode(XmlNodeStepMode.SKIP_INNER_TEXT_COMPONENT);

            // The "chapter" beginning and end elements are closed/have no children of their own
            int newChildNodeIndex = MakeNewChildNode(ElementEnum.CHAPTER, CurrentUsxParent);
            UsxHeap[newChildNodeIndex].Number = CurrentChapterNumber;
            UsxHeap[newChildNodeIndex].Style = "c";
            UsxHeap[newChildNodeIndex].Sid = string.Format("{0} {1}", BookCode, CurrentChapterNumber);

            int originalChapterNumber = CurrentChapterNumber;
            int originalChapterParent = CurrentUsxParent;

            string[] unsupportedV2Elements = new string[] {"table", "note", "sidebar"};

            while (true)
            {
                string elementName = XmlHeap[XmlHeapIndex].ElementName;
                XmlComponentEnum xmlType = XmlHeap[XmlHeapIndex].Type;

                // Start of the next chapter?
                if (elementName == "chapter")
                {
                    XmlScanner.RetrieveAttributeValue("number", ref XmlHeap[XmlHeapIndex], out string chapterNumberText2, out CurrentChapterNumber);
                    break;
                }
                else if (elementName == "para")
                {
                    ScanParaInChapter();
                }
                else if (Array.IndexOf(unsupportedV2Elements, elementName) != -1)
                {
                    // quietly discard
                    BlowByThisNodesChildren();
                }
                else
                {
                    Utils.LogEntry("Unrecognizable element '{0}'!", elementName);
                }

                // Finished?
                if (XmlHeapIndex == -1)
                {
                    break;
                }
            }

            // Add end chapter element
            newChildNodeIndex = MakeNewChildNode(ElementEnum.CHAPTER, originalChapterParent);
            UsxHeap[newChildNodeIndex].Number = originalChapterNumber;
            UsxHeap[newChildNodeIndex].Style = "c";
            UsxHeap[newChildNodeIndex].Eid = string.Format("{0} {1}", BookCode, originalChapterNumber);
        }

        private static bool IsParaStyleUnsupported(string paraStyle)
        {
            // 2.x supported styles:
            //  mt# = main title
            //  mte# = main title at end of introduction
            //  ms# = major section heading
            //  mr = major section reference range
            //  s = section heading
            //  sr = section reference range
            //  r = parallel passage range
            //  d = descriptive title
            //  sp = speaker identification
            //  p = normal paragraph
            //  m = margin paragraph (non-indented continuation from poetry, etc.)
            //  pmo = embedded text opening (like a salutation in an in-line letter)
            //  pm = embedded text paragraph
            //  pmc = embedded text closing
            //  pmr = embedded text refrain
            //  pi# = indented paragraph
            //  mi = indented flush-left paragraph (small indentation?)
            //  cls = closure of an epistle
            //  li# = list item (non-indented first line + indented following lines
            //  pc = centered paragraph
            //  pr = right aligned paragraph (deprecated: use pmr instead)
            //  ph# = indented paragraph with hanging indented (deprecated: use li# instead)
            //  lit = liturgical note. Guide to reader that he should recite a prayer (short, right-justified)
            //  q# = poetic line
            //  qr = right-justified poetic line
            //  qc = center-justified poetic line
            //  qa = acrostic heading
            //  qm# = embedded text poetic line
            //  b = blank line
            //
            //   2.x ==> 3.x
            //   mt#     mt#
            //   mte#    mte#
            //   ms#     ms#
            //   mr      mr
            //   s       s
            //   sr      sr
            //   r       r
            //   d       d
            //   sp      sp
            //   p       p
            //   m       m
            //   pmo     pmo
            //   pm      pm
            //   pmc     pmc
            //   pmr     pmr
            //   pi#     pi#
            //   mi      mi
            //   cls     cls
            //   li#     li#
            //   pc      pc
            //   pr      pr
            //   ph#     ph#
            //   lit     lit
            //   q#      q#
            //   qr      qr
            //   qc      qc
            //   qa      qa
            //   qm#     qm#
            //           qd
            //   b       b
            //           lh
            //           lf
            //           lim#
            //           litl
            //
            string[] listOfStyles = new string[]
                           {"mr", "sr", "r", "d", "sp",                   // titles and headings (non-numbered)
                            "mt", "mte", "ms",                            // titles and headings (numbered)
                           };

            SplitNumberedParaStyle(paraStyle, out string paraStyleWithoutNumber, out int numberThrowaway);

            if (Array.IndexOf(listOfStyles, paraStyleWithoutNumber) != -1)
                return true;

            return false;
        }

        private static void ScanParaInChapter()
        {
            XmlScanner.RetrieveAttributeValue("style", ref XmlHeap[XmlHeapIndex], out string style, out int styleThrowaway);
            string v3ParaStyle = style;
            if (IsParaStyleUnsupported(style))
                v3ParaStyle = "p";

            int paraLevel = XmlHeap[XmlHeapIndex].TreeLevel;
            int paraXmlIndex = XmlHeapIndex;

            int paraIndex = MakeNewChildNode(ElementEnum.PARA, CurrentUsxParent);
            UsxHeap[paraIndex].Style = v3ParaStyle;

            if (InTheMiddleOfAVerse)
            {
                UsxHeap[paraIndex].Vid = string.Format("{0} {1}:{2}", BookCode, CurrentChapterNumber, CurrentVerseNumber.ToString());
            }

            // Para element is new parent, for the time being
            CurrentUsxParent = paraIndex;

            bool checkToAddEndVerseTag = false;

            //bool paraHasChildren = XmlHeap[XmlHeapIndex].ChildrenCount > 0;
            // Given this example:
            //    <para style="p">
            //      <verse number="1" style="v" />The book of the generation of Jesus Christ, the son of David, the son of Abraham.</para>
            // The line feed before the verse tag will not be converted to a space.
            // This is a bit of a grey area.
            StepToNextXmlNode();
            int nextNodeLevel = XmlHeap[XmlHeapIndex].TreeLevel;

            while (nextNodeLevel > paraLevel)
            {
                XmlComponentEnum nextNodeType = XmlHeap[XmlHeapIndex].Type;

                bool nextIsElement = nextNodeType == XmlComponentEnum.ELEMENT;
                bool nextIsClosed = nextNodeType == XmlComponentEnum.CLOSED_ELEMENT;

                string nextElementName = "";
                if (nextIsElement || nextIsClosed)
                    nextElementName = XmlHeap[XmlHeapIndex].ElementName;

                bool nextIsVerseTag = nextIsClosed && nextElementName == "verse";
                bool nextIsCharElement = !nextIsClosed && nextElementName == "char";
                bool nextIsText = nextNodeType == XmlComponentEnum.TEXT;
                bool nextIsBreak = nextIsClosed && nextElementName == "optbreak";

                if (nextIsVerseTag)
                {
                    if (InTheMiddleOfAVerse)
                    {
                        // Close off previous verse: make verse-end node
                        int newChildIndex = MakeNewChildNode(ElementEnum.VERSE, CurrentUsxParent);
                        UsxHeap[newChildIndex].Eid = string.Format("{0} {1}:{2}", BookCode, CurrentChapterNumber, CurrentVerseNumber);

                        InTheMiddleOfAVerse = false;
                    }

                    // Do not simply increment verse number, as there may be gaps in the numbering
                    XmlScanner.RetrieveAttributeValue("number", ref XmlHeap[XmlHeapIndex],
                                     out string nextVerseNumberText, out int nextVerseNumberInt);
                    CurrentVerseNumber = nextVerseNumberInt;

                    StepToNextXmlNode();

                    // Create new verse-start node
                    int newVerseStartIndex = MakeNewChildNode(ElementEnum.VERSE, CurrentUsxParent);
                    UsxHeap[newVerseStartIndex].Sid = string.Format("{0} {1}:{2}", BookCode, CurrentChapterNumber, CurrentVerseNumber);
                    UsxHeap[newVerseStartIndex].Number = CurrentVerseNumber;

                    InTheMiddleOfAVerse = true;
                }
                else if (nextIsCharElement)
                {
                    XmlScanner.RetrieveAttributeValue("style", ref XmlHeap[XmlHeapIndex],
                                     out string nextStyleText, out int throwAway);
                    StepToNextXmlNode();

                    int newCharIndex = MakeNewChildNode(ElementEnum.CHAR, CurrentUsxParent);
                    UsxHeap[newCharIndex].Style = nextStyleText;

                    CurrentUsxParent = newCharIndex;
                }
                else if (nextIsText)
                {
                    string textString = XmlHeap[XmlHeapIndex].Text;
                    bool hasText = textString != null;

                    StepToNextXmlNode();

                    if (!hasText)
                    {
                        Utils.LogEntry("Sanity failed: Null text!");
                    }

                    int newTextIndex = MakeNewChildNode(ElementEnum.TEXT, CurrentUsxParent);
                    UsxHeap[newTextIndex].Text = textString!;
                }
                else if (nextIsBreak)
                {
                    StepToNextXmlNode();

                    MakeNewChildNode(ElementEnum.BREAK, CurrentUsxParent);
                }
                else
                {
                    Utils.LogEntry("Unrecognizable tag!");
                }

                // fixme: corner-case of line feeds after last text but before </para> not handled

                // Insert ending verse tag if we stepped back out of this para
                bool endOfFile = XmlHeapIndex == -1;
                bool steppedOutOfPara = XmlHeap[paraXmlIndex].NextSiblingHeapIndex == XmlHeapIndex;
                checkToAddEndVerseTag = endOfFile || steppedOutOfPara;

                // Finished?
                if (endOfFile)
                {
                    break;
                }

                int previousNodeLevel = nextNodeLevel;
                nextNodeLevel = XmlHeap[XmlHeapIndex].TreeLevel;
                int deltaLevels = previousNodeLevel - nextNodeLevel;

                // Did we step out of nested 'char' elements?
                // Then walk back parents in usx tree that many times
                // (Don't do if we finished para, as parent gets updated later)
                if (deltaLevels > 0)
                {
                    for (int i = 0; i < deltaLevels; i++)
                    {
                        CurrentUsxParent = UsxHeap[CurrentUsxParent].ParentHeapIndex;
                        if (CurrentUsxParent == -1)
                            Utils.LogFatal("ScanParaInChapter() Parent walk-back blew up!");
                    }
                }
            }

            if (checkToAddEndVerseTag)
            {
                // Now that the last element was walked through for this paragraph,
                // look ahead to the next paragraph, etc. to see if/when next verse starts.
                // Next verse start marks the end of this verse. If no text was
                // found to when next verse starts, then we're looking at spacing
                // paragraphs inbetween. End this verse now; don't include spacer paragraphs.
                if (InTheMiddleOfAVerse)
                {
                    bool fileEndingReached = XmlHeapIndex == -1;
                    bool nextVerseTagFound = false;
                    bool moreTextInThisVerse = false;
                    if (!fileEndingReached)
                        nextVerseTagFound = WalkXmlTreeUntilNextVerseTag(paraXmlIndex, out moreTextInThisVerse);

                    if (fileEndingReached || nextVerseTagFound || !moreTextInThisVerse)
                    {
                        // Close off previous verse: make verse-end node
                        int newVerseIndex = MakeNewChildNode(ElementEnum.VERSE, paraIndex);
                        UsxHeap[newVerseIndex].Eid = string.Format("{0} {1}:{2}", BookCode, CurrentChapterNumber, CurrentVerseNumber);

                        InTheMiddleOfAVerse = false;
                    }
                }

                // We're done with this para
                CloseOffThisNodesChildren(paraIndex);
            }

        }

        //private static void ScanCharStyle()
        //{
        //    //   2.x ==> 3.x
        //    //   qt      qt    quoted text
        //    //   rq      rq    inline quotation
        //    //   sig     sig   signature of apostle
        //    //   sls     sls   original text in secondary language
        //    //   tl      tl    transliterated words
        //    //   wj      wj    word of Jesus
        //    //   bd      bd    bold text
        //    //   bdit    bdit  bold + italics text
        //    //   em      em    emphasized text style
        //    //   it      it    italics
        //    //   no      no    normal text
        //    //   sc      sc    small caps
        //    //   pro     pro   pronounciation info
        //    //   w       w     wordlist/glossary/dictionary entry
        //    //   wg      wg    Greek word list entry
        //    //   wh      wh    Hebrew word list entry
        //    //           va" # Second (alternate) verse number (for coding dual numeration in Psalms; see also NRSV Exo 22.1-4)
        //    //           vp" # Published verse marker - this is a verse marking that would be used in the published text
        //    //           ca" # Second (alternate) chapter number
        //    //           qac" # Poetry text, Acrostic markup of the first character of a line of acrostic poetry
        //    //           qs" # Poetry text, Selah
        //    //           add" # For a translational addition to the text
        //    //           addpn" # For chinese words to be dot underline & underline (DEPRECATED - used nested char@style pn)
        //    //           bk" # For the quoted name of a book
        //    //           dc" # Deuterocanonical/LXX additions or insertions in the Protocanonical text
        //    //           efm" # Reference to caller of previous footnote in a study Bible
        //    //           fm" # Reference to caller of previous footnote
        //    //           k" # For a keyword
        //    //           nd" # For name of deity
        //    //           ndx" # A subject index text item
        //    //           ord" # For the text portion of an ordinal number
        //    //           pn" # For a proper name
        //    //           png" # For a geographic proper name
        //    //           pro" # For indicating pronunciation in CJK texts (DEPRECATED - used char@style rb)
        //    //           qt" # For Old Testament quoted text appearing in the New Testament
        //    //           rq" # A cross-reference indicating the source text for the preceding quotation.
        //    //           sig" # For the signature of the author of an Epistle
        //    //           sls" # To represent where the original text is in a secondary language or from an alternate text source
        //    //           tl" # For transliterated words
        //    //           wg" # A Greek Wordlist text item
        //    //           wh" # A Hebrew wordlist text item
        //    //           wa" # An Aramaic wordlist text item
        //    //           wj" # For marking the words of Jesus
        //    //           xt" # A target reference(s)
        //    //           jmp" # For associating linking attributes to a span of text
        //    //           no" # A character style, use normal text
        //    //           it" # A character style, use italic text
        //    //           bd" # A character style, use bold text
        //    //           bdit" # A character style, use bold + italic text
        //    //           em" # A character style, use emphasized text style
        //    //           sc" # A character style, for small capitalization text
        //    //           sup" # A character style, for superscript text. Typically for use in critical edition footnotes.
        //}

        private static bool IsANoteLikeElement(int thisHeapIndex)
        {
            string[] noteLikeElements = new string[]
                { "note", "figure", "ref", "table", "sidebar" };

            string elementName = XmlHeap[thisHeapIndex].ElementName;
            bool isOne = Array.IndexOf(noteLikeElements, elementName) != -1;
            return isOne;
        }


        // Do a look-ahead walk of xml tree down and to the right until you find
        // the next "verse" tag, indicating the start of the next verse.
        // If you find a next verse tag, return 'true'.
        //
        // Begin the walk at 'startIndex'
        //
        // If any text is found before reaching the next verse tag (or end of chapter),
        // set 'anyTextInBetweenHereAndNextVerseStart'.
        //
        // 'chapterIsAtThisLevel' is the tree level of the "chapter" element.
        // If in the walk you reach this level, you finished the chapter.
        //
        // fixme: this neglects to convert linefeeds to space chars.
        //        Do we even need to?
        private static bool WalkXmlTreeUntilNextVerseTag(int startIndex,
                                       out bool anyTextInBetweenHereAndNextVerseStart)
        {
            anyTextInBetweenHereAndNextVerseStart = false;

            int startLevel = XmlHeap[startIndex].TreeLevel;

            int walkIndex = startIndex;

            while (true)
            {
                int firstChildIndex = -1;
                if (XmlHeap[walkIndex].ChildrenCount > 0)
                    firstChildIndex = XmlHeap[walkIndex].Children[0];
                int nextSiblingIndex = XmlHeap[walkIndex].NextSiblingHeapIndex;
                int parentIndex = XmlHeap[walkIndex].ParentHeapIndex;
                bool isFootnoteEtc = IsANoteLikeElement(walkIndex);

                // If we ran across a footnote, then we've reached the
                // end of the chapter as far as verses are concerned.
                if (isFootnoteEtc)
                {
                    return false;
                }
                // Prefer stepping down a level to children rather than to the right to sibling
                else if (firstChildIndex != -1)
                {
                    walkIndex = firstChildIndex;

                    bool isVerseIndex = XmlHeap[walkIndex].Type == XmlComponentEnum.CLOSED_ELEMENT &&
                                        XmlHeap[walkIndex].ElementName == "verse";
                    if (isVerseIndex)
                    {
                        return true;
                    }

                    bool isText = XmlHeap[walkIndex].Type == XmlComponentEnum.TEXT &&
                                  XmlHeap[walkIndex].Text != null && XmlHeap[walkIndex].Text.Length > 0;
                    anyTextInBetweenHereAndNextVerseStart |= isText;
                }
                // Is there a branch to the right (a next-sibling)?
                else if (nextSiblingIndex != -1)
                {
                    walkIndex = nextSiblingIndex;

                    bool isVerseIndex = XmlHeap[walkIndex].Type == XmlComponentEnum.CLOSED_ELEMENT &&
                                        XmlHeap[walkIndex].ElementName == "verse";
                    if (isVerseIndex)
                    {
                        return true;
                    }

                    bool isText = XmlHeap[walkIndex].Type == XmlComponentEnum.TEXT &&
                                  XmlHeap[walkIndex].Text != null && XmlHeap[walkIndex].Text.Length > 0;
                    anyTextInBetweenHereAndNextVerseStart |= isText;
                }
                // Otherwise, go up the tree
                else
                {
                    walkIndex = parentIndex;

                    while (true)
                    {
                        int parentsSiblingIndex = XmlHeap[walkIndex].NextSiblingHeapIndex;
                        int parentsLevel = XmlHeap[walkIndex].TreeLevel;

                        if (parentsLevel <= ChapterLevel)
                        {
                            return false;
                        }
                        else if (XmlHeap[walkIndex].ElementName == "chapter")
                        {
                            // fixme: this must be caught in above if-condition
                            return false;
                        }
                        else if (parentsSiblingIndex != -1)
                        {
                            walkIndex = parentsSiblingIndex;
                            parentIndex = XmlHeap[walkIndex].ParentHeapIndex;
                            break;
                        }

                        // go up to next parent
                        walkIndex = XmlHeap[walkIndex].ParentHeapIndex;
                        parentIndex = XmlHeap[walkIndex].ParentHeapIndex;
                    }

                    bool finishedChapter = XmlHeap[walkIndex].TreeLevel >= ChapterLevel;
                    if (finishedChapter)
                    {
                        return false;
                    }

                    // probably don't need this, but just in case...
                    bool isText = XmlHeap[walkIndex].Type == XmlComponentEnum.TEXT &&
                                  XmlHeap[walkIndex].Text != null && XmlHeap[walkIndex].Text.Length > 0;
                    anyTextInBetweenHereAndNextVerseStart |= isText;
                }
            }
        }


        private enum XmlNodeStepMode
        {
             REGULAR,
             SKIP_INNER_TEXT_COMPONENT,
        }
        // Point to next item in Xml tree.
        // Next item is next sibling. If no more siblings, then next item
        //   is sibling of parent. Recurse upwards until you find a
        //   next sibling. If no next sibling can be found, parse is done.
        //
        // Returns number of line feeds consumed
        private static void StepToNextXmlNode(XmlNodeStepMode mode = XmlNodeStepMode.REGULAR)
        {
            bool skipInner = (mode == XmlNodeStepMode.SKIP_INNER_TEXT_COMPONENT);
                           

            // Step down into the first child
            // Decide whether to keep it or move on
            if (XmlHeap[XmlHeapIndex].ChildrenCount > 0)
            {
                bool parentHasSingleChild = XmlHeap[XmlHeapIndex].ChildrenCount == 1;
                int childIndex = XmlHeap[XmlHeapIndex].Children[0];

                bool childIsText = XmlHeap[childIndex].Type == XmlComponentEnum.TEXT;
                // If there's 1 child and that child is a text item,
                //  caller might've absorped it already
                bool skipInnerSingleChildText = skipInner && parentHasSingleChild && childIsText;

                XmlHeapIndex = childIndex;

                if (!skipInnerSingleChildText)
                {
                    return;
                }
            }


            while (true)
            {
                int siblingIndex = XmlHeap[XmlHeapIndex].NextSiblingHeapIndex;

                // Step to the next sibling?
                if (siblingIndex != -1)
                {
                    XmlHeapIndex = siblingIndex;

                    return;
                }
                // Return to parent
                else
                {
                    XmlHeapIndex = XmlHeap[XmlHeapIndex].ParentHeapIndex;

                    // Finished walking tree?
                    if (XmlHeapIndex == 0)
                    {
                        XmlHeapIndex = -1;
                        return ;
                    }
                }
            }
        }

        // Step in xml tree, a step which ignores children
        private static void BlowByThisNodesChildren()
        {
            while (true)
            {
                int siblingIndex = XmlHeap[XmlHeapIndex].NextSiblingHeapIndex;
                if (siblingIndex != -1)
                {
                    XmlHeapIndex = siblingIndex;
                    return;
                }
                // Otherwise, return to parent
                else
                {
                    XmlHeapIndex = XmlHeap[XmlHeapIndex].ParentHeapIndex;
                }
            }
        }

        private static int MakeRootNode()
        {
            int newNodeIndex = UsxHeapSize;

            TreeObject newObject = new TreeObject
            {
                Level = 0,
                ParentHeapIndex = -1,
                PreviousSiblingHeapIndex = -1,
                NextSiblingHeapIndex = -1,
                ChildrenHeapIndices = new int[100000],
                ChildrenCount = 0,

                ElementType = ElementEnum.USX_WRAPPER,

            };
            UsxHeap[UsxHeapSize++] = newObject;

            return newNodeIndex;
        }

        private static int MakeNewChildNode(ElementEnum type, int parentIndex)
        {
            int newNodeIndex = UsxHeapSize;

            int[] childrenHeap;
            if (type == ElementEnum.CHAPTER || type == ElementEnum.VERSE || type == ElementEnum.TEXT ||
                type == ElementEnum.BREAK)
                childrenHeap = new int[0];
            else
                childrenHeap = new int[1000];

            TreeObject newObject = new TreeObject
            {
                Level = UsxHeap[parentIndex].Level + 1,
                ParentHeapIndex = parentIndex,
                PreviousSiblingHeapIndex = -1,
                NextSiblingHeapIndex = -1,
                ChildrenHeapIndices = childrenHeap,
                ChildrenCount = 0,

                ElementType = type,

                Text = "",

            };
            UsxHeap[UsxHeapSize++] = newObject;

            // Update links: sibling, parent
            int currentChildCount = UsxHeap[parentIndex].ChildrenCount;
            if (currentChildCount > 0)
            {
                int previousSiblingIndex = UsxHeap[parentIndex].ChildrenHeapIndices[currentChildCount - 1];
                UsxHeap[previousSiblingIndex].NextSiblingHeapIndex = newNodeIndex;
                UsxHeap[newNodeIndex].PreviousSiblingHeapIndex = previousSiblingIndex;
            }

            UsxHeap[parentIndex].ChildrenHeapIndices[currentChildCount] = newNodeIndex;
            UsxHeap[parentIndex].ChildrenCount++;

            return newNodeIndex;
        }

        // Call this after last child added to this node
        private static void CloseOffThisNodesChildren(int nodeIndex)
        {
            Array.Resize(ref UsxHeap[nodeIndex].ChildrenHeapIndices, UsxHeap[nodeIndex].ChildrenCount);
        }

    }
}
