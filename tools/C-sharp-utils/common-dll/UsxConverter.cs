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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;


namespace common_dll
{
    public static partial class UsxConverter
    {

        public static string[]? USX_COPYRIGHT_TEXT;
        public static string? USX_TRANSLATION_NAME;
        public static string? USX_TRANSLATION_VERSION;

        //*********
        //*************  USX XML Generation
        //*********

        // ex outputs:
        //    "<char style="w" strong="G05485">gracious</char>"
        //    "<char style="w" strong="G05485:a">gracious</char>"
        //
        // Where 'displayWord' would be 'gracious'
        //  and for 2nd example, 'postStrongsNumberLetter' would be 'a'
        // (Set 'hasPostStrongsNumberLetter' to 'false' for no post letter
        private static string UsxCharEncode_StrongsReference(string displayWord,
                                                             bool isHebrewNotGreek,
                                                             int strongsNumber,
                                                             bool hasPostStrongsNumberLetter,
                                                             char postStrongsNumberLetter)
        {
            string usxStrongsEncoding = string.Format("<char style=\"w\" strong=\"{0}{1,5:D5}{2}\">{3}</char>",
                   isHebrewNotGreek ? "H" : "G",
                   strongsNumber,
                   hasPostStrongsNumberLetter ? string.Format(":{0}", postStrongsNumberLetter) : "",
                   displayWord);

            return usxStrongsEncoding;
        }

        private static bool IsAVerseWithNoContent(ref string[] verseLines)
        {
            if (verseLines == null || verseLines.Length == 0)
                return true;

            if (verseLines[0].Length == 0)
                return true;

            return false;
        }

        public static void GenerateUsxXml(bool makeUnabridgedEdition, string outputDirectory)
        {
            string directoryName;
            if (makeUnabridgedEdition)
            {
                directoryName = outputDirectory;
                Utils.LogEntry("Writing output files (unabridged edition) to {0}", directoryName);
            }
            else
            {
                directoryName = outputDirectory;
                Utils.LogEntry("Writing output files (abridged edition) to {0}", directoryName);
            }

            if (!Path.Exists(directoryName))
            {
                Utils.LogEntry("Directory {0} doesn't exist!", directoryName);
            }

            for (int i = 0; i < AllBooks.Length; i++)
            {
                BookEnum thisBookEnum = AllBooks[i].BookEnum;
                GenerateUsxForSingleBook(i, makeUnabridgedEdition, directoryName);
            }
        }


        //****   Global variables
#pragma warning disable CS8618
        private static string[] Indent;    // indentations
        private static string[] Line;
        private static int N;
        private static int Lvl;
#pragma warning restore


