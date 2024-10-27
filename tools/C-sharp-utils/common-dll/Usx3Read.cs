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
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Schema;

namespace common_dll
{
    public static partial class SusxConverter
    {
        public static bool? PRINT_DEBUG_FILES;
        public static string? DEBUG_PRINT_USX_FOLDER;

        // These are defined over in ToUsx3.cs
        //private static string BookCode;
        //private static int CurrentChapterNumber;
        //private static int ChapterLevel;

        //private static bool InTheMiddleOfAVerse;
        //private static int CurrentVerseNumber;

        private static int CharRecursionCount = 0;
        private static int NoteRecursionCount = 0;

#pragma warning disable CS8618
        // Since USX splits each Bible book into separate files,
        //  these get reused for each file/each book of the Bible
        private static XmlComponent[] XmlHeap;
        private static int XmlHeapIndex;
        private static TreeObject[] UsxHeap;
        private static int UsxHeapSize;
        private static int CurrentUsxParent;
#pragma warning restore


        // A collection of usx files exist in '{$BASE_DIR}/readSubFolderPath'
        // Read them and put them in 'allUsxBooks'.
        // 'allBookEnums' is 1-to-1 with 'allUsxBooks'.
        public static void ReadBible(string readDirectory,
                                     out TreeObject[][] allUsxBooks,
                                     out BookEnum[] allBookEnums)
        {

            bool noFileErrors = LoadBookNames(readDirectory,
                                              out string[] allUsxFileNames,
                                              out allBookEnums,
                                              out string fileErrorText);
            if (!noFileErrors)
                Utils.LogFatal(fileErrorText);

            int numberOfBooks = allUsxFileNames.Length;

            allUsxBooks = new TreeObject[numberOfBooks][];

            for (int i = 0; i < numberOfBooks; i++)
            {
                string thisFileName = allUsxFileNames[i];
                BookEnum thisEnum = allBookEnums[i];

                Utils.ConcatenateFqFileName(thisFileName, readDirectory, out string fqFileName);

                ReadBook(thisEnum, thisFileName, fqFileName, out allUsxBooks[i]);
            }

        }

