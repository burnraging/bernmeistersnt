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
using System.ComponentModel.Design;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.Arm;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace common_dll
{
#pragma warning disable CS8618
    public class BookInfo
    {
        public BookEnum BookEnum;
        public Paragraph[] Paragraph;          // Copy of relevant elements from DocxTextScanner.AllParagraphs
        public ChapterInfo[] Chapters;
    }

    public class ChapterInfo
    {
        public int ChapterNumber;
        public bool IsIntroductionNotChapter;
        public Paragraph[] Paragraph;         // Copy of relevant elements from BookInfo.Paragraphs
        public IntroductionInfo[] IntroductionParagraphs; // only set for introduction
        public VerseInfo[] Verses;                        // only set for chapters/not set for introduction
        public FootnoteInfo[] Footnotes;                  // (same)
        public int ChapterOrIntroStartIndex;   // in 'Paragraph', first index of content. Skips over any spacers.
        public int ChapterOrIntroLength;       // in 'Paragraph', length starting at 0 of span of paragraph elements
        public int FootnotesStartIndex;        // in 'Paragraph', first index of footnotes. Skips over any spacers. -1 if no footnotes.
        public int FootnotesLength;            // in 'Paragraph', length starting at 'FootnotesStartIndex' of footnote elements. -1 if no footnotes
    }

    public class IntroductionInfo
    {
        public string UnprocessedText;
        // In the intro, there are currently no differences between abridged and
        //   unabridged forms. No plans to make different either.
        public string ProcessedText_Unabridged;
        public string ProcessedText;              //
        public string[] MultiLineText_Unabridged; // == 'ProcessedText' broken into multiple lines
        public string[] MultiLineText;            // 
        public MarkerEnum ParagraphFormatting;
    }

    public class VerseInfo
    {
        public string UnprocessedText;
        // There will be two forms of processed text and multiline text,
        //   as they're different between abridged and unabridged editions
        public string ProcessedText_Unabridged;
        public string ProcessedText;                    // unabridged equivalent
        public string[] MultiLineText_Unabridged;       // == 'ProcessedText' broken into multiple lines
        public string[] MultiLineText;                  // unabridged equivalent
        public MarkerEnum ParagraphFormatting;
        public int VerseNumber;
        public bool ContinuedFromPrevious;   // Split verse: started in previous paragraph
        public bool ContinuedToNext;         // Split verse: will continue into next paragraph

        public int ParagraphIndex;           // x ==> ChapterInfo.Paragraph[x]
        public int OffsetInParagraph;        // x ==> ChapterInfo.Paragraph[ParagraphIndex].ProcessedText[x]

        //public string[] FootnoteStack;       // footnotes contained in this verse
    }

    public class FootnoteInfo
    {
        public string FootnoteLetter;         // most of the time, 1 letter. Sometimes 2 letters
        public FootnoteParagraph[] Paragraph;
    }

    public class FootnoteParagraph
    {
        public MarkerEnum ParagraphStyle;     // should all be the same
        public string UnprocessedText;
        public string ProcessedText;
        public string[] MultiLineText;        // == 'ProcessedText' broken into multiple lines
    }
#pragma warning restore

    public static partial class UsxConverter
    {
        // If 'true' and if author chooses not to embed chapter number
        //  in the Heading2 title, then assume that the first Heading2
        //  section is an introduction, not a chapter.
        // If 'false', assume first Heading2 is first chapter.
        // In either case, chapters will be one after another,
        //  and that there'll be no other Heading2 content.
        public static bool? ASSUME_EACH_BOOK_HAS_AN_INTRODUCTION;

        // If there is an introduction (ASSUME_EACH_BOOK_HAS_AN_INTRODUCTION==true),
        //   and if INTRODUCTION_USES_NO_HEADING2==true, do not require
        //   the introduction to have its own Heading2 title.
        public static bool? INTRODUCTION_USES_NO_HEADING2;

        // Scan the Heading2 for a chapter number
        // (Books with number in themn like 2 John are a special case)
        public static bool? SCAN_CHAPTER_NUMBER_FROM_TITLE;

        // Ideal maximum number of characters on a line of usx text.
        // This does not take indentation into consideration.
        public static int? MAX_CHARS_PER_LINE;

        // All text which is of FOOTNOTES_NORMAL style will be scanned
        // for Strong's numbers (ex: "Strong’s 1967"), the text "Strong’s 1967"
        // will be strippted and replaced with a USX encoding for Strong's number.
        public static bool? SCAN_AND_MARKUP_STRONGS_NUMBERS;

#pragma warning disable CS8618
        public static string PROCESSED_TEXT_OUTPUT_FILE_NAME_UNABRIDGED;
        public static string PROCESSED_TEXT_OUTPUT_FILE_NAME_ABRIDGED;
#pragma warning restore


        private const int INTRO_MAGIC_NUMBER = 999;
        private const int SPACER_VERSE_MAGIC_NUMBER = -2;

#pragma warning disable CS8618
        public static BookInfo[] AllBooks;
#pragma warning restore

        public static void Init()
        {
            AllBooks = new BookInfo[BibleBooks.NUM_BOOKS_IN_NEW_TESTAMENT];

            for (int i = 0; i < AllBooks.Length; i++)
            {
                AllBooks[i] = new BookInfo();
                AllBooks[i].BookEnum = BookEnum.MATT + i;
                int numChapters = BibleBooks.NumberOfChapters(AllBooks[i].BookEnum);
#pragma warning disable CS8629
                if (ASSUME_EACH_BOOK_HAS_AN_INTRODUCTION.Value)   // the intro occupies a BookInfo.Chapter[] element
                    numChapters++;
#pragma warning restore
                AllBooks[i].Chapters = new ChapterInfo[numChapters];
            }
        }


        public static void TransferParagraphs(ref Paragraph[] allParagraphs)
        {

            //*****
            //****** Create all books in 'AllBooks'. Copy over paragraphs for that book.
            //*****

            for (int i = 0; i < allParagraphs.Length; i++)
            {
                if (allParagraphs[i].ParagraphStyle == MarkerEnum.HEADING1)
                {
                    string heading1Text = allParagraphs[i].ProcessedText;

                    BookEnum bookEnum = BookEnum.GENESIS;
                    string bookNameString = "invalid";
                    bool startBookSection = false;

                    // Search Heading1 text for a book name somewhere in it.
                    // Assume that that section contians the chapters for the book.
                    for (int k = 0; k < heading1Text.Length; k++)
                    {
                        if (BibleBooks.BookNameMatch(ref heading1Text, k, out bookEnum, out bookNameString))
                        {
                            int bookIndex = bookEnum - BookEnum.MATT;
                            if (bookIndex < 0 || bookIndex >= BibleBooks.NUM_BOOKS_IN_NEW_TESTAMENT)
                            {
                                Utils.LogFatal("Book {0} is not in NT!", bookNameString);
                            }

                            startBookSection = true;
                            break;
                        }
                    }

                    if (startBookSection)
                    {
                        int numParagraphs = 0;

                        // FFWD to next Heading1 (regardless of what that heading is for), to tell where this section ends
                        // End of 'allParagraphs' acts like a Heading1 also
                        int lookaheadStartIndex = i + 1;
                        for (int paragraphLookaheadIndex = lookaheadStartIndex; paragraphLookaheadIndex < allParagraphs.Length; paragraphLookaheadIndex++)
                        {
                            bool lastParagraphInWordDocument = paragraphLookaheadIndex == allParagraphs.Length - 1;
                            bool isNextHeading1 = allParagraphs[paragraphLookaheadIndex].ParagraphStyle == MarkerEnum.HEADING1;
                            if (isNextHeading1 || lastParagraphInWordDocument)
                            {
                                numParagraphs = paragraphLookaheadIndex - i;
                                if (isNextHeading1)
                                    numParagraphs--;   // exclude next Heading1 paragraph

                                Paragraph[] selectParagraphRange = new Paragraph[numParagraphs];

                                // Copy all paragraphs in this section. This will not include either
                                // start or end Heading1 lines.
                                for (int p = 0; p < numParagraphs; p++)
                                {
                                    selectParagraphRange[p] = allParagraphs[p + lookaheadStartIndex];
                                }

                                // THIS MUST BE CHANGED TO ALLOW CONVERSIONS OF OLD TESTAMENT OR FULL BIBLES!
                                int allBooksIndex = bookEnum - BookEnum.MATT;
                                if (AllBooks[allBooksIndex].BookEnum != bookEnum)
                                {
                                    Utils.LogFatal("Book {0} is out of order (expects {1})!", bookEnum.ToString(), AllBooks[allBooksIndex].BookEnum);
                                }

                                AllBooks[allBooksIndex].Paragraph = selectParagraphRange;
                                break;
                            }
                        }

                        i += numParagraphs - 1;   // no need searching this range again.  -1 b/c loop counter will increment
                    }
                }
            }

            // Sanity check for any missing NT books
            // THIS MUST BE CHANGED TO ALLOW CONVERSIONS OF OLD TESTAMENT OR FULL BIBLES!
            for (int i = 0; i < AllBooks.Length; i++)
            {
                if (AllBooks[i] == null)
                {
                    BookEnum missingBookEnum = BookEnum.MATT + i;

                    Utils.LogFatal("Book {0} is missing!", missingBookEnum.ToString());
                }
                else if (AllBooks[i].Paragraph == null || AllBooks[i].Paragraph.Length == 0)
                {
                    Utils.LogFatal("Book {0} has no content!", AllBooks[i].BookEnum.ToString());
                }
            }

            //*****
            //****** Create all chapters in each book. Do another copy and copy over chapter content again to chapters.
            //*****

            for (int bookIndex = 0; bookIndex < AllBooks.Length; bookIndex++)
            {
                //  'chapterMapping' values: INTRO_MAGIC_NUMBER=intro; 1=chapter 1; 2=chapter 2; etc.
                int[] chapterMapping = new int[200];
                int[] chapterParagraphStartIndex = new int[200];
                int[] chapterNumParagraphs = new int[200];     // includes Heading2 line at beginning but not next Heading2 line
                int maxChapter = -1;

                // If ASSUME_EACH_BOOK_HAS_AN_INTRODUCTION, will be +1 > actual number of books
                int numberOfChaptersInThisBook = AllBooks[bookIndex].Chapters.Length;
                int actualNumberOfChaptersForThisBook = BibleBooks.NumberOfChapters(AllBooks[bookIndex].BookEnum);

#pragma warning disable CS8629
                bool skipHeading2ForIntroduction = INTRODUCTION_USES_NO_HEADING2.Value &&
                     ASSUME_EACH_BOOK_HAS_AN_INTRODUCTION.Value && numberOfChaptersInThisBook > actualNumberOfChaptersForThisBook;
#pragma warning restore

                // Walk all paragraphs in this book
                for (int i = 0; i < AllBooks[bookIndex].Paragraph.Length; i++)
                {
                    Paragraph thisParagraph = AllBooks[bookIndex].Paragraph[i];
                    bool isAHeading2Paragraph = thisParagraph.ParagraphStyle == MarkerEnum.HEADING2;

                    // A Heading2 is the start of either a chapter or a non-chapter (the introduction)
                    if (isAHeading2Paragraph || i == 0 && skipHeading2ForIntroduction)
                    {
                        maxChapter++;
                        chapterMapping[maxChapter] = 0;

                        string heading2Text;
                        if (!skipHeading2ForIntroduction)
                        {
                            heading2Text = AllBooks[bookIndex].Paragraph[i].ProcessedText;
                            // +1 to skip past Heading2 line for chapter or intro /w Heading2
                            chapterParagraphStartIndex[maxChapter] = i + 1;
                        }
                        else
                        {
                            heading2Text = "(skipped for introduction)";
                            chapterParagraphStartIndex[maxChapter] = i;
                        }
                        skipHeading2ForIntroduction = false;


#pragma warning disable CS8629
                        if (ASSUME_EACH_BOOK_HAS_AN_INTRODUCTION.Value && maxChapter == 0)
                        {
                            chapterMapping[maxChapter] = INTRO_MAGIC_NUMBER;
                        }
                        else if (SCAN_CHAPTER_NUMBER_FROM_TITLE.Value)
                        {
                            if (BibleBooks.IsSingleChapterBook(AllBooks[bookIndex].BookEnum))
                            {
                                chapterMapping[maxChapter] = 1;
                            }
                            else
                            {
                                bool ok = ScanChapterNumberFromHeading2Text(heading2Text, AllBooks[bookIndex].BookEnum,
                                                                            out int scannedChapterNumber);
                                if (!ok)
                                {
                                    Utils.LogFatal("Book {0} failed scan of chapter from {1}!", AllBooks[bookIndex].BookEnum.ToString(), heading2Text);
                                }

                                chapterMapping[maxChapter] = scannedChapterNumber;
                            }
                        }
                        // else, assume there's one Heading2 per chapter
                        else
                        {
                            if (ASSUME_EACH_BOOK_HAS_AN_INTRODUCTION.Value)
                                chapterMapping[maxChapter] = maxChapter;
                            else
                                chapterMapping[maxChapter] = maxChapter + 1;
                        }
#pragma warning restore
                    }

                    // Don't start recording until we've reached the first Heading2
                    // Assume every other Heading2 after that is for the intro or a chapter
                    if (maxChapter != -1 && !isAHeading2Paragraph)
                        chapterNumParagraphs[maxChapter]++;
                }

                // One final inc. b/c started at a -1 and we were pre-incrementing
                maxChapter++;
                // Trim back to actual values
                Array.Resize(ref chapterMapping, maxChapter);
                Array.Resize(ref chapterParagraphStartIndex, maxChapter);
                Array.Resize(ref chapterNumParagraphs, maxChapter);

#if false      // turn on for debug only
                Utils.LogEntry("Sanity for {0}    [{1}|{2}|{3}]  total-length({4}):",
                           AllBooks[bookIndex].BookEnum.ToString(), chapterMapping.Length, chapterParagraphStartIndex.Length, chapterNumParagraphs.Length,
                           AllBooks[bookIndex].Paragraph.Length);
                for (int z = 0; z < chapterMapping.Length; z++)
                {
                    int endOffset = chapterParagraphStartIndex[z] + chapterNumParagraphs[z];
                    int toNext;
                    if (z < chapterMapping.Length - 1)
                        toNext = chapterParagraphStartIndex[z + 1] - endOffset;
                    else
                        toNext = AllBooks[bookIndex].Paragraph.Length - endOffset;
                    Utils.LogEntry("  {0} [{1}]   offset({2})    length({3})  endOffset({4})  to-next({5})",
                         z, chapterMapping[z], chapterParagraphStartIndex[z], chapterNumParagraphs[z],
                         endOffset, toNext);
                }
#endif

                // Sanity checks
                int expectedChapterCount = BibleBooks.NumberOfChapters(AllBooks[bookIndex].BookEnum);
                for (int i = 0; i < maxChapter; i++)
                {
#pragma warning disable CS8629
                    if (ASSUME_EACH_BOOK_HAS_AN_INTRODUCTION.Value)
#pragma warning restore
                    {
                        if (i == 0 && chapterMapping[i] != INTRO_MAGIC_NUMBER)
                        {
                            Utils.LogFatal("Book {0} has no intro!", AllBooks[bookIndex].BookEnum.ToString());
                        }
                        else if (i > 0 && i != chapterMapping[i])
                        {
                            Utils.LogFatal("Book {0} is missing a chapter!", AllBooks[bookIndex].BookEnum.ToString());
                        }
                    }
                    else if (i + 1 != chapterMapping[i])
                    {
                        Utils.LogFatal("Book {0} has a missing/out of order chapter {1}!", AllBooks[bookIndex].BookEnum.ToString(), i);
                    }

                    // Last pass of loop?
                    if ((i == maxChapter - 1) && (chapterMapping[i] != expectedChapterCount))
                    {
                        Utils.LogFatal("Book {0} too many or too few chapters!", AllBooks[bookIndex].BookEnum.ToString());
                    }

                    // Start indices don't advance?
                    if ((i > 0) && (chapterParagraphStartIndex[i] <= chapterParagraphStartIndex[i - 1]))
                    {
                        Utils.LogFatal("Book {0} chapter paragraphs out of sequence!", AllBooks[bookIndex].BookEnum.ToString());
                    }

                    // Paragraphs within range?
                    int startIndexInThisChapter = chapterParagraphStartIndex[i];
                    int numParagraphsInThisChapter = chapterNumParagraphs[i];
                    int numParagraphsInThisBook = AllBooks[bookIndex].Paragraph.Length;
                    if (startIndexInThisChapter < 0 || startIndexInThisChapter + numParagraphsInThisChapter > numParagraphsInThisBook)
                    {
                        Utils.LogFatal("Book {0} paragraphs outside bounds of book!", AllBooks[bookIndex].BookEnum.ToString());
                    }

                    // Paragraph spans collide with next chapter?
                    bool isntFinalChapter = i < maxChapter - 1;
                    int thisChapterEndIndex = chapterParagraphStartIndex[i] + chapterNumParagraphs[i];   // next chapter start index, actually
                    int nextChapterBeginIndex = -1;
                    if (isntFinalChapter)
                        nextChapterBeginIndex = chapterParagraphStartIndex[i + 1];
                    if (isntFinalChapter && (thisChapterEndIndex > nextChapterBeginIndex))
                    {
                        Utils.LogFatal("Book {0} chapter paragraphs overlapping!", AllBooks[bookIndex].BookEnum.ToString());
                    }

                    // This chapter/intro has no content?
                    if (chapterNumParagraphs[i] == 0)
                    {
                        Utils.LogFatal("Book {0} chapter paragraph {1} has no content!", AllBooks[bookIndex].BookEnum.ToString(), i);
                    }

                }

                // Copy over paragraphs
                for (int i = 0; i < maxChapter; i++)
                {
                    Paragraph[] selectParagraphRange = new Paragraph[chapterNumParagraphs[i]];
                    for (int k = 0; k < selectParagraphRange.Length; k++)
                    {
                        int thisIndex = chapterParagraphStartIndex[i] + k;
                        selectParagraphRange[k] = AllBooks[bookIndex].Paragraph[thisIndex];
                    }

                    ChapterInfo newChapter = new ChapterInfo
                    {
                        ChapterNumber = chapterMapping[i],
                        IsIntroductionNotChapter = chapterMapping[i] == INTRO_MAGIC_NUMBER,
                        Paragraph = selectParagraphRange,
                    };

                    AllBooks[bookIndex].Chapters[i] = newChapter;
                }
            }
        }


        public static void ParseIntoVerses()
        {

            //*********
            //*************  Calculate verse and footnote starts and lengths
            //*********

            for (int bookIndex = 0; bookIndex < AllBooks.Length; bookIndex++)
            {
                for (int chapterIndex = 0; chapterIndex < AllBooks[bookIndex].Chapters.Length; chapterIndex++)
                {
                    ChapterInfo chapterCopy = AllBooks[bookIndex].Chapters[chapterIndex];

                    int numParagraphs = chapterCopy.Paragraph.Length;
                    for (int i = 0; i < numParagraphs; i++)
                    {
                        bool endOfText = chapterCopy.Paragraph[i].ParagraphStyle == MarkerEnum.SPACER_BEFORE_FOOTNOTES;
                        bool endSpacerInChapter = chapterCopy.Paragraph[i].ParagraphStyle == MarkerEnum.SPACER_BEFORE_CHAPTER;
                        bool lastParagraphInChapter = i == numParagraphs - 1;

                        if (endOfText || endSpacerInChapter || lastParagraphInChapter)
                        {
                            // 'ChapterOrIntroLength' doesn't include spacer-paragraph
                            AllBooks[bookIndex].Chapters[chapterIndex].ChapterOrIntroStartIndex = 0;   // might change this later
                            AllBooks[bookIndex].Chapters[chapterIndex].ChapterOrIntroLength = i;       // don't include spacer
                            if (lastParagraphInChapter)
                            {
                                AllBooks[bookIndex].Chapters[chapterIndex].ChapterOrIntroLength++;     // not a spacer; include
                            }

                            if (AllBooks[bookIndex].Chapters[chapterIndex].ChapterOrIntroLength == 0)
                            {
                                Utils.LogFatal("Book {0} has no content!", AllBooks[bookIndex].BookEnum.ToString());
                            }

                            // No chance of there being any footnotes?
                            if (endSpacerInChapter || lastParagraphInChapter || chapterCopy.IsIntroductionNotChapter)
                            {
                                AllBooks[bookIndex].Chapters[chapterIndex].FootnotesStartIndex = -1;
                                AllBooks[bookIndex].Chapters[chapterIndex].FootnotesLength = 0;
                            }
                            // Extra content out there, assuming it's footnotes
                            else
                            {
                                // Skip past spacer paragraph
                                int footnoteStartIndex = i + 1;

                                for (i++; i < numParagraphs; i++)
                                {
                                    endSpacerInChapter = chapterCopy.Paragraph[i].ParagraphStyle == MarkerEnum.SPACER_BEFORE_CHAPTER;
                                    lastParagraphInChapter = i == numParagraphs - 1;

                                    if (endSpacerInChapter || lastParagraphInChapter)
                                    {
                                        AllBooks[bookIndex].Chapters[chapterIndex].FootnotesStartIndex = footnoteStartIndex;
                                        AllBooks[bookIndex].Chapters[chapterIndex].FootnotesLength = i - footnoteStartIndex;
                                    }
                                }

                                // No content: must be not footnotes
                                if (AllBooks[bookIndex].Chapters[chapterIndex].FootnotesLength == 0)
                                {
                                    AllBooks[bookIndex].Chapters[chapterIndex].FootnotesStartIndex = -1;
                                    AllBooks[bookIndex].Chapters[chapterIndex].FootnotesLength = -1;
                                }
                            }
                        }
                    }
                }
            }

            //*********
            //*************  Handle all book introductions.
            //*********

            for (int bookIndex = 0; bookIndex < AllBooks.Length; bookIndex++)
            {
                for (int chapterIndex = 0; chapterIndex < AllBooks[bookIndex].Chapters.Length; chapterIndex++)
                {
                    ChapterInfo chapterCopy = AllBooks[bookIndex].Chapters[chapterIndex];

                    IntroductionInfo[] intro = new IntroductionInfo[200];
                    int numIntros = -1;

                    // Do intros, nothing else
                    if (chapterCopy.IsIntroductionNotChapter)
                    {
                        // Loop through each verse/intro paragraph in chapter. Footnotes not done here.
                        for (int paragraphIndex = chapterCopy.ChapterOrIntroStartIndex; paragraphIndex < chapterCopy.ChapterOrIntroLength; paragraphIndex++)
                        {
                            string entireParagraph = chapterCopy.Paragraph[paragraphIndex].ProcessedText;

                            IntroductionInfo introParagraph = new IntroductionInfo
                            {
                                ProcessedText = null!,                // filled in later
                                UnprocessedText = entireParagraph,
                                ParagraphFormatting = chapterCopy.Paragraph[paragraphIndex].ParagraphStyle,
                            };
                            numIntros++;
                            intro[numIntros] = introParagraph;
                        }
                    }

                    numIntros++;                            // inc. b/c we started at -1
                    Array.Resize(ref intro, numIntros);
                    AllBooks[bookIndex].Chapters[chapterIndex].IntroductionParagraphs = intro;
                }
            }

            //*********
            //*************  Create verses. Fill in text for them.
            //*********

            for (int bookIndex = 0; bookIndex < AllBooks.Length; bookIndex++)
            {
                BookEnum bookEnum = AllBooks[bookIndex].BookEnum;

                for (int chapterIndex = 0; chapterIndex < AllBooks[bookIndex].Chapters.Length; chapterIndex++)
                {
                    ChapterInfo chapterCopy = AllBooks[bookIndex].Chapters[chapterIndex];

                    VerseInfo[] verses = new VerseInfo[300];
                    int numVerses = -1;

                    // Since there's no verse numbers in intro, we would error out if we tried to do it here
                    if (!chapterCopy.IsIntroductionNotChapter)
                    {
                        // Loop through each verse/intro paragraph in chapter. Footnotes not done here.
                        for (int paragraphIndex = chapterCopy.ChapterOrIntroStartIndex; paragraphIndex < chapterCopy.ChapterOrIntroLength; paragraphIndex++)
                        {
                            if (chapterCopy.Paragraph[paragraphIndex].GarbageLine)
                                continue;

                            string paragraphCopy = chapterCopy.Paragraph[paragraphIndex].ProcessedText;

                            StripVersesFromParagraph(ref paragraphCopy,
                                                     bookEnum,
                                                     chapterCopy.ChapterNumber,
                                                     out string[] paragraphFragments,
                                                     out int[] verseNumbers);

                            bool startsWithAVerse = paragraphFragments[0].Length == 0 && verseNumbers.Length > 0;
                            if (!startsWithAVerse)
                            {
                                if (numVerses == -1)
                                    Utils.LogFatal("{0} chapter {1} paragraph should be empty!", bookEnum.ToString(), chapterCopy.ChapterNumber);

                                verses[numVerses].ContinuedToNext = true;

                                int previousVerseNumber = verses[numVerses].VerseNumber;

                                numVerses++;
                                VerseInfo newEntry = new VerseInfo
                                {
                                    UnprocessedText = paragraphFragments[0],
                                    // 'ProcessedText' to be set later
                                    ParagraphFormatting = chapterCopy.Paragraph[paragraphIndex].ParagraphStyle,
                                    VerseNumber = previousVerseNumber,
                                    ContinuedFromPrevious = true,
                                    // 'ContinuedToNext' to be set later

                                    ParagraphIndex = paragraphIndex,
                                    OffsetInParagraph = 0, // point to start of paragraph
                                };
                                verses[numVerses] = newEntry;
                            }

                            for (int i = 0; i < verseNumbers.Length; i++)
                            {
                                if (numVerses != -1)
                                {
                                    verses[numVerses].ContinuedToNext = false;
                                }

                                numVerses++;
                                VerseInfo newEntry = new VerseInfo
                                {
                                    UnprocessedText = paragraphFragments[i + 1],
                                    // 'ProcessedText' to be set later
                                    ParagraphFormatting = chapterCopy.Paragraph[paragraphIndex].ParagraphStyle,
                                    VerseNumber = verseNumbers[i],
                                    ContinuedFromPrevious = false,
                                    // 'ContinuedToNext' to be set later

                                    ParagraphIndex = paragraphIndex,
                                    OffsetInParagraph = 0, // point to start of paragraph
                                };
                                verses[numVerses] = newEntry;

                            }
                        }

                        // We're done with the verses for this chapter.
                        // Clean up.
                        numVerses++;
                        Array.Resize(ref verses, numVerses);
                        AllBooks[bookIndex].Chapters[chapterIndex].Verses = verses;
                    }
                }
            }

            //*************  Patch previous
            // In most cases, blank lines shouldn't be associated with a verse
            // Didn't want to mix it in with above logic b/c it would overcomplicate
            //   things.

            for (int bookIndex = 0; bookIndex < AllBooks.Length; bookIndex++)
            {
                BookEnum bookEnum = AllBooks[bookIndex].BookEnum;

                for (int chapterIndex = 0; chapterIndex < AllBooks[bookIndex].Chapters.Length; chapterIndex++)
                {
                    ChapterInfo chapterCopy = AllBooks[bookIndex].Chapters[chapterIndex];

                    if (!chapterCopy.IsIntroductionNotChapter)
                    {
                        // Reverse-walk verses:
                        // If following ver
                        int followingVerseParagraphIndex = -1;
                        int followingVerseNumber = -1;
                        for (int verseIndex = chapterCopy.Verses.Length - 1; verseIndex >= 0; verseIndex--)
                        {
                            bool isFirstVerse = verseIndex == 0;
                            bool isLastVerse = verseIndex == chapterCopy.Verses.Length - 1;

                            int thisVerseParagraphIndex = chapterCopy.Verses[verseIndex].ParagraphIndex;
                            int thisVerseNumber = chapterCopy.Verses[verseIndex].VerseNumber;

                            bool isBlankParagraph = chapterCopy.Paragraph[thisVerseParagraphIndex].ProcessedText.Length == 0;

                            bool nextVerseDenotesStartOfParagraph = thisVerseParagraphIndex != followingVerseParagraphIndex
                                                                &&
                                (thisVerseNumber != followingVerseNumber || followingVerseNumber == SPACER_VERSE_MAGIC_NUMBER);

                            // If the next verse is in a different paragraph and if the current verse
                            //  is in an empty paragraph, then the current verse should be disassociated
                            //  with any verse number (SPACER_VERSE_MAGIC_NUMBER), and the next/previous
                            //  pointers everywhere should be update.
                            // This is similar to removing an item from a linked list.
                            if (nextVerseDenotesStartOfParagraph && isBlankParagraph)
                            {
                                chapterCopy.Verses[verseIndex].VerseNumber = SPACER_VERSE_MAGIC_NUMBER;
                                chapterCopy.Verses[verseIndex].ContinuedFromPrevious = false;
                                chapterCopy.Verses[verseIndex].ContinuedToNext = false;

                                if (!isFirstVerse)
                                {
                                    chapterCopy.Verses[verseIndex - 1].ContinuedToNext = false;
                                }

                                if (!isLastVerse)
                                {
                                    chapterCopy.Verses[verseIndex + 1].ContinuedFromPrevious = false;
                                }
                            }

                            followingVerseParagraphIndex = thisVerseParagraphIndex;
                            followingVerseNumber = thisVerseNumber;
                        }
                    }
                }
            }


            //*********
            //*************  Footnotes
            //*********

            for (int bookIndex = 0; bookIndex < AllBooks.Length; bookIndex++)
            {
                BookEnum bookEnum = AllBooks[bookIndex].BookEnum;

                for (int chapterIndex = 0; chapterIndex < AllBooks[bookIndex].Chapters.Length; chapterIndex++)
                {
                    ChapterInfo chapterCopy = AllBooks[bookIndex].Chapters[chapterIndex];

                    FootnoteInfo[] footnotes = new FootnoteInfo[200];
                    int numFootnotes = -1;

                    // Skip intros, chapters /w no footnotes
                    if (!chapterCopy.IsIntroductionNotChapter && chapterCopy.FootnotesStartIndex != -1)
                    {
                        int thisFootnoteNumberOfParagraphs = 0;
                        HashSet<string> footnoteLetterBag = new HashSet<string>(200);

                        // Skip verse-content, skipping down to footnotes
                        for (int paragraphIndex = chapterCopy.FootnotesStartIndex;
                             paragraphIndex < chapterCopy.FootnotesStartIndex + chapterCopy.FootnotesLength;
                             paragraphIndex++)
                        {
                            if (chapterCopy.Paragraph[paragraphIndex].GarbageLine)
                                continue;

                            string paragraphCopy = chapterCopy.Paragraph[paragraphIndex].ProcessedText;

                            // Index of first non-whitespace character in this string
                            // https://stackoverflow.com/questions/12695501/get-index-of-first-non-whitespace-character-in-c-sharp-string#:~:text=You%20can%20use%20the%20String,the%20beginning%20of%20the%20string.
                            int firstNonWhitespaceIndex = paragraphCopy.TakeWhile(c => char.IsWhiteSpace(c)).Count();

                            if (paragraphIndex == 0 && firstNonWhitespaceIndex == -1)
                            {
                                Utils.LogFatal("Book {0} chapter {1} first paragraph of footnote section is blank!", AllBooks[bookIndex].BookEnum.ToString(), chapterIndex);
                            }

                            bool isVerseNumberOrFootnote = ScanVerseNumberOrFootnoteReference(ref paragraphCopy,
                                                                                    firstNonWhitespaceIndex,
                                                                                    out int verseNumberThrowAway,
                                                                                    out bool isVerseNumberNotFootnote,
                                                                                    out string footnoteLetters,
                                                                                    out int footnoteEncodingCharsConsumed,
                                                                                    bookEnum,
                                                                                    chapterCopy.ChapterNumber);
                            bool isFootnote = isVerseNumberOrFootnote && !isVerseNumberNotFootnote;
                            if (paragraphIndex == 0 && !isFootnote)
                            {
                                Utils.LogFatal("Book {0} chapter {1} footnote section doesn't begin with footnote reference!", AllBooks[bookIndex].BookEnum.ToString(), chapterIndex);
                            }

                            if (isFootnote)
                            {
                                // If 'paragraphCopy' is "[a]my-footnote-text" then
                                //  'footnoteParagraphMinusReferenceText' is "my-footnote-text"
                                int indexOfTextAfterFootnote = firstNonWhitespaceIndex + footnoteEncodingCharsConsumed;
                                string footnoteParagraphMinusReferenceText = paragraphCopy.Substring(
                                                        indexOfTextAfterFootnote, paragraphCopy.Length - indexOfTextAfterFootnote);

                                if (footnoteLetterBag.Contains(footnoteLetters))
                                {
                                    Utils.LogFatal("Book {0} chapter {1} footnote {2} is duplicated in chapter! Fix Word document!", AllBooks[bookIndex].BookEnum.ToString(), chapterIndex, footnoteLetters);
                                }
                                footnoteLetterBag.Add(footnoteLetters);

                                // Close off old footnote
                                if (numFootnotes != -1)
                                {
                                    Array.Resize(ref footnotes[numFootnotes].Paragraph, thisFootnoteNumberOfParagraphs);
                                    thisFootnoteNumberOfParagraphs = 0;
                                }

                                // Footnotes can be multiple paragraphs long.
                                // Add this paragraph to footnote
                                FootnoteParagraph footnoteParagraph = new FootnoteParagraph
                                {
                                    ParagraphStyle = chapterCopy.Paragraph[paragraphIndex].ParagraphStyle,
                                    UnprocessedText = footnoteParagraphMinusReferenceText,
                                    // 'ProcessedText' to be filled in later
                                };

                                // Create actual footnote object
                                FootnoteInfo footnote = new FootnoteInfo
                                {
                                    FootnoteLetter = footnoteLetters,
                                    Paragraph = new FootnoteParagraph[50],
                                };
                                footnote.Paragraph[thisFootnoteNumberOfParagraphs++] = footnoteParagraph;

                                numFootnotes++;
                                footnotes[numFootnotes] = footnote;
                            }
                            // Not a footnote.
                            // Must be a follow-on paragraph to previous footnote.
                            else
                            {
                                if (numFootnotes == -1)
                                {
                                    Utils.LogFatal("Book {0} chapter {1} follow-on paragraph but where's the first footnote?", AllBooks[bookIndex].BookEnum.ToString(), chapterIndex);
                                }

                                FootnoteParagraph footnoteParagraph = new FootnoteParagraph
                                {
                                    ParagraphStyle = chapterCopy.Paragraph[paragraphIndex].ParagraphStyle,
                                    UnprocessedText = paragraphCopy,
                                    // 'ProcessedText' to be filled in later
                                };

                                footnotes[numFootnotes].Paragraph[thisFootnoteNumberOfParagraphs++] = footnoteParagraph;
                            }

                        }

                        // Close out last footnote in chapter
                        Array.Resize(ref footnotes[numFootnotes].Paragraph, thisFootnoteNumberOfParagraphs);

                        // Close out all footnotes in chapter
                        numFootnotes++;                            // inc. b/c we started at -1
                        Array.Resize(ref footnotes, numFootnotes);
                        AllBooks[bookIndex].Chapters[chapterIndex].Footnotes = footnotes;
                    }

                }
            }
        }



        public static void ProcessCharacterFormatting()
        {
            for (int bookIndex = 0; bookIndex < AllBooks.Length; bookIndex++)
            {
                BookEnum bookEnum = AllBooks[bookIndex].BookEnum;

                for (int chapterIndex = 0; chapterIndex < AllBooks[bookIndex].Chapters.Length; chapterIndex++)
                {
                    ChapterInfo chapterCopy = AllBooks[bookIndex].Chapters[chapterIndex];

                    if (chapterCopy.IntroductionParagraphs != null)
                    {
                        for (int i = 0; i < chapterCopy.IntroductionParagraphs.Length; i++)
                        {
                            ApplyCharacterFormattingToSingleString(ref chapterCopy.IntroductionParagraphs[i].UnprocessedText,
                                   out AllBooks[bookIndex].Chapters[chapterIndex].IntroductionParagraphs[i].ProcessedText_Unabridged,
                                   out AllBooks[bookIndex].Chapters[chapterIndex].IntroductionParagraphs[i].ProcessedText,
                                   bookEnum, chapterCopy.ChapterNumber, i);

#pragma warning disable CS8629
                            BreakStringIntoMultipleStrings(ref AllBooks[bookIndex].Chapters[chapterIndex].IntroductionParagraphs[i].ProcessedText_Unabridged,
                                              MAX_CHARS_PER_LINE.Value,
                                              out AllBooks[bookIndex].Chapters[chapterIndex].IntroductionParagraphs[i].MultiLineText_Unabridged);
                            BreakStringIntoMultipleStrings(ref AllBooks[bookIndex].Chapters[chapterIndex].IntroductionParagraphs[i].ProcessedText,
                                              MAX_CHARS_PER_LINE.Value,
                                              out AllBooks[bookIndex].Chapters[chapterIndex].IntroductionParagraphs[i].MultiLineText);
#pragma warning restore
                        }
                    }

                    if (chapterCopy.Verses != null)
                    {
                        for (int i = 0; i < chapterCopy.Verses.Length; i++)
                        {
                            ApplyCharacterFormattingToSingleString(ref chapterCopy.Verses[i].UnprocessedText,
                                          out AllBooks[bookIndex].Chapters[chapterIndex].Verses[i].ProcessedText_Unabridged,
                                          out AllBooks[bookIndex].Chapters[chapterIndex].Verses[i].ProcessedText,
                                          bookEnum, chapterCopy.ChapterNumber, chapterCopy.Verses[i].VerseNumber);

#pragma warning disable CS8629
                            BreakStringIntoMultipleStrings(ref AllBooks[bookIndex].Chapters[chapterIndex].Verses[i].ProcessedText_Unabridged,
                                              MAX_CHARS_PER_LINE.Value,
                                              out AllBooks[bookIndex].Chapters[chapterIndex].Verses[i].MultiLineText_Unabridged);
                            BreakStringIntoMultipleStrings(ref AllBooks[bookIndex].Chapters[chapterIndex].Verses[i].ProcessedText,
                                              MAX_CHARS_PER_LINE.Value,
                                              out AllBooks[bookIndex].Chapters[chapterIndex].Verses[i].MultiLineText);
#pragma warning restore
                        }
                    }

                    if (chapterCopy.Footnotes != null)
                    {

                        for (int i = 0; i < chapterCopy.Footnotes.Length; i++)
                        {
                            for (int k = 0; k < chapterCopy.Footnotes[i].Paragraph.Length; k++)
                            {
                                ApplyCharacterFormattingToSingleString(ref chapterCopy.Footnotes[i].Paragraph[k].UnprocessedText,
                                              out AllBooks[bookIndex].Chapters[chapterIndex].Footnotes[i].Paragraph[k].ProcessedText,
                                              out string processedTextFootnotesUnabridgedThrowaway,
                                              bookEnum, chapterCopy.ChapterNumber, i);

#pragma warning disable CS8629
                                BreakStringIntoMultipleStrings(ref AllBooks[bookIndex].Chapters[chapterIndex].Footnotes[i].Paragraph[k].ProcessedText,
                                                  MAX_CHARS_PER_LINE.Value,
                                                  out AllBooks[bookIndex].Chapters[chapterIndex].Footnotes[i].Paragraph[k].MultiLineText);
#pragma warning restore
                            }
                        }
                    }
                }
            }
        }

        public static void WriteProcessedResults(bool unabridgedEdition, string outputFolder)
        {
            string fileName;
            if (unabridgedEdition)
            {
                fileName = PROCESSED_TEXT_OUTPUT_FILE_NAME_UNABRIDGED;
            }
            else
            {
                fileName = PROCESSED_TEXT_OUTPUT_FILE_NAME_ABRIDGED;
            }

            Utils.ConcatenateFqFileName(fileName, outputFolder, out string fqFileName);

            Utils.LogEntry("Printing processed output to intermediate file {0}...", fqFileName);

            string[] lines = new string[100000];
            int p = 0;

            lines[p++] = string.Format("*********");
            lines[p++] = string.Format("***************   USX Converter Processed Results  ************");
            lines[p++] = string.Format("*********");
            lines[p++] = string.Format("");
            lines[p++] = string.Format("");

            for (int bookIndex = 0; bookIndex < AllBooks.Length; bookIndex++)
            {
                lines[p++] = string.Format("");
                lines[p++] = string.Format("");
                lines[p++] = string.Format("*********");
                lines[p++] = string.Format("******************  {0}   **************", AllBooks[bookIndex].BookEnum.ToString());
                lines[p++] = string.Format("*********");

                for (int chapterIndex = 0; chapterIndex < AllBooks[bookIndex].Chapters.Length; chapterIndex++ )
                {
                    ChapterInfo chapterCopy = AllBooks[bookIndex].Chapters[chapterIndex];

                    if (chapterCopy.IsIntroductionNotChapter && chapterCopy.IntroductionParagraphs != null)
                    {
                        lines[p++] = string.Format("");
                        lines[p++] = string.Format("  >>>>>>>>>>>>>>  {0} Introduction  <<<<<<<<<<<<", AllBooks[bookIndex].BookEnum.ToString(), chapterCopy.ChapterNumber);

                        for (int paragraphIndex = 0; paragraphIndex < chapterCopy.IntroductionParagraphs.Length; paragraphIndex++)
                        {
                            if (unabridgedEdition && chapterCopy.IntroductionParagraphs[paragraphIndex].MultiLineText_Unabridged != null)
                            {
                                if (chapterCopy.IntroductionParagraphs[paragraphIndex].MultiLineText_Unabridged.Length == 0)
                                {
                                    lines[p++] = string.Format("         {0,29}: ||",
                                               chapterCopy.IntroductionParagraphs[paragraphIndex].ParagraphFormatting.ToString());
                                }
                                else
                                {
                                    for (int i = 0; i < chapterCopy.IntroductionParagraphs[paragraphIndex].MultiLineText_Unabridged.Length; i++)
                                    {
                                        if (i == 0)
                                            lines[p++] = string.Format("         {0,29}: |{1}|",
                                                       chapterCopy.IntroductionParagraphs[paragraphIndex].ParagraphFormatting.ToString(),
                                                       chapterCopy.IntroductionParagraphs[paragraphIndex].MultiLineText_Unabridged[0]);
                                        else
                                            lines[p++] = string.Format("                                        |{0}|", chapterCopy.IntroductionParagraphs[paragraphIndex].MultiLineText_Unabridged[i]);
                                    }
                                }
                            }
                            else if (unabridgedEdition)
                            {
                                lines[p++] = string.Format("         {0,29}: [no text]}",
                                           chapterCopy.IntroductionParagraphs[paragraphIndex].ParagraphFormatting.ToString());
                            }
                        }
                    }
                    else if (chapterCopy.IsIntroductionNotChapter)
                    {
                        lines[p++] = string.Format("    [ {0} Chapter {1} is missing introduction ]", AllBooks[bookIndex].BookEnum.ToString(), chapterCopy.ChapterNumber);
                    }


                    if (!chapterCopy.IsIntroductionNotChapter && chapterCopy.Verses != null)
                    {
                        lines[p++] = string.Format("");
                        lines[p++] = string.Format("  >>>>>>>>>>>>>>  {0} Chapter {1}  <<<<<<<<<<<<", AllBooks[bookIndex].BookEnum.ToString(), chapterCopy.ChapterNumber);

                        for (int verseIndex = 0; verseIndex < chapterCopy.Verses.Length; verseIndex++)
                        {
                            if (unabridgedEdition && chapterCopy.Verses[verseIndex].MultiLineText != null)
                            {
                                if (chapterCopy.Verses[verseIndex].MultiLineText.Length == 0)
                                {
                                    lines[p++] = string.Format(" {0}{1}{2} V{3,2} {4,29}: ||",
                                               true ? "+" : " ",
                                               chapterCopy.Verses[verseIndex].ContinuedFromPrevious ? "P" : " ",
                                               chapterCopy.Verses[verseIndex].ContinuedToNext ? "N" : " ",
                                               chapterCopy.Verses[verseIndex].VerseNumber != SPACER_VERSE_MAGIC_NUMBER? chapterCopy.Verses[verseIndex].VerseNumber : "--",
                                               chapterCopy.Verses[verseIndex].ParagraphFormatting.ToString());
                                }
                                else
                                {
                                    for (int i = 0; i < chapterCopy.Verses[verseIndex].MultiLineText.Length; i++)
                                    {
                                        bool isStartOfNewParagraph = true;
                                        if (verseIndex > 0)
                                            isStartOfNewParagraph = chapterCopy.Verses[verseIndex].ParagraphIndex !=
                                                                    chapterCopy.Verses[verseIndex - 1].ParagraphIndex;

                                        // "+" is start of a new paragraph
                                        // "P" is that this verse was continued from previous paragraph
                                        // "N" is that this verse is continued into next paragraph
                                        // Use big "V" for first line of verse, little "v" for continuation lines of same verse
                                        // (also style is missing for continuation lines)
                                        if (i == 0)
                                            lines[p++] = string.Format(" {0}{1}{2} V{3,2} {4,29}: |{5}|",
                                                       isStartOfNewParagraph ? "+" : " ",
                                                       chapterCopy.Verses[verseIndex].ContinuedFromPrevious ? "P" : " ",
                                                       chapterCopy.Verses[verseIndex].ContinuedToNext ? "N" : " ",
                                                       chapterCopy.Verses[verseIndex].VerseNumber != SPACER_VERSE_MAGIC_NUMBER ? chapterCopy.Verses[verseIndex].VerseNumber : "--",
                                                       chapterCopy.Verses[verseIndex].ParagraphFormatting.ToString(),
                                                       chapterCopy.Verses[verseIndex].MultiLineText_Unabridged[0]);
                                        else
                                            lines[p++] = string.Format("     v{0,2}                                |{1}|",
                                                chapterCopy.Verses[verseIndex].VerseNumber,
                                                chapterCopy.Verses[verseIndex].MultiLineText_Unabridged[i]);
                                    }
                                }
                            }
                            else if (unabridgedEdition)
                            {
                                bool isStartOfNewParagraph = true;
                                if (verseIndex > 0)
                                    isStartOfNewParagraph = chapterCopy.Verses[verseIndex].ParagraphIndex !=
                                                            chapterCopy.Verses[verseIndex - 1].ParagraphIndex;

                                lines[p++] = string.Format(" {0}{1}{2} V{3,2} {4,29}: [no text]",
                                           isStartOfNewParagraph ? "+" : " ",
                                           chapterCopy.Verses[verseIndex].ContinuedFromPrevious ? "P" : " ",
                                           chapterCopy.Verses[verseIndex].ContinuedToNext ? "N" : " ",
                                           chapterCopy.Verses[verseIndex].VerseNumber != SPACER_VERSE_MAGIC_NUMBER ? chapterCopy.Verses[verseIndex].VerseNumber : "--",
                                           chapterCopy.Verses[verseIndex].ParagraphFormatting.ToString());
                            }

                            if (!unabridgedEdition && chapterCopy.Verses[verseIndex].MultiLineText != null)
                            {
                                if (chapterCopy.Verses[verseIndex].MultiLineText.Length == 0)
                                {
                                    lines[p++] = string.Format(" {0}{1}{2} V{3,2} {4,29}: ||",
                                               true ? "+" : " ",
                                               chapterCopy.Verses[verseIndex].ContinuedFromPrevious ? "P" : " ",
                                               chapterCopy.Verses[verseIndex].ContinuedToNext ? "N" : " ",
                                               chapterCopy.Verses[verseIndex].VerseNumber != SPACER_VERSE_MAGIC_NUMBER ? chapterCopy.Verses[verseIndex].VerseNumber : "--",
                                               chapterCopy.Verses[verseIndex].ParagraphFormatting.ToString());
                                }
                                else
                                {
                                    for (int i = 0; i < chapterCopy.Verses[verseIndex].MultiLineText.Length; i++)
                                    {
                                        bool isStartOfNewParagraph = true;
                                        if (verseIndex > 0)
                                            isStartOfNewParagraph = chapterCopy.Verses[verseIndex].ParagraphIndex !=
                                                                    chapterCopy.Verses[verseIndex - 1].ParagraphIndex;

                                        if (i == 0)
                                            lines[p++] = string.Format(" {0}{1}{2} V{3,2} {4,29}: |{5}|",
                                                       isStartOfNewParagraph ? "+" : " ",
                                                       chapterCopy.Verses[verseIndex].ContinuedFromPrevious ? "P" : " ",
                                                       chapterCopy.Verses[verseIndex].ContinuedToNext ? "N" : " ",
                                                       chapterCopy.Verses[verseIndex].VerseNumber != SPACER_VERSE_MAGIC_NUMBER ? chapterCopy.Verses[verseIndex].VerseNumber : "--",
                                                       chapterCopy.Verses[verseIndex].ParagraphFormatting.ToString(),
                                                       chapterCopy.Verses[verseIndex].MultiLineText[0]);
                                        else
                                            lines[p++] = string.Format("     v{0,2}                                |{1}|",
                                                chapterCopy.Verses[verseIndex].VerseNumber != SPACER_VERSE_MAGIC_NUMBER ? chapterCopy.Verses[verseIndex].VerseNumber : "--",
                                                chapterCopy.Verses[verseIndex].MultiLineText[i]);
                                    }
                                }
                            }
                            else if (!unabridgedEdition)
                            {
                                bool isStartOfNewParagraph = true;
                                if (verseIndex > 0)
                                    isStartOfNewParagraph = chapterCopy.Verses[verseIndex].ParagraphIndex !=
                                                            chapterCopy.Verses[verseIndex - 1].ParagraphIndex;

                                lines[p++] = string.Format(" {0}{1}{2} V{3,2}  {4,29}: [no text]",
                                           isStartOfNewParagraph ? "+" : " ",
                                           chapterCopy.Verses[verseIndex].ContinuedFromPrevious ? "P" : " ",
                                           chapterCopy.Verses[verseIndex].ContinuedToNext ? "N" : " ",
                                           chapterCopy.Verses[verseIndex].VerseNumber != SPACER_VERSE_MAGIC_NUMBER ? chapterCopy.Verses[verseIndex].VerseNumber : "--",
                                           chapterCopy.Verses[verseIndex].ParagraphFormatting.ToString());
                            }

                        }
                    }
                    else if (!chapterCopy.IsIntroductionNotChapter && chapterCopy.Verses == null)
                    {
                        lines[p++] = string.Format("    [ {0} Chapter {1} has no verses ]", AllBooks[bookIndex].BookEnum.ToString(), chapterCopy.ChapterNumber);
                    }

                    if (!chapterCopy.IsIntroductionNotChapter && chapterCopy.Footnotes != null)
                    {
                        for (int footnoteIndex = 0; footnoteIndex < chapterCopy.Footnotes.Length; footnoteIndex++)
                        {
                            if (chapterCopy.Footnotes[footnoteIndex].Paragraph != null)
                            {
                                for (int paragraphIndex = 0; paragraphIndex < chapterCopy.Footnotes[footnoteIndex].Paragraph.Length; paragraphIndex++)
                                {
                                    if (chapterCopy.Footnotes[footnoteIndex].Paragraph[paragraphIndex].MultiLineText != null)
                                    {
                                        if (chapterCopy.Footnotes[footnoteIndex].Paragraph[paragraphIndex].MultiLineText.Length == 0)
                                        {
                                            lines[p++] = string.Format("    {0,2}   {1,29}: ||",
                                                       chapterCopy.Footnotes[footnoteIndex].FootnoteLetter,
                                                       chapterCopy.Footnotes[footnoteIndex].Paragraph[paragraphIndex].ParagraphStyle.ToString());
                                        }
                                        else
                                        {
                                            for (int i = 0; i < chapterCopy.Footnotes[footnoteIndex].Paragraph[paragraphIndex].MultiLineText.Length; i++)
                                            {
                                                if (i == 0)
                                                    lines[p++] = string.Format("    {0,2}   {1,29}: |{2}|",
                                                               chapterCopy.Footnotes[footnoteIndex].FootnoteLetter,
                                                               chapterCopy.Footnotes[footnoteIndex].Paragraph[paragraphIndex].ParagraphStyle.ToString(),
                                                               chapterCopy.Footnotes[footnoteIndex].Paragraph[paragraphIndex].MultiLineText[0]);
                                                else
                                                    lines[p++] = string.Format("                                        |{0}|", chapterCopy.Footnotes[footnoteIndex].Paragraph[paragraphIndex].MultiLineText[i]);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        lines[p++] = string.Format("    {0,2}  {1,29}: [no text]",
                                                    chapterCopy.Footnotes[footnoteIndex].FootnoteLetter,
                                                   chapterCopy.Footnotes[footnoteIndex].Paragraph[paragraphIndex].ParagraphStyle.ToString());
                                    }
                                }
                            }
                        }
                    }
                    else if (!chapterCopy.IsIntroductionNotChapter && chapterCopy.Footnotes == null)
                    {
                        lines[p++] = string.Format("    [ {0} Chapter {1} has no footnotes ]", AllBooks[bookIndex].BookEnum.ToString(), chapterCopy.ChapterNumber);
                    }
                }

            }

            Array.Resize(ref lines, p);
            Utils.WriteFileLines(fileName, fqFileName, ref lines, true);
        }
    }

}