        private static void GenerateUsxForSingleBook(int bookIndex, bool unabridged, string baseDirectory)
        {
            BookEnum bookEnum = AllBooks[bookIndex].BookEnum;
            string usxStyleBookAbbrev = UsxDefinitions.BibleBookNamesText[(int)bookEnum];
            string usxFileName = string.Format("{0}.usx", usxStyleBookAbbrev);
            Utils.ConcatenateFqFileName(usxFileName, baseDirectory, out string usxFqFileName);

            Indent = Utils.BuildIndentationArray();

            Lvl = 0;     // current indentation level. Index in 'indentation'
            Line = new string[100000];
            N = 0;

            Line[N++] = "<?xml version=\"1.0\" encoding=\"utf-8\"?>";

#pragma warning disable CS8600
            string[] copyrightLines = USX_COPYRIGHT_TEXT;
            string translationName = USX_TRANSLATION_NAME;
#pragma warning restore

            string toc1Text;
            if (BibleBooks.IsGospel(bookEnum))
            {
                toc1Text = "The Gospel of";
            }
            else if (BibleBooks.IsEpistle(bookEnum))
            {
                toc1Text = "The Epistle of";
            }
            else
            {
                toc1Text = "The Book of";
            }


#pragma warning disable CS8600, CS8602
            for (int i = 0; i < copyrightLines.Length; i++)
                Line[N++] = string.Format("{0}<!-- {1} -->", Indent[Lvl], copyrightLines[i]);
            Line[N++] = string.Format("{0}<!-- {1} Version {2} -->", Indent[Lvl], USX_TRANSLATION_NAME, USX_TRANSLATION_VERSION);
#pragma warning restore

            Line[N++] = string.Format("{0}<usx version=\"3.0\">", Indent[Lvl]);

            Lvl++;
            Line[N++] = string.Format("{0}<!--   *******************   -->", Indent[Lvl]);
            Line[N++] = string.Format("{0}<!--   Titles and TOC Info   -->", Indent[Lvl]);
            Line[N++] = string.Format("{0}<!--   *******************   -->", Indent[Lvl]);
            Line[N++] = string.Format("{0}<book code=\"{1}\" style=\"id\">{2}</book>", Indent[Lvl], usxStyleBookAbbrev, translationName);
            Line[N++] = string.Format("{0}<para style=\"h\">{1}</para>", Indent[Lvl], BibleBooks.AllBooksFullNames[(int)bookEnum]);
            Line[N++] = string.Format("{0}<para style=\"toc1\">{1} {2}</para>", Indent[Lvl], toc1Text, BibleBooks.AllBooksFullNames[(int)bookEnum]);
            Line[N++] = string.Format("{0}<para style=\"toc2\">{1}</para>", Indent[Lvl], BibleBooks.AllBooksFullNames[(int)bookEnum]);
            Line[N++] = string.Format("{0}<para style=\"toc3\">{1}</para>", Indent[Lvl], BibleBooks.AllBooksAbbrev[(int)bookEnum]);
            Line[N++] = string.Format("{0}<para style=\"mt2\">{1}</para>", Indent[Lvl], toc1Text.ToUpper());
            Line[N++] = string.Format("{0}<para style=\"mt1\">{1}</para>", Indent[Lvl], BibleBooks.AllBooksFullNames[(int)bookEnum].ToUpper());

            for (int chapterIndex = 0; chapterIndex < AllBooks[bookIndex].Chapters.Length; chapterIndex++)
            {
                ChapterInfo chapterCopy = AllBooks[bookIndex].Chapters[chapterIndex];

                if (unabridged && chapterCopy.IsIntroductionNotChapter)
                {
                    Line[N++] = string.Format("{0}<!--   ************   -->", Indent[Lvl]);
                    Line[N++] = string.Format("{0}<!--   Introduction   -->", Indent[Lvl]);
                    Line[N++] = string.Format("{0}<!--   ************   -->", Indent[Lvl]);
                    Line[N++] = string.Format("{0}<para style=\"imt\">Introduction</para>", Indent[Lvl]);

                    //Lvl++;
                    for (int paraIndex = 0; paraIndex < chapterCopy.IntroductionParagraphs.Length; paraIndex++)
                    {
                        string[] content;
                        if (unabridged)
                            content = chapterCopy.IntroductionParagraphs[paraIndex].MultiLineText_Unabridged;
                        else
                            content = chapterCopy.IntroductionParagraphs[paraIndex].MultiLineText;

                        bool isSpace = IsAVerseWithNoContent(ref content);

                        MarkerEnum style = chapterCopy.IntroductionParagraphs[paraIndex].ParagraphFormatting;
                        // Introduction paragraph styles
                        //  ipi: introduction, 1st line indented prose
                        //  imi: introduction, no 1st line indentation
                        //  ip:  prose
                        //  ib:  blank line
                        //  iq:  poetry   (best usx can offer for indented paragraphs in introductions)
                        //  ie:  ending marker
                        string styleAttrib;
                        if (isSpace)
                            styleAttrib = "ib";
                        else if (style == MarkerEnum.NON_VERSE_CONTENT_INDENTED)
                            styleAttrib = "iq";
                        else if (style == MarkerEnum.NON_VERSE_CONTENT)   // or, if (style == MarkerEnum.NON_VERSE_CONTENT) or invalid
                            styleAttrib = "ipi";
                        else
                        {
                            Utils.LogEntry("{0} Introduction para {1}: Unsupported style {2}", bookEnum.ToString(), paraIndex, style.ToString());
                            styleAttrib = "ipi";       // default for a soft landing
                        }

                        if (isSpace)
                            Line[N++] = string.Format("{0}<para style=\"{1}\"></para>", Indent[Lvl], styleAttrib);
                        else if (content.Length == 1)
                            Line[N++] = string.Format("{0}<para style=\"{1}\">{2}</para>", Indent[Lvl], styleAttrib, content[0]);
                        else
                        {
                            Line[N++] = string.Format("{0}<para style=\"{1}\">{2}", Indent[Lvl], styleAttrib, content[0]);
                            Lvl++;
                            for (int i = 1; i < content.Length; i++)
                            {
                                if (i < content.Length - 1)    // not last?
                                    Line[N++] = string.Format("{0}{1}", Indent[Lvl], content[i]);
                                else                           // last?
                                    Line[N++] = string.Format("{0}{1}</para>", Indent[Lvl], content[i]);
                            }
                            Lvl--;
                        }
                    }
                    //Lvl--;

                    //Line[N++] = string.Format("{0}</para>", Indent[Lvl]);   // closes <para style="imt">
                }

                else if (!chapterCopy.IsIntroductionNotChapter)
                {
                    Line[N++] = string.Format("{0}<!--   **********   -->", Indent[Lvl]);
                    Line[N++] = string.Format("{0}<!--   Chapter {1,2}   -->", Indent[Lvl], chapterCopy.ChapterNumber);
                    Line[N++] = string.Format("{0}<!--   **********   -->", Indent[Lvl]);
                    Line[N++] = string.Format("{0}<chapter number=\"{1}\" style=\"c\" sid=\"{2} {1}\" />", Indent[Lvl], chapterCopy.ChapterNumber, usxStyleBookAbbrev);

                    Lvl++;
                    for (int verseIndex = 0; verseIndex < chapterCopy.Verses.Length; verseIndex++)
                    {
                        int thisParagraphIndex = chapterCopy.Verses[verseIndex].ParagraphIndex;

                        int additionalNumberOfVerses = 0;
                        for (int k = verseIndex + 1; k < chapterCopy.Verses.Length; k++)
                        {
                            int nextParagraphIndex = chapterCopy.Verses[k].ParagraphIndex;
                            if (thisParagraphIndex == nextParagraphIndex)
                            {
                                additionalNumberOfVerses++;
                            }
                            else
                            {
                                break;
                            }
                        }
                        int spanLength = additionalNumberOfVerses + 1;

                        PopulateParagraph(bookEnum, ref chapterCopy, verseIndex, spanLength, unabridged);

                        // Skip over verses covered by this span
                        verseIndex += additionalNumberOfVerses;
                    }
                    Lvl--;

                    // Some footnote examples (not quite relevant)
                    // From https://github.com/ubsicap/usx/blob/usx3.0.7/docs/notes.rst
                    //
                    //<note caller="°" style="f">
                    //        <char style="fr" closed="false">4,1 </char><char style="fw" closed="false">B Δ 700</char>
                    //</note>
                    //<note caller="+" style="f">
                    //  <char style="fr">7.38: </char>
                    //  <char style="ft">Jesus' words in verses 37-38 may be translated: </char>
                    //  <char style="fqa">“Whoever is thirsty should come to me and drink. </char>
                    //  <char style="fv">38</char> As the scripture says, ‘Streams of life-giving water
                    //  will pour out from within anyone who believes in me.’”
                    //</note>

                    if (unabridged)
                    {
                        if (chapterCopy.Footnotes != null && chapterCopy.Footnotes.Length > 0)
                        {
                            Line[N++] = string.Format("{0}<!--   .........   -->", Indent[Lvl]);
                            Line[N++] = string.Format("{0}<!--   Footnotes   -->", Indent[Lvl]);
                            Line[N++] = string.Format("{0}<!--   .........   -->", Indent[Lvl]);
                            Lvl++;

                            for (int footnoteIndex = 0; footnoteIndex < chapterCopy.Footnotes.Length; footnoteIndex++)
                            {
                                string footnoteLetter = chapterCopy.Footnotes[footnoteIndex].FootnoteLetter;

                                if (chapterCopy.Footnotes[footnoteIndex].Paragraph == null)
                                {
                                    Utils.LogFatal("Book {0} chapter {1} footnote [{2}] has no paragraphs!",
                                           bookEnum.ToString(), chapterCopy.ChapterNumber, footnoteLetter);
                                }

                                Line[N++] = string.Format("{0}<note caller=\"[{1}]\" style=\"{2}\">", Indent[Lvl], footnoteLetter, "f");

                                Lvl++;
                                // Ref "fk" in https://github.com/ubsicap/usx/blob/usx3.0.7/docs/notes.rst
                                Line[N++] = string.Format("{0}<char style=\"fk\">[{1}]</char>", Indent[Lvl], footnoteLetter);

                                int numParagraphs = chapterCopy.Footnotes[footnoteIndex].Paragraph.Length;
                                for (int paraIndex = 0; paraIndex < numParagraphs; paraIndex++)
                                {
                                    string[] content = chapterCopy.Footnotes[footnoteIndex].Paragraph[paraIndex].MultiLineText;

                                    bool noContent = IsAVerseWithNoContent(ref content);
                                    if (noContent && paraIndex == 0)
                                    {
                                        Utils.LogFatal("Book {0} chapter {1} footnote [{2}] has no content!",
                                               bookEnum.ToString(), chapterCopy.ChapterNumber, footnoteLetter);
                                    }
                                    if (noContent)
                                    {
                                        // fp= Footnote additional paragraph.
                                        // If this doesn't work, put in an <optbreak /> instead. 
                                        Line[N++] = string.Format("{0}<char style=\"fp\"></char>", Indent[Lvl]);
                                        continue;
                                    }

                                    if (paraIndex == 0)      // first?
                                    {
                                        // no action
                                    }
                                    else                     // all others?
                                    {
                                        // fp= Footnote additional paragraph.
                                        // If this doesn't work, put in an <optbreak /> instead. 
                                        Line[N++] = string.Format("{0}<char style=\"fp\"></char>", Indent[Lvl]);
                                    }

                                    if (content.Length == 1)
                                    {
                                        Line[N++] = string.Format("{0}<char style=\"ft\">{1}</char>", Indent[Lvl], content[0]);
                                    }
                                    else
                                    {
                                        Line[N++] = string.Format("{0}<char style=\"ft\">{1}", Indent[Lvl], content[0]);

                                        Lvl++;
                                        for (int i = 1; i < content.Length; i++)
                                        {
                                            if (i < content.Length - 1)   // not last?
                                                Line[N++] = string.Format("{0}{1}", Indent[Lvl], content[i]);
                                            else                          // last?
                                                Line[N++] = string.Format("{0}{1}</char>", Indent[Lvl], content[i]);
                                        }
                                        Lvl--;
                                    }
                                }

                                Lvl--;
                                Line[N++] = string.Format("{0}</note>", Indent[Lvl]);
                            }
                            Lvl--;
                        }
                        else
                        {
                            Line[N++] = string.Format("{0}<!--   ...............................   -->", Indent[Lvl]);
                            Line[N++] = string.Format("{0}<!--   (No footnotes for this chapter)   -->", Indent[Lvl]);
                            Line[N++] = string.Format("{0}<!--   ...............................   -->", Indent[Lvl]);
                        }
                    }


                    Line[N++] = string.Format("{0}<chapter eid=\"{1} {2}\" />", Indent[Lvl], usxStyleBookAbbrev, chapterCopy.ChapterNumber);
                }
            }

            Lvl--;
            Line[N++] = string.Format("{0}</usx>", Indent[Lvl]);

            Array.Resize(ref Line, N);

            Utils.WriteFileLines(usxFileName, usxFqFileName, ref Line, true);
        }