        // Analyze '{$BASE_DIR}/readSubFolderPath' for usx-styled file names
        // and put those names in 'allFileNamesOfBooks'.
        // 'allEnumsOfBooks' is 1-to-1 with 'allFileNamesOfBooks', indicating
        // which the book name for the file.
        //
        // All the files in the folder are scanned, any file not matching
        // the well-known file
        // 'allFileNamesOfBooks'/'allEnumsOfBooks' get sorted according
        //
        // Returns 'false' if operation failed/an error was encountered and puts
        // error in 'errorText'
        public static bool LoadBookNames(string readFolder,
                                         out string[] allFileNamesOfBooks,
                                         out BookEnum[] allEnumsOfBooks,
                                         out string errorText)
        {
            allFileNamesOfBooks = new string[0];
            allEnumsOfBooks = new BookEnum[0];
            errorText = "";

            // Build file names. Do folder/path sanity checks
            if (!Path.Exists(readFolder))
            {
                errorText = string.Format("{0} doesn't exist!", readFolder);
                return false;
            }

            string[] fqNamesOfallFilesInFolder = Directory.GetFiles(readFolder);
            if (fqNamesOfallFilesInFolder == null || fqNamesOfallFilesInFolder.Length == 0)
            {
                errorText = string.Format("{0} has no files in it!", readFolder);
                return false;
            }


            // Establish comprehensive usx file name list.
            // 'UsxDefinitions.BibleBookNamesText' is already in order.
            UsxDefinitions.LoadBibleBookNames();
            string[] usxFileNamesLowerCase = new string[UsxDefinitions.BibleBookNamesText.Length];
            for (int i = 0; i < usxFileNamesLowerCase.Length; i++)
            {
                usxFileNamesLowerCase[i] = string.Format("{0}.usx", UsxDefinitions.BibleBookNamesText[i].ToLower());
            }

            // 'usxFileNamesLowerCaseSortedList' is sorted equivalent of 'usxFileNamesLowerCase'
            //   --Sorted alpha-numerically, that is.
            // This speeds up file matches.
            // key=lower-case well-known usx book name; value=(int)BookEnum
            SortedList<string, int> usxFileNamesLowerCaseSortedList = new SortedList<string, int>(usxFileNamesLowerCase.Length);
            for (int i = 0; i < usxFileNamesLowerCase.Length; i++)
            {
                usxFileNamesLowerCaseSortedList.Add(usxFileNamesLowerCase[i], i);
            }

            // Build 'fileNamesOfAllFilesInFolder' (list of all file names in folder)
            // Count the number which are usx files.
            // Save off the usx files and their enums

            int[] bookEnumIntUnsorted = new int[fqNamesOfallFilesInFolder.Length];
            string[] usxFileNamesUnsorted = new string[fqNamesOfallFilesInFolder.Length];

            int usxBookCount = 0;
            string[] fileNamesOfAllFilesInFolder = new string[fqNamesOfallFilesInFolder.Length];
            for (int i = 0; i < fileNamesOfAllFilesInFolder.Length; i++)
            {
                // 'thisFileName' is file name + extension but no folder/path/directory name
                string thisFileName = Path.GetFileName(fqNamesOfallFilesInFolder[i]);
                fileNamesOfAllFilesInFolder[i] = thisFileName;
                string thisFileNameLowerCase = thisFileName.ToLower();

                // Is 'thisFileNameLowerCase' a usx-approved file name for a bible book?
                // NOTE: we're not enforcing case sensitivity
                //       ex:  can use 1ch.usx in place of 1CH.usx
                if (usxFileNamesLowerCaseSortedList.TryGetValue(thisFileNameLowerCase, out int thisBookEnumInt))
                {
                    bookEnumIntUnsorted[usxBookCount] = thisBookEnumInt;
                    usxFileNamesUnsorted[usxBookCount] = thisFileName;    // lifting the restriction that file name prefix must be upper case

                    usxBookCount++;
                }
            }

            if (usxBookCount == 0)
            {
                errorText = string.Format("{0} has files, but none are usx files!", readFolder);
                return false;
            }

            Array.Resize(ref bookEnumIntUnsorted, usxBookCount);
            Array.Resize(ref usxFileNamesUnsorted, usxBookCount);

            // This "redirection" list is a sorting object which maps an
            // unsorted list to the order according to enum-int.

            // key=book enum-int; value=index in 'bookEnumIntUnsorted'
            SortedList<int, int> sortRedirection = new SortedList<int, int>(usxBookCount);
            for (int i = 0; i < usxBookCount; i++)
            {
                // 'sortRedirection' sorts 'bookEnumIntUnsorted' entries
                sortRedirection.Add(bookEnumIntUnsorted[i], i);
            }

            allFileNamesOfBooks = new string[usxBookCount];
            allEnumsOfBooks = new BookEnum[usxBookCount];

            // We can now build the outputs 'allFileNamesOfBooks', 'allEnumsOfBooks',
            //  which are sorted equivalents of 'usxFileNamesUnsorted', 'bookEnumIntUnsorted'
            for (int i = 0; i < usxBookCount; i++)
            {
                //int thisEnumInt = sortRedirection.GetKeyAtIndex(i);
                int unsortedListIndex = sortRedirection.GetValueAtIndex(i);

                allEnumsOfBooks[i] = (BookEnum)bookEnumIntUnsorted[unsortedListIndex];
                allFileNamesOfBooks[i] = usxFileNamesUnsorted[unsortedListIndex];
            }

            return true;
        }

