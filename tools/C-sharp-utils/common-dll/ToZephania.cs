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
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
//using static common_dll.SusxConverter;

namespace common_dll
{
    public static partial class SusxConverter
    {
        //******   ToZephania settings

        // folder containing usx files to be converted to Zephania
        public static string? TOZ_USX_SOURCE_FOLDER;

        public static string? TOZ_OUTPUT_FOLDER;

        // </INFORMATION> fill-ins
        public static string? TOZ_BIBLE_NAME;
        public static string? TOZ_BIBLE_ABBREV;
        public static string? TOZ_CREATOR;
        public static string? TOZ_PUBLISHER;
        public static string? TOZ_LANGUAGE_ABBREV;

        // Some USX formatting encapsulates every verse in its own paragraph, rather
        // than encapuslating multiple verses in a single paragraph, according to the
        // way a modern translation is read. To make the output more Zephania-friendly,
        // suppressing paragraph breaks is desirable.
        //
        // So this input:
        //
        //<usx version="3.0">
        //  <book code="MAT" style="id">KJV</book>
        //  <!-- Titles and Table of Contents -->
        //  <para style="mt">Matthew</para>
        //  <!-- (no introduction) -->
        //  <!-- Chapter 1 -->
        //  <chapter number="1" style="c" sid="MAT 1" />
        //    <para style="p"><verse number="1" style="v" sid="MAT 1:1" />The book of the generation
        //      of Jesus Christ, the son of David, the son of Abraham.<verse eid="MAT 1:1"
        //      /></para>
        //    <para style="p"><verse number="2" style="v" sid="MAT 1:2" />Abraham begat Isaac;
        //      and Isaac begat Jacob; and Jacob begat Judah and his brethren;<verse eid="MAT 1:2"
        //      /></para>
        //    <para style="p"><verse number="3" style="v" sid="MAT 1:3" />And Judah begat Phares
        //      and Zara of Thamar; and Phares begat Esrom; and Esrom begat Aram;<verse eid="MAT 1:3"
        //      /></para>
        //
        // Without TOZ_SUPPRESS_PARA_BREAKS == false:
        //
        //   <CHAPTER cnumber="1">
        //      <VERS vnumber="1">The book of the generation of Jesus Christ, the son of David, the son of Abraham.</VERS>
        //      <VERS vnumber="2"><BR art="x-p"/>Abraham begat Isaac; and Isaac begat Jacob; and Jacob begat Judah
        //        and his brethren;</VERS>
        //      <VERS vnumber="3"><BR art="x-p"/>And Judah begat Phares and Zara of Thamar; and Phares begat Esrom;
        //        and Esrom begat Aram;</VERS>
        //
        // Without TOZ_SUPPRESS_PARA_BREAKS == true:
        //
        //   <CHAPTER cnumber="1">
        //      <VERS vnumber="1">The book of the generation of Jesus Christ, the son of David, the son of Abraham.</VERS>
        //      <VERS vnumber="2">Abraham begat Isaac; and Isaac begat Jacob; and Jacob begat Judah
        //        and his brethren;</VERS>
        //      <VERS vnumber="3">And Judah begat Phares and Zara of Thamar; and Phares begat Esrom;
        //        and Esrom begat Aram;</VERS>

        public static bool? ZSUPPRESS_PARA_BREAKS;

        // For ToZephania only, add end-style-tag comments.
        // This example adds <!-- end css-red --> before </STYLE> :
        //
        //<VERS vnumber="44">And saith unto him, <STYLE css="color:#FF0000">See thou say nothing to any man:
        //        but go thy way, shew thyself to the priest, and offer for thy cleansing those things which Moses
        //        commanded, for a testimony unto them.<!-- end css-red --></STYLE></VERS>
        public static bool? ZDEBUG_ADD_STYLE_ENDING_COMMENTS;

#pragma warning disable CS8618
        private static TreeObject[][] ZUsxTree;
        private static BookEnum[] ZUsxBooks;

        // Placeholder for each book: copy of above
        private static TreeObject[] ZTree;
        private static BookEnum ZBookEnum;
        private static int ZTreeIndex;
#pragma warning restore