        // Get USX paragraph formatting attribute code for this MS Word style type
        //   ex: the "q" in <para style="q1" vid="GEN 2:23">
        // 'retrieveBlankLineInstead' means that the scanner found a paragraph which is a blank line,
        //   and to apply the blank line formatting instead
        private static void ParagraphStyleTranslation(MarkerEnum style, bool retrieveBlankLineInstead, out string styleAttrib,
                                                      BookEnum bookEnum, int chapterNumber, int verseNumber)
        {
            // Verse-content paragraph styles
            //  p: text, first-line indented prose
            //  pi: indented paragraph (all lines indented) (NOTE: pi == pi1)
            //  m: text, no first-line indentation
            //  b:  blank line; extra whitespace between paragraphs
            // Other/not-used:
            //  q:  poetry, single-indentation scheme
            //  q1/q2/q3: poetry, multi-level indentation schemes
            //  sd:  semantic division (large vertical space break); to divide into divisions
            //  mi: indented flush left paragraph
            //  pc: centered paragraph
            if (retrieveBlankLineInstead && style == MarkerEnum.SPACER_INTER_PROSE)
                styleAttrib = "pi";
            else if (retrieveBlankLineInstead)   // mostly expect style == MarkerEnum.VERSES_PROSE
                styleAttrib = "p";
            else if (style == MarkerEnum.VERSES_NARRATIVE)
                styleAttrib = "p";
            else if (style == MarkerEnum.VERSES_NON_INDENTED_NARRATIVE)
                styleAttrib = "m";
            else if (style == MarkerEnum.VERSES_PROSE)
                styleAttrib = "pi";
            else if (style == MarkerEnum.SPACER_INTER_PROSE)
                styleAttrib = "pi";
            else
            {
                styleAttrib = "p";
                Utils.LogEntry("{0} chapter {1} v{2} Unsupported paragraph style {3}",
                    bookEnum, chapterNumber, verseNumber, style.ToString());
            }
        }