        public static void ReadBook(BookEnum bookEnum, string fileName, string fqFileName, out TreeObject[] treeObjects)
        {
            XmlScanner.ScanFile(fileName, fqFileName, out string singleStringXmlFile, out XmlHeap);
            XmlHeapIndex = 0;

            SusxConverter.SuperficialScanUsxSanity(ref XmlHeap, out int majorUsxVersionNumber);
            if (majorUsxVersionNumber != 3)
                Utils.LogFatal("{0}/{1} is not a USX 3.x file!", bookEnum.ToString(), fileName);

            UsxHeap = new TreeObject[XmlHeap.Length * 2];
            UsxHeapSize = 0;

            MakeRootNode();

            CurrentUsxParent = 0;
            XmlHeapIndex = XmlHeap[0].Children[0];

            // <book code="1CH" style="id">- American Standard Version</book>
            XmlScanner.RetrieveAttributeValue("code", ref XmlHeap[XmlHeapIndex], out BookCode, out int codeThrowAway);
            XmlScanner.RetrieveTextComponentAsContent(ref XmlHeap, XmlHeapIndex, out string title);
            int bookElementIndex = MakeNewChildNode(ElementEnum.BOOK, CurrentUsxParent);
            UsxHeap[bookElementIndex].Code = BookCode;
            UsxHeap[bookElementIndex].Style = "id";
            UsxHeap[bookElementIndex].Text = title;
            CloseOffThisNodesChildren(bookElementIndex);

            StepToNextXmlNode(XmlNodeStepMode.SKIP_INNER_TEXT_COMPONENT);

            ScanIntroductoryParagraphs();

            bool isChapter = XmlHeapIndex != -1 && XmlHeap[XmlHeapIndex].ElementName == "chapter";
            bool hasChapterSid = false;
            string chapterSid = "";
            if (isChapter)
                hasChapterSid = XmlScanner.RetrieveAttributeValue("sid", ref XmlHeap[XmlHeapIndex], out chapterSid, out int chapterSidThrowAway);

            while (isChapter && hasChapterSid)
            {
                ScanChapter();

                isChapter = XmlHeapIndex != -1 && XmlHeap[XmlHeapIndex].ElementName == "chapter";
                hasChapterSid = false;
                if (isChapter)
                    hasChapterSid = XmlScanner.RetrieveAttributeValue("sid", ref XmlHeap[XmlHeapIndex], out chapterSid, out int chapterSidThrowAway);
            }

            Array.Resize(ref UsxHeap, UsxHeapSize);

            // trim all children arrays
            for (int i = 0; i < UsxHeap.Length; i++)
            {
                Array.Resize(ref UsxHeap[i].ChildrenHeapIndices, UsxHeap[i].ChildrenCount);
            }

            treeObjects = UsxHeap;

#pragma warning disable CS8604, CS8629
            if (PRINT_DEBUG_FILES.Value)
            {
                PrintBookInUsxFormat(BookCode, DEBUG_PRINT_USX_FOLDER);
            }
#pragma warning restore
        }

        // Sanity check/debug utility
        // Writes 'UsxHeap' back to a file
        public static void PrintBookInUsxFormat(string bookCode, string subFolder)
        {
            string[] copyrightText = new string[]
            {
                "Usx3Read.cs debug output file"
            };
            string translationName = "debug";

            WriteBook(bookCode, ref UsxHeap, subFolder, copyrightText, translationName);
        }

        private static void ScanIntroductoryParagraphs()
        {
            int xmlParaIndex = XmlHeapIndex;

            bool isPara = RetrieveParaInfo(XmlHeapIndex, out string paraStyle,
                                   out bool isIntroductoryPara, out string paraText);

            while (isIntroductoryPara)
            {
                ScanPara();

                isPara = RetrieveParaInfo(XmlHeapIndex, out paraStyle,
                                         out isIntroductoryPara, out paraText);
            }

        }