        // Converting to Zephania 2005, not Zephania 2014
        // AFAIKT Z2014 doesn't support red text (css=color:#FF0000)
        public static void ZConvert(string outputFileName)
        {
            BibleBooks.Init();

#pragma warning disable CS8604
            ReadBible(TOZ_USX_SOURCE_FOLDER, out ZUsxTree, out ZUsxBooks);
#pragma warning restore

            if (ZUsxBooks == null || ZUsxBooks.Length == 0)
                Utils.LogFatal("Zephania conversion: no books to work on");

#pragma warning disable CS8602
            BookEnum firstBook = ZUsxBooks[0];
#pragma warning restore
            string subjectMatter = "Collection of Bible books";
            if (firstBook == BookEnum.MATT && ZUsxBooks.Length == BibleBooks.NUM_BOOKS_IN_NEW_TESTAMENT)
                subjectMatter = "New Testament";
            else if (firstBook == BookEnum.GENESIS && ZUsxBooks.Length == BibleBooks.NUM_BOOKS_IN_OLD_TESTAMENT)
                subjectMatter = "Old Testament";
            else if (firstBook == BookEnum.GENESIS && ZUsxBooks.Length == (BibleBooks.NUM_BOOKS_IN_OLD_TESTAMENT + BibleBooks.NUM_BOOKS_IN_NEW_TESTAMENT))
                subjectMatter = "The Bible";

            Line = new string[10000000];
            N = 0;
            Indent = Utils.BuildIndentationArray();
            Lvl = 0;

            // todo

#pragma warning disable CS8604
            ZWriteBeginning(TOZ_BIBLE_NAME,
                            TOZ_CREATOR,
                            subjectMatter,
                            TOZ_PUBLISHER,
                            TOZ_BIBLE_ABBREV,
                            TOZ_LANGUAGE_ABBREV);
#pragma warning restore

            for (int i = 0; i < ZUsxBooks.Length; i++)
            {
                ZWriteBook(i);
            }

            Lvl--;
            Line[N++] = String.Format("{0}</XMLBIBLE>", Indent[Lvl]);

            Array.Resize(ref Line, N);

            // Write results to file
#pragma warning disable CS8604
            Utils.ConcatenateFqFileName(outputFileName, TOZ_OUTPUT_FOLDER, out string outputFqFileName);
#pragma warning restore

            string outputFolderNameOnly = Path.GetDirectoryName(outputFqFileName)!;
            if (!Directory.Exists(outputFolderNameOnly))
            {
                Utils.LogFatal("Folder {0} doesn't exist for writing Zephania file", outputFolderNameOnly);
            }

            File.WriteAllLines(outputFqFileName, Line);
        }

        private static void ZWriteBeginning(string bibleName,
                                            string creator,
                                            string subject,
                                            string publisher,
                                            string bibleAbbreviation,
                                            string languageAbbreviation)
        {

            Line[N++] = "<?xml version=\"1.0\" encoding=\"utf-8\"?>";
            Line[N++] = "<!-- Generated by UsxConvert's \"ToZephania\" utility -->";
            Line[N++] = "<!--Visit the online documentation for Zefania XML Markup-->";
            Line[N++] = "<!--http://bgfdb.de/zefaniaxml/bml/-->";
            Line[N++] = "<XMLBIBLE xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:noNamespaceSchemaLocation=\"zef2005.xsd\"";
            Lvl += 2;
            Line[N++] = string.Format("{0}version=\"2.0.1.18\" revision=\"1\" status=\"v\" biblename=\"{1}\" type=\"x-bible\">",
                               Indent[Lvl], bibleName);
            Lvl -= 2;

            Lvl++;
            Line[N++] = string.Format("{0}<INFORMATION>", Indent[Lvl]);
            Lvl++;
            Line[N++] = string.Format("{0}<format>Zefania XML Bible Markup Language</format>", Indent[Lvl]);
            Line[N++] = string.Format("{0}<!-- This is to Zephania 2005 format, not Zephania 2014 -->", Indent[Lvl]);
            // https://www.c-sharpcorner.com/blogs/date-and-time-format-in-c-sharp-programming1
            Line[N++] = string.Format("{0}<date>{1}</date>", Indent[Lvl], DateTime.Now.ToString("yyyy'-'MM'-'dd"));
            Line[N++] = string.Format("{0}<title>{1}</title>", Indent[Lvl], bibleName);
            Line[N++] = string.Format("{0}<creator>{1}</creator>", Indent[Lvl], creator);
            Line[N++] = string.Format("{0}<subject>{1}</subject>", Indent[Lvl], subject);
            Line[N++] = string.Format("{0}<description>{1}</description>", Indent[Lvl], bibleName);
            Line[N++] = string.Format("{0}<publisher>{1}</publisher>", Indent[Lvl], publisher);
            // <contributers> left blank
            Line[N++] = string.Format("{0}<type>Bible</type>", Indent[Lvl]);
            Line[N++] = string.Format("{0}<identifier>{1}</identifier>", Indent[Lvl], bibleAbbreviation);
            Line[N++] = string.Format("{0}<source>the Bible</source>", Indent[Lvl]);
            Line[N++] = string.Format("{0}<language>{1}</language>", Indent[Lvl], languageAbbreviation);
            // <coverage> left blank
            // <rights> left blank
            Lvl--;
            Line[N++] = string.Format("{0}</INFORMATION>", Indent[Lvl]);
        }