        // From: https://ubsicap.github.io/usx/elements.html#verse
        //
        //<chapter number="1" style="c" sid="GEN 1" />
        //<para style="s">The Story of Creation</para>
        //...
        //<para style="p">
        //  <verse number="21" style="v" sid="GEN 2:21" />Then the <char style="nd">Lord</char> God
        //  made the man fall into a deep sleep, and while he was sleeping, he took out one of the
        //  man's ribs and closed up the flesh.<verse eid="GEN 2:21" />
        //  <verse number="22" style="v" sid="GEN 2:22" />He formed a woman out of the rib and brought
        //  her to him.<verse eid="GEN 2:22" />
        //  <verse number="23" style="v" sid="GEN 2:23" />Then the man said,
        //</para>
        //<para style="q1" vid="GEN 2:23">“At last, here is one of my own kind—</para>
        //<para style="q1" vid="GEN 2:23">Bone taken from my bone, and flesh from my flesh.</para>
        //<para style="q1" vid="GEN 2:23">‘Woman’ is her name because she was taken out of man.”
        //  <verse eid="GEN 2:23" />
        //</para>
        //<para style="m">
        //  <verse number="24" style="v" sid="GEN 2:24" />That is why a man leaves his father and mother
        //  and is united with his wife, and they become one.<verse eid="GEN 2:24" />
        //</para>
        //<para style="p">
        //  <verse number="25" style="v" sid="GEN 2:25" />The man and the woman were both naked, but
        //  they were not embarrassed.<verse eid="GEN 2:25" />
        //</para>
        //...
        //<chapter eid="GEN 1" />
        //<chapter number="2" style="c" sid="GEN 2" />
        //