        private static void ScanChapter()
        {
            bool ok1 = XmlScanner.RetrieveAttributeValue("number", ref XmlHeap[XmlHeapIndex], out string throwAway3, out int chapterNumber);
            bool ok2 = XmlScanner.RetrieveAttributeValue("style", ref XmlHeap[XmlHeapIndex], out string chapterStyle, out int throwAway1);
            bool ok3 = XmlScanner.RetrieveAttributeValue("sid", ref XmlHeap[XmlHeapIndex], out string chapterSid, out int throwAway2);
            // altnumber, pubnumber not supported
            bool isChapterStart = XmlHeap[XmlHeapIndex].ElementName == "chapter" &&
                           ok1 && ok2 && ok3 && chapterSid.Length > 0; 
            if (!isChapterStart)
                Utils.LogFatal("XML index {0}: expected chapter element!", XmlHeapIndex);

            int usxStartChapterIndex = MakeNewChildNode(ElementEnum.CHAPTER, CurrentUsxParent);
            UsxHeap[usxStartChapterIndex].Number = chapterNumber;
            UsxHeap[usxStartChapterIndex].Sid = chapterSid;
            UsxHeap[usxStartChapterIndex].Style = chapterStyle;

            CurrentChapterNumber = chapterNumber;

            StepToNextXmlNode();

            bool nextIsChapter = XmlHeap[XmlHeapIndex].ElementName == "chapter";
            bool isEndChapterEid = XmlScanner.RetrieveAttributeValue("eid", ref XmlHeap[XmlHeapIndex], out string chapterEid, out int throwAway4);
            bool isChapterEndMilestone = nextIsChapter && isEndChapterEid;

            while (!isChapterEndMilestone)
            {
                string xmlElementName = XmlHeap[XmlHeapIndex].ElementName;

                if (xmlElementName == "para")
                {
                    ScanPara();
                }
                else if (xmlElementName == "note")
                {
                    ScanNote();
                }
                else
                {
                    // Not supported
                    BlowByThisNodesChildren();
                }

                nextIsChapter = XmlHeap[XmlHeapIndex].ElementName == "chapter";
                isEndChapterEid = XmlScanner.RetrieveAttributeValue("eid", ref XmlHeap[XmlHeapIndex], out chapterEid, out throwAway4);
                isChapterEndMilestone = nextIsChapter && isEndChapterEid;
            }

            int usxEndChapterIndex = MakeNewChildNode(ElementEnum.CHAPTER, CurrentUsxParent);
            UsxHeap[usxEndChapterIndex].Number = chapterNumber;
            UsxHeap[usxEndChapterIndex].Eid = chapterEid;
            UsxHeap[usxEndChapterIndex].Style = chapterStyle;

            StepToNextXmlNode();
        }

        private static void ScanPara()
        {
            bool isPara = RetrieveParaInfo(XmlHeapIndex, out string paraStyle,
                               out bool isIntroductoryPara, out string paraText);
            if (!isPara)
                Utils.LogFatal("XML index {0}: expected para element!", XmlHeapIndex);

            int usxParaIndex = MakeNewChildNode(ElementEnum.PARA, CurrentUsxParent);
            UsxHeap[usxParaIndex].Style = paraStyle;
            if (isIntroductoryPara)
                UsxHeap[usxParaIndex].Text = paraText;

            int originalUsxParent = CurrentUsxParent;
            CurrentUsxParent = usxParaIndex;

            int nextXmlSibling = XmlHeap[XmlHeapIndex].NextSiblingHeapIndex;
            //int xmlParent = XmlHeap[XmlHeapIndex].ParentHeapIndex;
            int paraLevel = XmlHeap[XmlHeapIndex].TreeLevel;

            if (isIntroductoryPara)
                StepToNextXmlNode(XmlNodeStepMode.SKIP_INNER_TEXT_COMPONENT);
            else
                StepToNextXmlNode();

            bool skipNextXmlStep = false;

            while (XmlHeapIndex != nextXmlSibling &&
                   XmlHeap[XmlHeapIndex].TreeLevel > paraLevel)
            {
                XmlComponentEnum componentEnum = XmlHeap[XmlHeapIndex].Type;
                string elementName = XmlHeap[XmlHeapIndex].ElementName;

                if (elementName == "char")
                {
                    // ScanChar returns pointing to next xml element
                    ScanChar();

                    skipNextXmlStep = true;
                }
                else if (elementName == "verse")
                {
                    bool hasSid = XmlScanner.RetrieveAttributeValue("sid", ref XmlHeap[XmlHeapIndex], out string sidText, out int sidThrowAway);
                    bool hasEid = XmlScanner.RetrieveAttributeValue("eid", ref XmlHeap[XmlHeapIndex], out string eidText, out int eidThrowAway);
                    if (!hasSid && !hasEid)
                        Utils.LogFatal("ScanPara() {0} {1}:{2} verse element has neither sid nor eid",
                              BookCode, CurrentChapterNumber, CurrentVerseNumber);

                    int usxVerseIndex = MakeNewChildNode(ElementEnum.VERSE, CurrentUsxParent);
                    if (hasSid)
                    {
                        UsxHeap[usxVerseIndex].Sid = sidText;
                        bool hasVerseNumber = XmlScanner.RetrieveAttributeValue("number", ref XmlHeap[XmlHeapIndex],
                                               out string numberText, out int numberValue);
                        UsxHeap[usxVerseIndex].Number = numberValue;
                    }
                    if (hasEid)
                        UsxHeap[usxVerseIndex].Eid = eidText;

                }
                else if (componentEnum == XmlComponentEnum.TEXT)
                {
                    int usxTextIndex = MakeNewChildNode(ElementEnum.TEXT, CurrentUsxParent);
                    UsxHeap[usxTextIndex].Text = "";
                    if (XmlHeap[XmlHeapIndex].Text != null && XmlHeap[XmlHeapIndex].Text.Length > 0) 
                        UsxHeap[usxTextIndex].Text = XmlHeap[XmlHeapIndex].Text;
                }
                else
                {
                    break;
                }

                if (!skipNextXmlStep)
                    StepToNextXmlNode();
                skipNextXmlStep = false;
            }

            CurrentUsxParent = originalUsxParent;
        }


