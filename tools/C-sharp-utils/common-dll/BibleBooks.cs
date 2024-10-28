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
    public enum BookEnum
    {
        GENESIS = 0, EXODUS, LEVIT, NUMB, DEUT,
        JOSH, JUDGES, RUTH,
        SAM1, SAM2, KINGS1, KINGS2, CHRON1, CHRON2,
        EZRA, NEH, ESTHER,
        JOB, PSALMS, PROV, ECCLES, SONG,
        ISAIAH, JEREM, LAMEN, EZEK, DANIEL, HOSEA,
        JOEL, AMOS, OBAD, JON, MIC, NAH, HABAK,
        ZEPH, HAG, ZECH, MAL,
        MATT, MARK, LUKE, JOHN,
        ACTS, ROM, COR1, COR2,
        GAL, EPH, PHIL, COL,
        THESS1, THESS2, TIM1, TIM2, TITUS, PHILEM,
        HEB, JAMES, PET1, PET2,
        JOHN1, JOHN2, JOHN3,
        JUDE, REV
    };

    public static class BibleBooks
    {
        //public static List<string> AllBookNamesList;
#pragma warning disable CS8618
        public static string[] AllBooksAbbrev;
        public static string[] AllBooksAbbrev2;
        public static string[] AllBooksFullNames;

        public static int[] NumberOfChaptersPerBook;
#pragma warning restore

        public const int NUM_BOOKS_IN_OLD_TESTAMENT = 39;
        public const int NUM_BOOKS_IN_NEW_TESTAMENT = 27;

        public const int LONGEST_BOOK_NAME = 15;   // == "1 Thessalonians".LENGTH

        private static bool m_InitDone = false;

        public static void Init()
        {
            if (m_InitDone)
                return;

            m_InitDone = true;

            AllBooksAbbrev = new string[] {
                    "Gen.", "Exod.", "Lev.", "Num.", "Deut.", "Josh.", "Judg.", "Ruth",
                    "1 Sam.", "2 Sam.", "1 Kings", "2 Kings", "1 Chron.", "2 Chron.",
                    "Ezra", "Neh.", "Esther", "Job", "Ps.", "Prov.", "Eccles.",
                    "Song of Sol.", "Isa.", "Jer.", "Lam.", "Ezek.", "Dan.", "Hos.",
                    "Joel", "Amos", "Obad.", "Jon.", "Mic.", "Nah.", "Hab.",
                    "Zeph.", "Hag.", "Zech.", "Mal.", "Matt.", "Mark", "Luke", "John",
                    "Acts", "Rom.", "1 Cor.", "2 Cor.", "Gal.", "Eph.", "Phil.", "Col.",
                    "1 Thess.", "2 Thess.", "1 Tim.", "2 Tim.", "Titus", "Philem.",
                    "Heb.", "James", "1 Pet.", "2 Pet.", "1 John", "2 John",
                    "3 John", "Jude", "Rev."};

            AllBooksAbbrev2 = new string[] {
                    "Gn.", "Ex.", "Lv.", "Nm.", "Dt.", "Jo.", "Jgs.", "Ru.",
                    "1 Sm.", "2 Sm.", "1 Kgs.", "2 Kgs.", "1 Chr.", "2 Chr.",
                    "Ezr.", "Neh.", "Est.", "Jb.", "Pss.", "Prv.", "Eccl.",
                    "Sg.", "Is.", "Jer.", "Lam.", "Ez.", "Dn.", "Hos.",
                    "Jl.", "Am.", "Ob.", "Jon.", "Mi.", "Na.", "Hb.",
                    "Zep.", "Hg.", "Zec.", "Mal.", "Mt.", "Mk.", "Lk.", "Jn.",
                    "Acts", "Rom.", "1 Cor.", "2 Cor.", "Gal.", "Eph.", "Phil.", "Col.",
                    "1 Thes.", "2 Thes.", "1 Tm.", "2 Tm.", "Ti.", "Phlm.",
                    "Heb.", "Jas.", "1 Pt.", "2 Pt.", "1 Jn.", "2 Jn.",
                    "3 Jn.", "Jude", "Rv."};

            AllBooksFullNames = new string[] {
                    "Genesis", "Exodus", "Leviticus", "Numbers", "Deuteronomy", "Joshua",
                    "Judges", "Ruth", "1 Samuel", "2 Samuel", "1 Kings", "2 Kings",
                    "1 Chronicles", "2 Chronicles", "Ezra", "Nehemiah", "Esther",
                    "Job", "Psalm", "Proverbs", "Ecclesiastes", "Song of Solomon",
                    "Isaiah", "Jeremiah", "Lamentations", "Ezekiel", "Daniel",
                    "Hosea", "Joel", "Amos", "Obadiah", "Jonah", "Micah", "Nahum",
                    "Habakkuk", "Zephaniah", "Haggai", "Zechariah", "Malachi", "Matthew",
                    "Mark", "Luke", "John", "Acts", "Romans", "1 Corinthians",
                    "2 Corinthians", "Galatians", "Ephesians", "Philippians", "Colossians",
                    "1 Thessalonians", "2 Thessalonians", "1 Timothy", "2 Timothy",
                    "Titus", "Philemon", "Hebrews", "James", "1 Peter", "2 Peter",
                    "1 John", "2 John", "3 John", "Jude", "Revelation"};

            NumberOfChaptersPerBook = new int[NUM_BOOKS_IN_OLD_TESTAMENT + NUM_BOOKS_IN_NEW_TESTAMENT]
            {
                // OT is TBD
                    0, 0, 0, 0, 0, 0,                   // "Genesis", "Exodus", "Leviticus", "Numbers", "Deuteronomy", "Joshua"
                    0, 0, 0, 0, 0, 0,                   // "Judges", "Ruth", "1 Samuel", "2 Samuel", "1 Kings", "2 Kings"
                    0, 0, 0, 0, 0,                      // "1 Chronicles", "2 Chronicles", "Ezra", "Nehemiah", "Esther",
                    0, 0, 0, 0, 0,                      // "Job", "Psalm", "Proverbs", "Ecclesiastes", "Song of Solomon",
                    0, 0, 0, 0, 0,                      // "Isaiah", "Jeremiah", "Lamentations", "Ezekiel", "Daniel",
                    0, 0, 0, 0, 0, 0, 0,                // "Hosea", "Joel", "Amos", "Obadiah", "Jonah", "Micah", "Nahum",
                    0, 0, 0, 0, 0, 28,                  // "Habakkuk", "Zephaniah", "Haggai", "Zechariah", "Malachi", "Matthew",
                    16, 24, 21, 28, 16, 16,             // "Mark", "Luke", "John", "Acts", "Romans", "1 Corinthians",
                    13, 6, 6, 4, 4,                     // "2 Corinthians", "Galatians", "Ephesians", "Philippians", "Colossians",
                    5, 3, 6, 4,                         // "1 Thessalonians", "2 Thessalonians", "1 Timothy", "2 Timothy",
                    3, 1, 13, 5, 5, 3,                  // "Titus", "Philemon", "Hebrews", "James", "1 Peter", "2 Peter",
                    5, 1, 1, 1, 22                      // "1 John", "2 John", "3 John", "Jude", "Revelation"
            };

        }

        // Matches 'bookNameText' starting at 'offset' but not to end of 'bookNameText'
        public static bool BookNameMatch(ref string bookNameText, int offset, out BookEnum book, out string bookString)
        {
            Init();

            for (int i = 0; i < AllBooksFullNames.Length; i++)
            {
                if (String.Compare(bookNameText, offset, AllBooksFullNames[i], 0, AllBooksFullNames[i].Length) == 0)
                {
                    book = (BookEnum)i;
                    bookString = AllBooksFullNames[i];
                    return true;
                }
            }

            for (int i = 0; i < AllBooksAbbrev.Length; i++)
            {
                if (String.Compare(bookNameText, offset, AllBooksAbbrev[i], 0, AllBooksAbbrev[i].Length) == 0)
                {
                    book = (BookEnum)i;
                    bookString = AllBooksAbbrev[i];
                    return true;
                }
            }

            for (int i = 0; i < AllBooksAbbrev2.Length; i++)
            {
                if (String.Compare(bookNameText, offset, AllBooksAbbrev2[i], 0, AllBooksAbbrev2[i].Length) == 0)
                {
                    book = (BookEnum)i;
                    bookString = AllBooksAbbrev2[i];
                    return true;
                }
            }

            book = BookEnum.GENESIS;
            bookString = "";
            return false;
        }

        // Same as 'BookNameMatch()' but search backwards
        public static bool BookNameMatchBackwards(ref string bookNameText, int offset,
                                       out BookEnum book, out string bookString, out int matchOffset)
        {
            const int MAX_REWIND = LONGEST_BOOK_NAME + 1;     // add 1 for space

            BookEnum matchBook = BookEnum.GENESIS;
            string matchBookString = "";

            // Start backwards at shortest book name, then continue to work
            // your way back
            for (int i = offset - 3; (i >= offset - MAX_REWIND) && (i >= 0); i--)
            {
                if (BookNameMatch(ref bookNameText, i, out matchBook, out matchBookString))
                {
                    book = matchBook;
                    bookString = matchBookString;
                    matchOffset = i;

                    // Dig deeper to see if "John" is actually "1 John", etc.
                    if (matchBook == BookEnum.JOHN)
                    {
                        // Just look 2 spaces back b/c "John" can only become "1 Joh"
                        for (int k = i - 1; (k >= i - 2) && (k >= 0); k--)
                        {
                            if (BookNameMatch(ref bookNameText, i, out matchBook, out matchBookString))
                            {
                                book = matchBook;
                                bookString = matchBookString;
                                matchOffset = k;
                                return true;
                            }
                        }
                    }

                    return true;
                }
            }

            book = BookEnum.GENESIS;
            bookString = "";
            matchOffset = 0;
            return false;
        }

        public static bool IsSingleChapterBook(BookEnum book)
        {
            Init();

            BookEnum[] singleChapterList = new BookEnum[]
            {
                BookEnum.OBAD,
                BookEnum.JOHN2,
                BookEnum.JOHN3,
                BookEnum.PHILEM,
                BookEnum.JUDE,
            };

            if (Array.IndexOf(singleChapterList, book) != -1)
                return true;

            return false;
        }

        public static bool IsGospel(BookEnum book)
        {
            BookEnum[] x = new BookEnum[]
            {
                BookEnum.MATT, BookEnum.MARK, BookEnum.LUKE, BookEnum.JOHN,
            };

            bool found = Array.Find(x, name => book == name) == book;
            return found;
        }

        public static bool IsEpistle(BookEnum book)
        {
            BookEnum[] x = new BookEnum[]
            {
                BookEnum.ROM, BookEnum.COR1, BookEnum.COR2, BookEnum.GAL, BookEnum.EPH, BookEnum.PHIL, BookEnum.COL,
                BookEnum.THESS1, BookEnum.THESS2, BookEnum.TIM1, BookEnum.TIM2, BookEnum.TITUS, BookEnum.PHILEM,
                BookEnum.HEB, BookEnum.JAMES, BookEnum.PET1, BookEnum.PET2, BookEnum.JOHN1, BookEnum.JOHN2, BookEnum.JOHN3,
                BookEnum.JUDE,
            };

            bool found = Array.Find(x, name => book == name) == book;
            return found;
        }

        public static int NumberOfChapters(BookEnum book)
        {
            Init();

            return NumberOfChaptersPerBook[(int)book];
        }

        // 'chapterOffset' is ranged 0-259 for all chapters in the NT, starting
        // with Matthew chapter 1 and ending with Revelation chapter 22
        // 'chapterNumber' is 1-based value
        public static bool NewTestamentBookChapterByOffset(int chapterOffset, out BookEnum book, out int chapterNumber)
        {
            book = BookEnum.GENESIS;
            chapterNumber = 0;

            int chapterCount = 0;
            for (int i = 0; i < NUM_BOOKS_IN_NEW_TESTAMENT; i++)
            {
                int numberOfChaptersInThisBook = NumberOfChaptersPerBook[i + NUM_BOOKS_IN_OLD_TESTAMENT];

                if ((chapterOffset >= chapterCount) && (chapterOffset < chapterCount + numberOfChaptersInThisBook))
                {
                    book = (BookEnum)(NUM_BOOKS_IN_OLD_TESTAMENT + i);
                    chapterNumber = chapterOffset - chapterCount + 1;
                    return true;
                }

                chapterCount += numberOfChaptersInThisBook;
            }

            return false;
        }
    }
}