        // from https://github.com/ubsicap/usx/blob/usx3.0.7/docs/elements.rst
        //<para style="p">
        //  <verse number="22" style="v" sid="ACT 17:22" />Paul stood up in front of the city council
        //  and said, <ms style="qt1-s" who="Paul"/>“I see that in every way you Athenians are very
        //  religious.<verse eid="ACT 17:22" />
        //  <verse number="23" style="v" sid="ACT 17:23" />For as I walked through your city ...
        //  ...
        //  <verse number="27" style="v" sid="ACT 17:27" />He did this so that they would look for him,
        //  and perhaps find him as they felt around for him. Yet God is actually not far from any one of us;
        //  <verse eid="ACT 17:27" /> <verse number="28" style="v"/>as someone has said,</para>
        //<para style="q1" vid="ACT 17:28" /><ms style="qt2-s" who="someone" />‘In him we live and move
        //  and exist.’<ms style="qt2-e" />
        //<para style="b" />
        //<para style="m" vid="ACT 17:28" />It is as some of your poets have said,</para>
        //<para style="q1" vid="ACT 17:28"><ms style="qt2-s" who="poets"/>‘We too are his children.’
        //  <ms style="qt2-e"/><verse eid="ACT 17:28" />
        //</para>
        //...
        //  <verse number="31" style="v" sid="ACT 17:31" />For he has fixed a day in which he will judge
        //  the whole world with justice by means of a man he has chosen. He has given proof of this to
        //  everyone by raising that man from death!”<ms style="qt1-e"/><verse eid="ACT 17:31" />
        //</para>