        private static void ScanChar()
        {
            CharRecursionCount++;

            if (CharRecursionCount > 10 || CharRecursionCount < 0)
                Utils.LogFatal("ScanChar() Stuck in infinite recursive cycle!");

            bool isChar = XmlHeap[XmlHeapIndex].ElementName == "char";
            if (!isChar)
                Utils.LogFatal("XML index {0}: expected char element!", XmlHeapIndex);

            int usxCharIndex = MakeNewChildNode(ElementEnum.CHAR, CurrentUsxParent);
            XmlScanner.RetrieveAttributeValue("style", ref XmlHeap[XmlHeapIndex],
                              out UsxHeap[usxCharIndex].Style, out int charStyleThrowaway);

            int nextXmlSibling = XmlHeap[XmlHeapIndex].NextSiblingHeapIndex;
            int charLevel = XmlHeap[XmlHeapIndex].TreeLevel;

            StepToNextXmlNode();

            int originalUsxParent = CurrentUsxParent;
            CurrentUsxParent = usxCharIndex;

            while (XmlHeapIndex != nextXmlSibling &&
                   XmlHeap[XmlHeapIndex].TreeLevel > charLevel)
            {
                XmlComponentEnum componentEnum = XmlHeap[XmlHeapIndex].Type;
                string elementName = XmlHeap[XmlHeapIndex].ElementName;

                if (elementName == "char")
                {
                    ScanChar();
                }
                // milestone
                else if (elementName == "ms")
                {
                    // Not supported; quietly discard...
                    //   ...And hope there're no repurcussions
                    BlowByThisNodesChildren();
                }
                else if (elementName == "ref")
                {
                    // Not supported; quietly discard
                    BlowByThisNodesChildren();
                }
                else if (elementName == "note")
                {
                    ScanNote();
                }
                else if (componentEnum == XmlComponentEnum.TEXT)
                {
                    int usxTextIndex = MakeNewChildNode(ElementEnum.TEXT, CurrentUsxParent);
                    UsxHeap[usxTextIndex].Text = "";
                    if (XmlHeap[XmlHeapIndex].Text != null && XmlHeap[XmlHeapIndex].Text.Length > 0)
                        UsxHeap[usxTextIndex].Text = XmlHeap[XmlHeapIndex].Text;

                    StepToNextXmlNode();
                }
                else if (elementName == "optbreak")
                {
                    MakeNewChildNode(ElementEnum.BREAK, CurrentUsxParent);

                    StepToNextXmlNode();
                }
                else
                {
                    Utils.LogEntry("{0} {1}:{2} Unsupported element {3} under char", BibleBooks.AllBooksFullNames[(int)ZBookEnum], CurrentChapterNumber, CurrentVerseNumber, elementName);
                    break;
                }

            }

            CurrentUsxParent = originalUsxParent;

            CharRecursionCount--;
        }