        private static void ZWriteBook(int bookIndex)
        {
            ZTree = ZUsxTree[bookIndex];
            ZBookEnum = ZUsxBooks[bookIndex];
            ZTreeIndex = 0;
            BookCode = UsxDefinitions.BibleBookNamesText[(int)ZBookEnum];


            if (ZTree[ZTreeIndex].ElementType != ElementEnum.USX_WRAPPER)
                Utils.LogFatal("ZTree sanity: expecting usx wrapper!");
            ZTreeIndex = TreeStep(ref ZTree, ZTreeIndex);

            Line[N++] = string.Format("{0}<BIBLEBOOK bnumber=\"{1}\" bname=\"{2}\" bsname=\"{3}\">", Indent[Lvl],
                           (int)ZBookEnum, BibleBooks.AllBooksFullNames[(int)ZBookEnum], BibleBooks.AllBooksAbbrev[(int)ZBookEnum]);
            Lvl++;

            if (ZTree[ZTreeIndex].ElementType != ElementEnum.BOOK)
                Utils.LogFatal("ZTree sanity: expecting book!");
            ZTreeIndex = TreeStep(ref ZTree, ZTreeIndex);

            // Quietly consume and discard headings and introductory paragraphs
            string paraType = ZTree[ZTreeIndex].Style;
            bool isIntroPara = ZTree[ZTreeIndex].ElementType == ElementEnum.PARA &&
                             (IsBookHeaderParaStyle(paraType) || IsBookTitleParaStyle(paraType));

            while (isIntroPara)
            {
                ZTreeIndex = TreeStepBlowByChildren(ref ZTree, ZTreeIndex);

                paraType = ZTree[ZTreeIndex].Style;
                isIntroPara = ZTree[ZTreeIndex].ElementType == ElementEnum.PARA &&
                                 (IsBookHeaderParaStyle(paraType) || IsBookTitleParaStyle(paraType));
            }

            bool isChapter = ZTree[ZTreeIndex].ElementType == ElementEnum.CHAPTER;
            if (!isChapter)
                Utils.LogFatal("ZTree sanity: expecting chapter!");

            // Do each chapter
            while (isChapter)
            {
                ZWriteChapter();

                if (ZTreeIndex == -1)
                    break;

                isChapter = ZTree[ZTreeIndex].ElementType == ElementEnum.CHAPTER;
            }

            Lvl--;
            Line[N++] = string.Format("{0}</BIBLEBOOK>", Indent[Lvl]);
        }

