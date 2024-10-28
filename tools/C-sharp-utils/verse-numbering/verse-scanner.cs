using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using common_dll;

namespace verse_numbering
{
    public static class Top
    {
        public static string? INPUT_FOLDER;
        public static string? INPUT_FILE_NAME;
        public static string? OUTPUT_FOLDER;
        public static string? VERSES_BY_LINE_OUTPUT_FILE_NAME;

        //** verse-scanner
        //public static string DOCX_TEXT_FILE_NAME_FOR_VERSE_SCANNER_ONLY = @"the-bernmeisters-new-testament-215-DESTRUCTIVE.txt";  // todo: merge with 'DOCX_TEXT_FILE_NAME' when common file format is used
        //public static string VERSES_BY_LINES_FILE_NAME = @"verses-by-lines.txt";

        // 1) Substitute the word 'Chapter' for style==Heading 2 with the chapter hash
        //    hack: add text in chapters with no word "Chapter": Philemon, 2,3 John, Jude
        // 2) Substitute ever superscript numeric digit (do digits 0-9 one by one)
        //    with verse hash + digit
        // 3) Save as text file, use these selections:
        //    (a) End lines with LF only
        //    (b) Allow character substitution
        //
        private static string CHAPTER_HASH = "!@#$%";
        private static string VERSE_HASH = "$$$$$";

#pragma warning disable CS8618
        private class FootnoteInfo
        {
            public int ArrayPtr;
            public int Index;
            public string QuotedText;
        }
#pragma warning restore