        private static void ScanNote()
        {
            NoteRecursionCount++;

            if (NoteRecursionCount > 10 || NoteRecursionCount < 0)
                Utils.LogFatal("ScanNote() Stuck in infinite recursive cycle!");

            bool isNote = XmlHeap[XmlHeapIndex].ElementName == "note";
            if (!isNote)
                Utils.LogFatal("XML index {0}: expected note element!", XmlHeapIndex);

            int usxNoteIndex = MakeNewChildNode(ElementEnum.NOTE, CurrentUsxParent);
            XmlScanner.RetrieveAttributeValue("style", ref XmlHeap[XmlHeapIndex],
                              out UsxHeap[usxNoteIndex].Style, out int noteStyleThrowaway);
            XmlScanner.RetrieveAttributeValue("caller", ref XmlHeap[XmlHeapIndex],
                              out UsxHeap[usxNoteIndex].Caller, out int noteCallerThrowaway);

            int nextXmlSibling = XmlHeap[XmlHeapIndex].NextSiblingHeapIndex;
            int noteLevel = XmlHeap[XmlHeapIndex].TreeLevel;

            StepToNextXmlNode();

            int originalUsxParent = CurrentUsxParent;
            CurrentUsxParent = usxNoteIndex;

            while (XmlHeapIndex != nextXmlSibling &&
                   XmlHeap[XmlHeapIndex].TreeLevel > noteLevel)
            {
                //XmlComponentEnum componentEnum = XmlHeap[XmlHeapIndex].Type;
                string elementName = XmlHeap[XmlHeapIndex].ElementName;

                if (elementName == "char")
                {
                    ScanChar();
                }
                else if (elementName == "ref")
                {
                    // Not supported; quietly discard
                    BlowByThisNodesChildren();
                }
                else if (elementName == "text")
                {
                    int usxTextIndex = MakeNewChildNode(ElementEnum.TEXT, CurrentUsxParent);
                    UsxHeap[usxTextIndex].Text = "";
                    if (XmlHeap[XmlHeapIndex].Text != null && XmlHeap[XmlHeapIndex].Text.Length > 0)
                        UsxHeap[usxTextIndex].Text = XmlHeap[XmlHeapIndex].Text;

                    StepToNextXmlNode();
                }
                else if (elementName == "optbreak")
                {
                    MakeNewChildNode(ElementEnum.BREAK, CurrentUsxParent);

                    StepToNextXmlNode();
                }
                else
                {
                    Utils.LogEntry("{0} {1}:{2} Unsupported element {3} under note", BibleBooks.AllBooksFullNames[(int)ZBookEnum], CurrentChapterNumber, CurrentVerseNumber, elementName);
                    break;
                }

            }

            CurrentUsxParent = originalUsxParent;

            NoteRecursionCount--;
        }

        // Returns 'true' if this is a para and gets "style" attrib
        // Populate 'introParagraphText' if appropriate/if this is an introductory paragraph
        private static bool RetrieveParaInfo(int xmlIndex, out string style,
                                             out bool isIntroductoryParagraph, out string introductoryParagraphText)
        {
            bool hasStyle = XmlScanner.RetrieveAttributeValue("style", ref XmlHeap[xmlIndex], out style, out int paraStyleThrowaway);

            // Can have closed elements also as paragraphs:
            //     <para style="pi" />    <!-- Spacer line. No verse associated with it. -->
            bool isOpenOrClosedElement = XmlHeap[xmlIndex].Type == XmlComponentEnum.ELEMENT ||
                                         XmlHeap[xmlIndex].Type == XmlComponentEnum.CLOSED_ELEMENT;
            bool isPara = isOpenOrClosedElement &&
                          hasStyle && XmlHeap[xmlIndex].ElementName == "para";

            introductoryParagraphText = "";

            bool isheaderStyle = IsBookHeaderParaStyle(style);
            bool isTitleStyle = IsBookTitleParaStyle(style);
            bool isIntroStyle = IsBookIntroductoryStyle(style);
            isIntroductoryParagraph = isheaderStyle || isTitleStyle || isIntroStyle;
            
            if (isIntroductoryParagraph)
            {
                XmlScanner.RetrieveTextComponentAsContent(ref XmlHeap, xmlIndex, out introductoryParagraphText);
            }

            return hasStyle && isPara;
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
                        return;
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