        // From American Standard Version, 1 Chronicles, Chapter 1
        //
        //<para style="p" vid="1CH 1:51">And the chiefs of Edom were: chief Timna,
        //        chief Aliah, chief Jetheth, <verse eid="1CH 1:51" /><verse number="52" style="v" sid="1CH 1:52" />
        //        [[blah-blah-blah]]
        //        <verse number="54" style="v" sid="1CH 1:54" />chief Magdiel, chief Iram. These are the chiefs of Edom.<verse eid="1CH 1:54" />
        //</para>

        //                      Rules for marking up verses
        //                      ---------------------------
        //  -- Use the opening xml element <para style="xyz"> for paragraphs which
        //     begin with a verse number
        //  -- Use the opening element <para style="xyz" vid="123"> for paragraphs which
        //     don't begin with a verse number, but are continuations from
        //     the previous paragraph
        //  -- All verses must have a single start and end verse tag, in other words,
        //     a <verse number="27" style="v" sid="ACT 17:27" /> and a
        //     <verse eid="ACT 17:28" />
        //  -- For a given verse, the start tag must occur before the end tag
        //  -- All start and end tags must appear as tags under a paragraph element.
        //     Paragraph elements must not appear as tags under a verse tags
        //
        //     Right way:
        //         <para style="p">
        //             <verse number="1" style="v" sid="GEN 1:1" />In the beginning<verse eid="GEN 1:1" />
        //         </para>
        //
        //     Wrong way:
        //         <verse number="1" style="v" sid="GEN 1:1" />
        //             <para style="p">In the beginning</para>
        //         <verse eid="GEN 1:1" />
        //

        // Caution!
        //   Do not start or end text for verses without being butted up
        //   against a tag. Not being up against a tag will cause the xml
        //   parser in the app which reads this to drop leading or trailing
        //   space chars.
        //   Same would apply for text in introductory paragraphs and
        //   footnote paragraphs.
        //
        //   Right ways:
        //     <verse number="1" style="v" sid="GEN 1:1" />In the beginning God created the heavens and the earth.<verse eid="GEN 1:1" />
        //
        //     <verse number="1" style="v" sid="GEN 1:1" />In the beginning
        //         God created
        //         the heavens and the earth.<verse eid="GEN 1:1" />
        //
        //   Wrong ways:
        //     <verse number="1" style="v" sid="GEN 1:1" />
        //          In the beginning God created the heavens and the earth.<verse eid="GEN 1:1" />
        //
        //     <verse number="1" style="v" sid="GEN 1:1" />In the beginning God created
        //          the heavens and the earth.<verse eid="GEN 1:1" />
        //
        //     <verse number="1" style="v" sid="GEN 1:1" />
        //          In the beginning God creatd the heavens and the earth.
        //     <verse eid="GEN 1:1" />

        // Write an entire paragraph, for a paragraph which has > 1 verse,
        //   even if some of these verses are started or are ended on other paragraphs.
        // This will not write the encapsulating paragraph tags, just the verse
        //   content below it.