        public static void Entry()// string inputDirectory, string inputFileName, string outputDirectory, string outputFileName)
        {
#pragma warning disable CS8604
            Utils.ConcatenateFqFileName(INPUT_FILE_NAME, INPUT_FOLDER, out string fqFileName);
#pragma warning restore

            string entireTextFile = File.ReadAllText(fqFileName);

            int[] allChapterHashIndices = new int[0];
            int[] allChapterEndIndices = new int[0];
            int[] allVerseHashesIndices = new int[20000];   // trim later
            int[] allFootnoteSectionStartIndices = new int[2000];
            int allVerseHashesIndicesCount = 0;
            int[][] allVerseHashesByBook;      // [book array index][list of verse indices]
            int[][] allVerseNumbersByBook;     // [book array index][list of verse indices]
            int[][] allFootnotesByBook;        // [book array index][list of footnote start indices]
            //BookEnum[] allBooks = new BookEnum[0];
            //string[] allBookNames = new string[0];

            // Run through and populate chapter info
            // 'allChapterHashIndices',  'allBooks', 'allBookNames'
            for (int i = 0; i < entireTextFile.Length; i++)
            {
                int nextChapterIndex = entireTextFile.IndexOf(CHAPTER_HASH, i);
                if (nextChapterIndex > -1)
                {
                    if (BibleBooks.BookNameMatchBackwards(ref entireTextFile, nextChapterIndex, out BookEnum book,
                                                      out string bookText, out int matchOffset) == false)
                    {
                        Utils.LogFatal("Blew up!");
                        throw new Exception("Blew up!");
                    }

                    //Array.Resize(ref allBooks, allBooks.Length + 1);
                    //Array.Resize(ref allBookNames, allBookNames.Length + 1);
                    Array.Resize(ref allChapterHashIndices, allChapterHashIndices.Length + 1);
                    Array.Resize(ref allChapterEndIndices, allChapterEndIndices.Length + 1);
                    //allBooks[^1] = book;
                    //allBookNames[^1] = bookText;
                    allChapterHashIndices[^1] = nextChapterIndex;
                    if (allChapterHashIndices.Length > 1)
                        allChapterEndIndices[^2] = nextChapterIndex - 1;    // set to 1 before previous chapter's end

                    i = nextChapterIndex;    // advance 'i' to where hash was found; next pass in loop will increment it
                }
                else
                {
                    allChapterEndIndices[^1] = entireTextFile.Length - 1;              // set to end of file
                    break;
                }
            }

            // Do same for verses: 'allVerseHashesIndices'
            for (int i = 0; i < entireTextFile.Length; i++)
            {
                int nextVerseIndex = entireTextFile.IndexOf(VERSE_HASH, i);
                if (nextVerseIndex > -1)
                {
                    allVerseHashesIndices[allVerseHashesIndicesCount++] = nextVerseIndex;
                    i = nextVerseIndex;
                }
                else
                {
                    break;
                }
            }
            Array.Resize(ref allVerseHashesIndices, allVerseHashesIndicesCount);

            // Calculate 'allVerseHashesByBook', which is 'allVerseHashIndices'
            //   broken down by chapter.
            allVerseHashesByBook = new int[allChapterHashIndices.Length][];
            allVerseNumbersByBook = new int[allChapterHashIndices.Length][];

            int firstVerseArrayPtr = 0;  // array ptr into 'allVerseHashesIndices' where next chapter starts
            for (int i = 0; i < allChapterHashIndices.Length; i++)
            {
                int[] verseHashIndices = new int[300];   // trim this later
                int verseHashIndicesInChapterCount = 0;

                int chapterEndIndex = allChapterEndIndices[i];

                // Walk through 'allVerseHashesIndices' starting at
                // 'firstVerseArrayPtr' until you step over next chapter start.
                // When you do, you've got a full set
                while (firstVerseArrayPtr + verseHashIndicesInChapterCount < allVerseHashesIndices.Length)
                {
                    int thisVerseIndex = allVerseHashesIndices[firstVerseArrayPtr + verseHashIndicesInChapterCount];

                    // This verse index within current chapter?
                    if (thisVerseIndex < chapterEndIndex)
                    {
                        verseHashIndices[verseHashIndicesInChapterCount] = thisVerseIndex;
                        verseHashIndicesInChapterCount++;
                    }
                    else
                    {
                        firstVerseArrayPtr += verseHashIndicesInChapterCount;
                        break;
                    }
                }
                Array.Resize(ref verseHashIndices, verseHashIndicesInChapterCount);
                allVerseHashesByBook[i] = verseHashIndices;
            }

            // Calculate 'allFootnoteSectionStartIndices'
            for (int i = 0; i < allChapterHashIndices.Length; i++)
            {
                BibleBooks.NewTestamentBookChapterByOffset(i, out BookEnum book, out int chapterNumber);

                // Find last index in chapter
                int lastIndexInChapter = allChapterEndIndices[i];

                int lastVerseInChapterIndex = allVerseHashesByBook[i][^1];

                // Since we're starting from the last verse, we scan the last verse's content
                // This becomes a problem when the last verse has an [a] or [A].
                int index_1a = entireTextFile.IndexOf("[a]", lastVerseInChapterIndex);
                int index_1A = entireTextFile.IndexOf("[A]", lastVerseInChapterIndex);
                int index_2a = entireTextFile.IndexOf("[a]", index_1a + 1);
                int index_2A = entireTextFile.IndexOf("[A]", index_1A + 1);
                bool has1a = (index_1a != -1) && (index_1a < lastIndexInChapter);
                bool has1A = (index_1A != -1) && (index_1A < lastIndexInChapter);
                bool has2a = (index_2a != -1) && (index_2a < lastIndexInChapter);
                bool has2A = (index_2A != -1) && (index_2A < lastIndexInChapter);

                // Be mindful of non-footnote [a] like in
                // Acts 26: [D]in a little way…Lit: in [a] little. Or: with little. 
                bool acts26Exception = (book == BookEnum.ACTS) && (chapterNumber == 26);
                bool secondCor5Exception = (book == BookEnum.COR2) && (chapterNumber == 5);

                allFootnoteSectionStartIndices[i] = -1;
                if (has2a && !(acts26Exception || secondCor5Exception))
                    allFootnoteSectionStartIndices[i] = index_2a;
                else if (has1a)
                    allFootnoteSectionStartIndices[i] = index_1a;
                else if (has2A)
                    allFootnoteSectionStartIndices[i] = index_2A;
                else if (has1A)
                    allFootnoteSectionStartIndices[i] = index_1A;
                else
                {
                    Utils.LogEntry("{0} Chapter {1}: has no footnote section!", book.ToString(), chapterNumber);
                }
            }

            allFootnotesByBook = new int[allChapterHashIndices.Length][];

            for (int i = 0; i < allChapterHashIndices.Length; i++)
            {
                BibleBooks.NewTestamentBookChapterByOffset(i, out BookEnum book, out int chapterNumber);

                //if ((book == BookEnum.ACTS) && (chapterNumber == 26))
                //{
                //    Utils.LogEntry("Temp marker!!!");
                //}
                int footnoteStartIndex = allFootnoteSectionStartIndices[i];
                int chapterEndIndex = allChapterEndIndices[i];

                string footnoteText = entireTextFile.Substring(footnoteStartIndex, chapterEndIndex - footnoteStartIndex + 1);
                char[] footnoteTextCharArray = footnoteText.ToCharArray();

                allFootnotesByBook[i] = new int[200];
                int allFootnotesByBookCount = 0;

                // Search for start of each individual footnote
                for (int k = 0; k < footnoteTextCharArray.Length - 2; k++)    // -2 is to prevent array bounds overrun
                {
                    char letter = footnoteTextCharArray[k + 1];

                    if ((footnoteTextCharArray[k] == '[') && (footnoteTextCharArray[k + 2] == ']') &&
                        ((letter >= 'A') && (letter <= 'Z') || (letter >= 'a') && (letter <= 'z')))
                    {
                        allFootnotesByBook[i][allFootnotesByBookCount++] = footnoteStartIndex + k;
                    }
                }

                Array.Resize(ref allFootnotesByBook[i], allFootnotesByBookCount);

                if (allFootnotesByBookCount == 0)
                {
                    Utils.LogEntry("{0} Chapter {1}: no footnotes found in footnote section!", book.ToString(), chapterNumber);
                }
            }

            string[] linesByChapterVerse = new string[100000];     // oversize to save CPU cycles; will trim later
            int linesByChapterVerseIndex = 0;

            // Walk through to build text output
            for (int i = 0; i < allChapterHashIndices.Length; i++)
            {
                BibleBooks.NewTestamentBookChapterByOffset(i, out BookEnum book, out int chapterNumber);

                linesByChapterVerse[linesByChapterVerseIndex++] = string.Format("&{0} {1}", book, chapterNumber);

                allVerseNumbersByBook[i] = new int[200];
                int allVerseNumbersByBookCount = 0;

                // Walk through all verses in this chapter only
                for (int k = 0; k < allVerseHashesByBook[i].Length; k++)
                {
                    // We need to go back 1 char to get to verse digit
                    // Ex:
                    // 1$$$$$ index will be after '1'
                    int verseIndex = allVerseHashesByBook[i][k];
                    string verseDigits = entireTextFile.Substring(verseIndex - 1, 1);   // grab the digit
                    int charsConsumed = VERSE_HASH.Length;

                    int nextVerseIndex = -1;
                    if (k + 1 < allVerseHashesByBook[i].Length)     // check that we don't run over end of array
                        nextVerseIndex = allVerseHashesByBook[i][k + 1];  // where in 'entireTextFile' is next verse?

                    // Cover case where you have to consume 2 digits
                    // Ex: 1$$$$$1$$$$$
                    // If next verse number is adjacent to current verse number, then
                    //   it must be the 2nd digit
                    if ((nextVerseIndex != -1) && (nextVerseIndex == verseIndex + VERSE_HASH.Length + 1))
                    {
                        string verseDigit2 = entireTextFile.Substring(nextVerseIndex - 1, 1);
                        verseDigits += verseDigit2;
                        charsConsumed += 1 + VERSE_HASH.Length;   // +1 for 2nd digit of verse num.

                        // Advance 'nextVerseIndex' again to step over 2nd digit
                        nextVerseIndex = -1;
                        if (k + 2 < allVerseHashesByBook[i].Length)     // check that we don't run over end of array
                            nextVerseIndex = allVerseHashesByBook[i][k + 2];  // where in 'entireTextFile' is next verse?

                        k++;    // don't repeat this 2nd digit when we loop around again
                    }

                    if (Int32.TryParse(verseDigits, out int verseNumber) == false)
                    {
                        throw new Exception(string.Format("{0} Chapter {1}:Can't parse verse number {2}", book.ToString(), chapterNumber, verseDigits));
                    }

                    allVerseNumbersByBook[i][allVerseNumbersByBookCount++] = verseNumber;

                    string verseText = "*****";
                    int startIndexAfterHash = verseIndex + charsConsumed;
                    if (nextVerseIndex != -1)
                    {
                        int trailingVerseLength = nextVerseIndex - startIndexAfterHash - 1;
                        verseText = entireTextFile.Substring(startIndexAfterHash, trailingVerseLength);
                    }
                    else
                    {
                        int lengthOfLastVerse = allFootnotesByBook[i][0] - startIndexAfterHash;
                        // Last verse in chapter...just grab 100 chars for now, fix later
                        verseText = entireTextFile.Substring(startIndexAfterHash, lengthOfLastVerse);

                        // Now print all subscripts
                        for (int p = 0; p < allFootnotesByBook[i].Length; p++)
                        {
                            int nextFootnoteIndex = entireTextFile.Length - 1;   // default to end of entire NT
                            if (i + 1 < allChapterHashIndices.Length)            // if there's another chapter, move default up
                                nextFootnoteIndex = allChapterHashIndices[i + 1];
                            if (p + 1 < allFootnotesByBook[i].Length - 1)        // if there's another verse in this chapte, move default up
                                nextFootnoteIndex = allFootnotesByBook[i][p + 1];

                            string footnoteText = entireTextFile.Substring(allFootnotesByBook[i][p],
                                                         nextFootnoteIndex - allFootnotesByBook[i][p] - 1); // -1 is to remove \n
                            linesByChapterVerse[linesByChapterVerseIndex++] = string.Format("*{0}", footnoteText);
                        }

                    }

                    linesByChapterVerse[linesByChapterVerseIndex++] = string.Format("{0}|{1}", verseNumber, verseText);
                }

                Array.Resize(ref allVerseNumbersByBook[i], allVerseNumbersByBookCount);
            }

            Array.Resize(ref linesByChapterVerse, linesByChapterVerseIndex);

#pragma warning disable CS8604
            Utils.ConcatenateFqFileName(VERSES_BY_LINE_OUTPUT_FILE_NAME, OUTPUT_FOLDER, out string fqVersesByLineFileName);
#pragma warning restore
            File.WriteAllLines(fqVersesByLineFileName, linesByChapterVerse);


            // Calculate missing/out-of-order verse numbers
            Utils.LogEntry("**********");
            Utils.LogEntry("**************  Missing/problematic verse number");
            Utils.LogEntry("**********");

            for (int i = 0; i < allChapterHashIndices.Length; i++)
            {
                BibleBooks.NewTestamentBookChapterByOffset(i, out BookEnum book, out int chapterNumber);

                int previousVerseNumber = 0;
                for (int k = 0; k < allVerseNumbersByBook[i].Length; k++)
                {
                    int thisVerseNumber = allVerseNumbersByBook[i][k];
                    if ((previousVerseNumber + 1) != thisVerseNumber)
                    {
                        Utils.LogEntry("{0} {1}: Verse {2} problem", book.ToString(), chapterNumber, thisVerseNumber);
                    }

                    previousVerseNumber = thisVerseNumber;
                }
            }

            // Sanity check footnotes

            Utils.LogEntry("**********");
            Utils.LogEntry("**************  Footnotes ");
            Utils.LogEntry("**********");
            
            for (int i = 0; i < allChapterHashIndices.Length; i++)
            {
                BibleBooks.NewTestamentBookChapterByOffset(i, out BookEnum book, out int chapterNumber);

                if ((book == BookEnum.JOHN) && (chapterNumber == 16))
                {
                    Utils.LogEntry("Temp marker!!!");
                }

                Dictionary<char, FootnoteInfo> perChapterFootnotes = new Dictionary<char, FootnoteInfo>(50);   // key=footnote letter (case sensitive)

                char lastFootnoteLetter = (char)('a' - 1);
                for (int k = 0; k < allFootnotesByBook[i].Length; k++)
                {
                    // Do footnotes in footnote section
                    //Ex:    [a]you marvelous godsend...Lit: You descendent of David. Ref. note of Matt. 12:23.
                    //       [b]Jesus...The name Jesus in Hebrew means deliverer or rescuer
                    int thisFootnoteIndex = allFootnotesByBook[i][k];
                    int footnoteTextStartIndex = thisFootnoteIndex + "[a]".Length;
                    char footnoteLetter = entireTextFile[thisFootnoteIndex + 1];    // +1 to skip over [

                    if ((footnoteLetter != (lastFootnoteLetter + 1)) && (footnoteLetter != 'A'))
                    {
                        Utils.LogEntry("{0} {1}: Footnote {2} out of sequence/missing/suspicious", book.ToString(), chapterNumber, footnoteLetter);
                    }

                    if (perChapterFootnotes.ContainsKey(footnoteLetter))
                    {
                        Utils.LogEntry("{0} {1}: Footnote {1} duplicated chapter text", book.ToString(), chapterNumber, footnoteLetter);
                    }
                    else
                    {
                        // 'nextFootnoteIndex' is start index of next footnote or end of chapter,
                        //    whichever comes first
                        int nextFootnoteIndex = entireTextFile.Length - 1;
                        if (i + 1 < allChapterHashIndices.Length)
                            nextFootnoteIndex = allChapterHashIndices[i + 1];
                        if (k + 1 < allFootnotesByBook[i].Length)
                            nextFootnoteIndex = allFootnotesByBook[i][k + 1];

                        int ellipsesIndex = entireTextFile.IndexOf("...", footnoteTextStartIndex);

                        string quotedText;
                        if ((ellipsesIndex == -1) || (ellipsesIndex >= nextFootnoteIndex))
                        {
                            // Not all footnotes have quoted text?
                            quotedText = "[No quoted text for this footnote]";
                        }
                        else
                        {
                            quotedText = entireTextFile.Substring(footnoteTextStartIndex, ellipsesIndex - footnoteTextStartIndex);
                        }

                        FootnoteInfo entry = new FootnoteInfo
                        {
                            ArrayPtr = k,
                            Index = thisFootnoteIndex,
                            QuotedText = quotedText,
                        };
                        perChapterFootnotes.Add(footnoteLetter, entry);
                    }

                    lastFootnoteLetter = footnoteLetter;
                }

                // Scan chapter text and sanity check
                int chapterStartIndex = allVerseHashesByBook[i][0];
                int footnoteStartIndex = allFootnoteSectionStartIndices[i];

                string entireChapter = entireTextFile.Substring(chapterStartIndex, footnoteStartIndex - chapterStartIndex);
                char[] entireChapterChar = entireChapter.ToCharArray();

                for (int k = 0; k <  entireChapterChar.Length - 2; k++)   // -2 to prevent array overrun
                {
                    char potentialLeftBracket = entireChapterChar[k];
                    char potentialFootnoteLetter = entireChapterChar[k+1];
                    char potentialRightBracket = entireChapterChar[k+2];
                    if ((potentialLeftBracket == '[') && (potentialRightBracket == ']'))
                    {
#pragma warning disable CS8600
                        if (!perChapterFootnotes.TryGetValue(potentialFootnoteLetter, out FootnoteInfo entry))
#pragma warning restore
                        {
                            Utils.LogEntry("{0} {1}: No footnote for reference {2} found in text", book.ToString(), chapterNumber, potentialFootnoteLetter);
                        }
                        else if (entry.QuotedText.Length > 0)
                        {
                            // sanity check text
                            int estimatedFootnoteInVersesStart = k - entry.QuotedText.Length;
                            if (estimatedFootnoteInVersesStart < 0)
                                estimatedFootnoteInVersesStart = 0;
                            int estimatedFootnoteLength = entry.QuotedText.Length;
                            string footnoteAsInVerses = entireChapter.Substring(estimatedFootnoteInVersesStart, estimatedFootnoteLength);

                            if (footnoteAsInVerses != entry.QuotedText)
                            {
                                //Utils.LogEntry("{0} {1}: Footnote {2} text mismatch, verses vs. quoted: {3} vs. {4}",
                                //                  book.ToString(), chapterNumber,
                                //                  potentialFootnoteLetter, footnoteAsInVerses, entry.QuotedText);
                            }
                        }
                    }
                }

            }


        }


    }
}