        private static void ZWriteChapter()
        {
            int chapterNumber = ZTree[ZTreeIndex].Number;
            CurrentChapterNumber = chapterNumber;

            //**
            //*****  Step #1: Translate USX info into a verse-based system
            //**

            const int MAX_NUM_VERSES = 200;
            const int MAX_NUM_CHARS_IN_A_VERSE = 2000;
            const int MAX_STACK_DEPTH = 10;

            const int ITALICS = 1;
            const int BOLD = 2;
            const int SUPERSCRIPT = 4;
            const int WORDS_OF_JESUS = 8;
            const int SMALL_CAPS = 16;
            const int UNSUPPORTED = 32;

            int currentCharStyleMask = 0;

            // verseText[A][B] :   A ==> verse number (1-based/always skips 0)    B ==> char in verse
            // 'verseText' is text to be printed
            // 'markupBitField' is markup on a per-char basis
            // 'breakCount' is number of para breaks to apply *before* this char
            char[][] verseText = new char[MAX_NUM_VERSES][];    // [][2000]
            int[][] markupBitField = new int[MAX_NUM_VERSES][];
            int[][] breakCount = new int[MAX_NUM_VERSES][];

            // current verse number (and hence A-index in 'verseText', etc) being processed
            int vNumber = -1;   // [A][]
            bool doingAVerse = false;      // 'true'==actively processing a verse
            bool doingFirstVerse = true;

            // number of para breaks which occured between last verse and this verse
            int interVerseParagraphBreakCount = 0;
            // Offset in 'verseText' / length of text there
            int charOffset = 0; // [][B]

            int chapterLevel = ZTree[ZTreeIndex].Level;

            // step past chapter start
            ZTreeIndex = TreeStep(ref ZTree, ZTreeIndex);
            int currentLevel = ZTree[ZTreeIndex].Level;


            int topCharElementLevel = -1;  // -1==no char in effect. If != -1, gives ZTree's level of topmost char element
            // Nested usx char element formatting markup
            int[] formattingStack = new int[MAX_STACK_DEPTH];
            int stackDepth = 0;

            bool blowByStep = false;

            bool isChapterEnd = ZTree[ZTreeIndex].ElementType == ElementEnum.CHAPTER &&
                                ZTree[ZTreeIndex].Eid != null && ZTree[ZTreeIndex].Eid.Length > 0;

            while (!isChapterEnd)
            {
                ElementEnum elementEnum = ZTree[ZTreeIndex].ElementType;

                if (elementEnum == ElementEnum.PARA)
                {
                    // Since Zephania has no paragraph styles,
                    //  every USX para just becomes a break
                    if (doingAVerse)
                    {
                        breakCount[vNumber][charOffset]++;
                    }
                    else
                    {
                        // Add a paragraph break
                        //   to the first char of the next verse.
                        interVerseParagraphBreakCount++;
                    }
                }
                else if (elementEnum == ElementEnum.CHAR)
                {
                    //if (topCharElementLevel != -1)
                    //{
                    //    topCharElementLevel = ZTree[ZTreeIndex].Level;
                    //}
                    if (stackDepth == 0)
                    {
                        topCharElementLevel = ZTree[ZTreeIndex].Level;
                    }

                    string charStyle = ZTree[ZTreeIndex].Style;

                    if (charStyle == "bdit")
                    {
                        formattingStack[stackDepth] = BOLD;
                        formattingStack[stackDepth] = ITALICS;
                        stackDepth++;
                    }
                    else if (charStyle == "it")
                    {
                        formattingStack[stackDepth++] = ITALICS;
                        currentCharStyleMask |= ITALICS;
                    }
                    else if (charStyle == "bd")
                    {
                        formattingStack[stackDepth++] = BOLD;
                        currentCharStyleMask |= BOLD;
                    }
                    else if (charStyle == "sup" || charStyle == "em")
                    {
                        formattingStack[stackDepth++] = SUPERSCRIPT;
                        currentCharStyleMask |= SUPERSCRIPT;
                    }
                    else if (charStyle == "wj")
                    {
                        formattingStack[stackDepth++] = WORDS_OF_JESUS;
                        currentCharStyleMask |= WORDS_OF_JESUS;
                    }
                    else if (charStyle == "sc")
                    {
                        formattingStack[stackDepth++] = SMALL_CAPS;
                        currentCharStyleMask |= SMALL_CAPS;
                    }
                    else
                    {
                        formattingStack[stackDepth++] = UNSUPPORTED;
                        currentCharStyleMask |= UNSUPPORTED;
                    }
                }
                else if (elementEnum == ElementEnum.VERSE)
                {
                    bool isStart = ZTree[ZTreeIndex].Sid != null && ZTree[ZTreeIndex].Sid.Length > 0;

                    if (isStart)    // <verse number="1" style="v" sid="1CO 1:1" />
                    {
                        charOffset = 0;

                        vNumber = ZTree[ZTreeIndex].Number;
                        CurrentVerseNumber = vNumber;

                        verseText[vNumber] = new char[MAX_NUM_CHARS_IN_A_VERSE];
                        markupBitField[vNumber] = new int[MAX_NUM_CHARS_IN_A_VERSE];
                        breakCount[vNumber] = new int[MAX_NUM_CHARS_IN_A_VERSE];

                        // catch up on any para or break elements which occured between verses
                        if (interVerseParagraphBreakCount > 0 && !(doingFirstVerse && charOffset == 0))
                        {
#pragma warning disable CS8629
                            if (!ZSUPPRESS_PARA_BREAKS.Value)
#pragma warning restore
                            {
                                // observe that breaks are applied before index number;
                                //   these breaks get applied before the first char
                                breakCount[vNumber][0] = interVerseParagraphBreakCount;
                            }
                        }

                        doingAVerse = true;
                    }
                    else        // assume is end: <verse eid = "1CO 1:1" />
                    {
                        // Close off current verse's content
                        Array.Resize(ref verseText[vNumber], charOffset);
                        Array.Resize(ref markupBitField[vNumber], charOffset);
                        Array.Resize(ref breakCount[vNumber], charOffset);

                        doingAVerse = false;
                        doingFirstVerse = false;
                    }

                    interVerseParagraphBreakCount = 0;
                }
                else if (elementEnum == ElementEnum.TEXT)
                {
                    char[] textArray = ZTree[ZTreeIndex].Text.ToArray();
                    int textLength = textArray.Length;

                    textArray.CopyTo(verseText[vNumber], charOffset);

                    // Apply outstanding char formatting on a per-char basis
                    for (int q = charOffset; q < charOffset + textLength; q++)
                    {
                        markupBitField[vNumber][q] = currentCharStyleMask;
                    }

                    charOffset += textLength;
                }
                else if (elementEnum == ElementEnum.BREAK)
                {
                    if (doingAVerse)
                    {
                        breakCount[vNumber][charOffset]++;
                    }
                    else
                    {
                        interVerseParagraphBreakCount++;
                    }
                }
                else if (elementEnum == ElementEnum.NOTE)
                {
                    // todo
                    blowByStep = true;
                }
                else
                {
                    Utils.LogFatal("ZWriteChapter() Unknown element, fix code!");
                }

                int oldLevel = currentLevel;

                if (blowByStep)
                {
                    ZTreeIndex = TreeStepBlowByChildren(ref ZTree, ZTreeIndex);
                    blowByStep = false;
                }
                else
                {
                    ZTreeIndex = TreeStep(ref ZTree, ZTreeIndex);
                }

                isChapterEnd = ZTree[ZTreeIndex].ElementType == ElementEnum.CHAPTER &&
                    ZTree[ZTreeIndex].Eid != null && ZTree[ZTreeIndex].Eid.Length > 0;

                currentLevel = ZTree[ZTreeIndex].Level;
                if (currentLevel < oldLevel && !isChapterEnd)
                {
                    int levelsDropped = oldLevel - currentLevel;
                    for (int q = 0; q < levelsDropped; q++)
                    {
                        if (stackDepth == 0)
                        {
                            topCharElementLevel = -1;   // no char formatting in play; reset
                            break;
                        }

                        // This assumes that no char formatting wraps a char
                        // formatting of the same kind--otherwise, this'll break
                        currentCharStyleMask &= ~formattingStack[--stackDepth];
                    }
                }

            }

            if (doingAVerse)
                Utils.LogFatal("End of chapter {0} of {1} reached, but verse never closed", chapterNumber, BibleBooks.AllBooksFullNames[(int)ZBookEnum]);

            // Discard chapter end element
            ZTreeIndex = TreeStep(ref ZTree, ZTreeIndex);

            //**
            //*****  Step #2: Process and convert previous verse-system to Zephania
            //**
            Array.Resize(ref verseText, vNumber + 1);
            Array.Resize(ref markupBitField, vNumber + 1);
            Array.Resize(ref breakCount, vNumber + 1);

            Line[N++] = String.Format("{0}<CHAPTER cnumber=\"{1}\">", Indent[Lvl], chapterNumber);
            Lvl++;

            string verseTextSingleLine = "";
            //NEED THIS??? char[] verseTextSingleArray = new char[MAX_NUM_CHARS_IN_A_VERSE];

            int previousCharStyleMask = 0;

            for (int verseNumber = 0; verseNumber < verseText.Length; verseNumber++)
            {
                if (verseText[verseNumber] != null && verseText[verseNumber].Length > 0)
                {
                    int rawLength = verseText[verseNumber].Length;

                    // All Zephania examples have no leading or trailing spaces
                    //   inside verse elements. Must assume app viewers expect this.
                    //   Leading spaces don't normall exist
                    int trailingSpaceCount = 0;
                    for (int offset = rawLength - 1; offset >= 0; offset--)
                    {
                        char x = verseText[verseNumber][offset];
                        if (x == ' ')
                            trailingSpaceCount++;
                        else
                            break;
                    }
                    int truncatedRawLength = rawLength - trailingSpaceCount;

                    previousCharStyleMask = 0;
                    int lastOffsetTextCopiedFrom = 0;

                    verseTextSingleLine = String.Format("<VERS vnumber=\"{0}\">", verseNumber);

                    // walk each char in usx text for this verse
                    for (int offset = 0; offset < truncatedRawLength + 1; offset++)
                    {
                        // Do one more pass of the loop to force addition of
                        // </STYLE> elements that end at verse ending.
                        bool isExtraPassBeyondText = offset == truncatedRawLength;

                        if (!isExtraPassBeyondText)
                        {
                            int thisBreakCount = breakCount[verseNumber][offset];

                            // Types of breaks are "x-p" (new paragraph) and "x-nl" (new line).
                            // I don't know what the difference is; might be based on HTML though
                            //
                            // If the break's at the beginning of the verse, we'll use x-p;
                            //    if it's in middle of the verse, we'll use x-nl
                            if (thisBreakCount == 1 && offset == 0)
                            {
                                verseTextSingleLine += String.Format("<BR art=\"x-p\"/>");
                            }
                            else if (thisBreakCount == 1)
                            {
                                verseTextSingleLine += String.Format("<BR art=\"x-nl\"/>");
                            }
                            else if (thisBreakCount > 0 && offset == 0)
                            {
                                verseTextSingleLine += String.Format("<BR art=\"x-p\" count=\"{0}\"/>", thisBreakCount);
                            }
                            else if (thisBreakCount > 0)
                            {
                                verseTextSingleLine += String.Format("<BR art=\"x-nl\" count=\"{0}\"/>", thisBreakCount);
                            }
                        }

                        int thisCharStyleMask = 0;    // if 'isExtraPassBeyondText'=='true', then all masks get turned off
                        if (!isExtraPassBeyondText)
                            thisCharStyleMask = markupBitField[verseNumber][offset];
                        int deltaAddMask = (thisCharStyleMask ^ previousCharStyleMask) & thisCharStyleMask;
                        int deltaRemoveMask = (thisCharStyleMask ^ previousCharStyleMask) & previousCharStyleMask;

                        Utils.BitMaskToIndividualBits(deltaAddMask, out int[] addedStyles);
                        Utils.BitMaskToIndividualBits(deltaRemoveMask, out int[] removedStyles);

                        // Any change in styles? Copy over text up to this point before mark up elements
                        if (addedStyles.Length > 0 || removedStyles.Length > 0)
                        {
                            int copyLength = offset - lastOffsetTextCopiedFrom;
                            if (copyLength > 0)
                            {
                                string textToCopy = new string(verseText[verseNumber], lastOffsetTextCopiedFrom, copyLength);
                                verseTextSingleLine += textToCopy;

                                lastOffsetTextCopiedFrom = offset;
                            }
                        }

                        for (int removeIndex = 0; removeIndex < removedStyles.Length; removeIndex++)
                        {
                            string debugComment = "";
#pragma warning disable CS8629
                            if (ZDEBUG_ADD_STYLE_ENDING_COMMENTS.Value == true)
#pragma warning restore
                            {
                                if (removedStyles[removeIndex] == SMALL_CAPS)
                                    debugComment = String.Format("<!-- end small-caps -->");
                                else if (removedStyles[removeIndex] == WORDS_OF_JESUS)
                                    debugComment = String.Format("<!-- end css-red -->");
                                else if (removedStyles[removeIndex] == SUPERSCRIPT)
                                    debugComment = String.Format("<!-- end super -->");
                                else if (removedStyles[removeIndex] == BOLD)
                                    debugComment = String.Format("<!-- end bold -->");
                                else if (removedStyles[removeIndex] == ITALICS)
                                    debugComment = String.Format("<!-- end italic -->");
                            }

                            // reverse order w.r.t. 'addStyles' loop
                            if (removedStyles[removeIndex] == SMALL_CAPS)
                                verseTextSingleLine += String.Format("{0}</STYLE>", debugComment);
                            else if (removedStyles[removeIndex] == WORDS_OF_JESUS)
                                verseTextSingleLine += String.Format("{0}</STYLE>", debugComment);
                            else if (removedStyles[removeIndex] == SUPERSCRIPT)
                                verseTextSingleLine += String.Format("{0}</STYLE>", debugComment);
                            else if (removedStyles[removeIndex] == BOLD)
                                verseTextSingleLine += String.Format("{0}</STYLE>", debugComment);
                            else if (removedStyles[removeIndex] == ITALICS)
                                verseTextSingleLine += String.Format("{0}</STYLE>", debugComment);
                        }

                        for (int addIndex = 0; addIndex < addedStyles.Length; addIndex++)
                        {
                            if (addedStyles[addIndex] == ITALICS)
                                verseTextSingleLine += String.Format("<STYLE fs=\"italic\">");
                            else if (addedStyles[addIndex] == BOLD)
                                verseTextSingleLine += String.Format("<STYLE fs=\"bold\">");
                            else if (addedStyles[addIndex] == SUPERSCRIPT)
                                verseTextSingleLine += String.Format("<STYLE fs=\"super\">");
                            else if (addedStyles[addIndex] == WORDS_OF_JESUS)
                                verseTextSingleLine += String.Format("<STYLE css=\"color:#FF0000\">");  // deprecated/not in Zephania 2014/is there an alternative?
                            else if (addedStyles[addIndex] == SMALL_CAPS)
                                verseTextSingleLine += String.Format("<STYLE fs=\"small-caps\">");
                        }

                        previousCharStyleMask = thisCharStyleMask;
                    }

                    // Copy residual text
                    if (lastOffsetTextCopiedFrom != truncatedRawLength)
                    {
                        int copyLength = truncatedRawLength - lastOffsetTextCopiedFrom;
                        if (copyLength > 0)
                        {
                            string textToCopy = new string(verseText[verseNumber], lastOffsetTextCopiedFrom, copyLength);
                            verseTextSingleLine += textToCopy;

                            lastOffsetTextCopiedFrom = verseText.Length;
                        }
                    }

                    // Add verse closing
                    verseTextSingleLine += "</VERS>";

                    // Indent, wrap, convert to multi-line, then add lines
                    string[] verseTextMultiLines = UsxConverter.SingleStringToMultiXmlStrings(verseTextSingleLine,
                                                            TARGET_LINE_LENGTH, Indent[Lvl], Indent[Lvl + 1]);

                    for (int i = 0; i < verseTextMultiLines.Length; i++)
                    {
                        Line[N++] = string.Format("{0}", verseTextMultiLines[i]);
                    }
                }
            }

            // All verses done. Add chapter ending element.
            Lvl--;
            Line[N++] = String.Format("{0}</CHAPTER> <!-- end of {1} chapter {2} -->", Indent[Lvl], BibleBooks.AllBooksFullNames[(int)ZBookEnum], chapterNumber);
        }
    }
}