        private static void PopulateParagraph(BookEnum bookEnum,
                                              ref ChapterInfo chapterCopy,
                                              int verseStartIndex,
                                              int numberOfVerseEntries,
                                              bool unabridged)
        {
            string usxStyleBookAbbrev = UsxDefinitions.BibleBookNamesText[(int)bookEnum];
            MarkerEnum style = chapterCopy.Verses[verseStartIndex].ParagraphFormatting;
            int chapterNumber = chapterCopy.ChapterNumber;
            int paragraphIndex = chapterCopy.Verses[verseStartIndex].ParagraphIndex;
            int startVerseNumber = chapterCopy.Verses[verseStartIndex].VerseNumber;

            //bool blankSpace = style == MarkerEnum.SPACER_INTER_PROSE || style == MarkerEnum.SPACER_AFTER_PROSE;  // SPACER_AFTER_PROSE isn't used
            bool blankSpace = chapterCopy.Verses[verseStartIndex].VerseNumber == SPACER_VERSE_MAGIC_NUMBER;
            ParagraphStyleTranslation(style, blankSpace, out string styleAttrib,
                                      bookEnum, chapterNumber, startVerseNumber);

            bool startVerseContinuedFromPrevious = chapterCopy.Verses[verseStartIndex].ContinuedFromPrevious;
            bool startVerseContinuedToNext = chapterCopy.Verses[verseStartIndex].ContinuedToNext;

            string[] startContent;
            if (unabridged)
                startContent = chapterCopy.Verses[verseStartIndex].MultiLineText_Unabridged;
            else
                startContent = chapterCopy.Verses[verseStartIndex].MultiLineText;
            bool noStartContent = IsAVerseWithNoContent(ref startContent);
            bool noStartWrap = !noStartContent && startContent.Length == 1;  // first verse fits on single line

            if (blankSpace)
            {
                if (!noStartContent)
                {
                    Utils.LogEntry("{0} chapter {1} v{2} is a space line, but has content. Dropping content.", bookEnum.ToString(), chapterNumber, startVerseNumber);
                }
                if (numberOfVerseEntries > 1)
                {
                    Utils.LogEntry("{0} chapter {1} v{2} is a space line, but has multiple verses. Dropping extra verses.?", bookEnum.ToString(), chapterNumber, startVerseNumber);
                }

                Line[N++] = string.Format("{0}<para style=\"{1}\" />    <!-- Spacer line. No verse associated with it. -->", Indent[Lvl], styleAttrib);

                return;
            }

            for (int i = verseStartIndex; i < verseStartIndex + numberOfVerseEntries; i++)
            {
                if (chapterCopy.Verses[i].VerseNumber == SPACER_VERSE_MAGIC_NUMBER)
                {
                    Utils.LogFatal("Book {0} chapter {1} has an unexpected spacer verse!",
                             bookEnum.ToString(), chapterNumber);
                }
            }

            bool requiresClosingParagraphTag = true;

            // Finish off dangling verse from previous paragraph
            if (startVerseContinuedFromPrevious)
            {
                if (startVerseContinuedToNext && noStartContent)
                {
                    Line[N++] = string.Format("{0}<para style=\"{1}\" vid=\"{2} {3}:{4}\"></para>", Indent[Lvl],
                                  styleAttrib, usxStyleBookAbbrev, chapterNumber, startVerseNumber);
                    requiresClosingParagraphTag = false;
                }
                else if (startVerseContinuedToNext && noStartWrap && startContent.Length == 1)
                {
                    Line[N++] = string.Format("{0}<para style=\"{1}\" vid=\"{2} {3}:{4}\">{5}</para>",
                                  Indent[Lvl], styleAttrib, usxStyleBookAbbrev, chapterNumber, startVerseNumber, startContent[0]);
                    requiresClosingParagraphTag = false;
                }
                else if (startVerseContinuedToNext)
                {
                    Line[N++] = string.Format("{0}<para style=\"{1}\" vid=\"{2} {3}:{4}\">{5}",
                                  Indent[Lvl], styleAttrib, usxStyleBookAbbrev, chapterNumber, startVerseNumber, startContent[0]);

                    Lvl += 2;                    // line up with indentation of verse tag follow-on lines
                    for (int i = 1; i < startContent.Length; i++)
                    {
                        Line[N++] = string.Format("{0}{1}", Indent[Lvl], startContent[i]);
                    }
                    Lvl -= 2;
                }
                else if (startContent.Length == 0 && numberOfVerseEntries == 1)
                {
                    // A blank-space line
                    Line[N++] = string.Format("{0}<para style=\"{1}\" vid=\"{2} {3}:{4}\"><verse eid=\"{2} {3}:{4}\"/></para>",
                                  Indent[Lvl], styleAttrib, usxStyleBookAbbrev, chapterNumber, startVerseNumber);
                    requiresClosingParagraphTag = false;
                }
                else if (startContent.Length == 1)
                {
                    Line[N++] = string.Format("{0}<para style=\"{1}\" vid=\"{2} {3}:{4}\">{5}<verse eid=\"{2} {3}:{4}\"/>",
                                  Indent[Lvl], styleAttrib, usxStyleBookAbbrev, chapterNumber, startVerseNumber, startContent[0]);
                }
                else
                {
                    Line[N++] = string.Format("{0}<para style=\"{1}\" vid=\"{2} {3}:{4}\">{5}",
                                  Indent[Lvl], styleAttrib, usxStyleBookAbbrev, chapterNumber, startVerseNumber, startContent[0]);

                    Lvl += 2;                    // line up with indentation of verse tag follow-on lines
                    for (int i = 1; i < startContent.Length; i++)
                    {
                        if (i < startContent.Length - 1)      // not last?
                            Line[N++] = string.Format("{0}{1}",
                                          Indent[Lvl], startContent[i]);
                        else                                  // last?
                            Line[N++] = string.Format("{0}{1}<verse eid=\"{2} {3}:{4}\"/>",
                                          Indent[Lvl], startContent[i], usxStyleBookAbbrev, chapterNumber, startVerseNumber);
                    }
                    Lvl -= 2;
                }
            }
            // Paragraph has no dangling verse
            else
            {
                if (startVerseContinuedToNext)
                {
                    if (numberOfVerseEntries != 1)
                    {
                        Utils.LogFatal("Book {0} chapter {1} v{2} continues, but how? There's {3} verses in this paragraph!",
                                 bookEnum.ToString(), chapterNumber, startVerseNumber, numberOfVerseEntries);
                    }

                    Line[N++] = string.Format("{0}<para style=\"{1}\">", Indent[Lvl], styleAttrib, startVerseNumber);
                }
                else
                {
                    Line[N++] = string.Format("{0}<para style=\"{1}\">", Indent[Lvl], styleAttrib, startVerseNumber);
                }
            }

            // Exclude first verse if it was continued from previous or is just a space;
            //   otherwise, loop through all verses
            int firstVerseInLoop = verseStartIndex;
            int loopLength = numberOfVerseEntries;
            if (startVerseContinuedFromPrevious || blankSpace)
            {
                firstVerseInLoop++;
                loopLength--;
            }

            Lvl++;
            for (int verseIndex = firstVerseInLoop; verseIndex < firstVerseInLoop + loopLength; verseIndex++)
            {
                VerseInfo verse = chapterCopy.Verses[verseIndex];

                string[] content;
                if (unabridged)
                    content = verse.MultiLineText_Unabridged;
                else
                    content = verse.MultiLineText;
                bool noContent = IsAVerseWithNoContent(ref content);

                if (noContent)
                {
                    Line[N++] = string.Format("{0}<verse number=\"{1}\" style=\"v\" sid=\"{2} {3}:{1}\" /><verse eid=\"{2} {3}:{1}\" />",
                        Indent[Lvl], verse.VerseNumber, usxStyleBookAbbrev, chapterNumber);
                }
                else if (verse.ContinuedToNext && content.Length == 1)
                {
                    Line[N++] = string.Format("{0}<verse number=\"{1}\" style=\"v\" sid=\"{2} {3}:{1}\" />{4}",
                        Indent[Lvl], verse.VerseNumber, usxStyleBookAbbrev, chapterNumber, content[0]);
                }
                else if (content.Length == 1)
                {
                    Line[N++] = string.Format("{0}<verse number=\"{1}\" style=\"v\" sid=\"{2} {3}:{1}\" />{4}<verse eid=\"{2} {3}:{1}\" />",
                        Indent[Lvl], verse.VerseNumber, usxStyleBookAbbrev, chapterNumber, content[0]);
                }
                else
                {
                    Line[N++] = string.Format("{0}<verse number=\"{1}\" style=\"v\" sid=\"{2} {3}:{1}\" />{4}",
                        Indent[Lvl], verse.VerseNumber, usxStyleBookAbbrev, chapterNumber, content[0]);

                    Lvl++;
                    for (int i = 1; i < content.Length; i++)
                    {
                        if (i < content.Length - 1)      // not last?
                            Line[N++] = string.Format("{0}{1}",
                                Indent[Lvl], content[i]);
                        else if (verse.ContinuedToNext)  // last but continued?
                        {
                            // fixme: sanity check that loop counter is on last pass

                            Line[N++] = string.Format("{0}{1}</para>",
                                Indent[Lvl], content[i]);

                            requiresClosingParagraphTag = false;
                        }
                        else                            // last?
                            Line[N++] = string.Format("{0}{1}<verse eid=\"{2} {3}:{4}\" />",
                                Indent[Lvl], content[i], usxStyleBookAbbrev, chapterNumber, verse.VerseNumber);
                    }
                    Lvl--;
                }
            }
            Lvl--;

            if (requiresClosingParagraphTag)
            {
                Line[N++] = string.Format("{0}</para>", Indent[Lvl]);
            }
        }
    }
}