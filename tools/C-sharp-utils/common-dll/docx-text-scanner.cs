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
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace common_dll
{
    // **CHANGE THIS AND MUST CHANGE Init() 'AllMarkerText' perhaps more**
    public enum MarkerEnum
    {
        INVALID,

        // Formatting
        ITALICS,                       // Italics
        SUPERSCRIPT,                   // Superscript
        SUBSCRIPT,                     // Subscript  (none currently exist: skip)
        BOLD,                          // Bold (none currently exist: skip)
        RED_TEXT,                      // Words of Jesus

        // Formatting/style-specific
        SMALL_CAPS_CHAR,               // Small-caps   (Style: verses--small caps Char font)
        SMALL_CAPS,                    // Small-caps   (Style: verses--small caps font) (none currently exist: skip)

        // Styles
        NORMAL,                        // Normal (used on title page only)
        NON_VERSE_CONTENT,             //  non-verse-content
        NON_VERSE_CONTENT_INDENTED,    //  non-verse-content-indented
        SPACER_AFTER_PROSE,            //  spacer-after-prose
        SPACER_BEFORE_CHAPTER,         //  spacer-before-chapter
        SPACER_BEFORE_FOOTNOTES,       //  spacer-before-footnotes
        SPACER_INTER_PROSE,            //  spacer-inter-prose
        VERSES_NARRATIVE,              //  verses-narrative
        VERSES_NON_INDENTED_NARRATIVE, //  verses-non-indented-narrative
        VERSES_PROSE,                  //  verses-prose
        VERSES_TITLE,                  //  verses-title
        FOOTNOTES_NORMAL,              //  footnotes-normal
        TITLE,                         //  Title
        HEADING1,                      //  Heading1
        HEADING2,                      //  Heading2
    }

#pragma warning disable CS8618
    public class Paragraph
    {
        // Scanned material
        public string WordOutputText;
        public string TextStrippedOfMarkers;
        public MarkerEnum ParagraphStyle;
        //public MarkerEnum[][] Markers;         // [x][y]: x=index in 'TextStrippedOfMarkers'; y=each marker for that char
        public int[] MarkerBitField;           // [x]: x=index in 'TextStrippedOfMarkers'. Value is per 'AllCharStyles'

        // Processed for intermediate print
        public bool GarbageLine;
        public int[] FormattingBitMap;  // bit field: SUPERSCRIPT_BIT, ITALICS_BIT, SMALL_CAPS_BIT
        public int[] FormattingIndices; // start of formatting in 'TextStrippedOfMarkers'
        public int[] FormattingLength;  // num. of contiguous char's of this formatting in 'TextStrippedOfMarkers'
        public string ProcessedText;
    }

    public static class DocxTextScanner
    {
        // File must've been modified per notes and saved as a text,
        // in order to work here


        public static Paragraph[] AllParagraphs;

        private static string[] AllMarkerText;      // MarkerEnum converted to int is index into this
        private static MarkerEnum[] AllCharStyles;
        private static char[] AllCharStyleLetters;

        //private static MarkerEnum[] OrderedCharStyles;   //

        //public const string SMALL_CAPS_OPEN_TEXT = "<c|";
        //public const string SMALL_CAPS_CLOSE_TEXT = "|c>";
        //public const string SUPERSCRIPT_OPEN_TEXT = "<s|";
        //public const string SUPERSCRIPT_CLOSE_TEXT = "|s>";
        //public const string ITALICS_OPEN_TEXT = "<i|";
        //public const string ITALICS_CLOSE_TEXT = "|i>";

        public static void Init()
        {
            UsxDefinitions.LoadBibleBookNames();

            // CODE ASSUMES ALL MARKERS START WITH %
            AllMarkerText = new string[] {
                         "invalid",

                         // Formatting
                         "%i@",    // Italics
                         "%u@",    // Superscript
                         "%d@",    // Subscript  (none currently exist: skip)
                         "%b@",    // Bold (none currently exist: skip)
                         "%r@",    // Red text

                         // Formatting/style-specific
                         "%c@",    // Small-caps   (Style: verses--small caps Char font)
                         "%s@",    // Small-caps   (Style: verses--small caps font) (none currently exist: skip)

                         // Styles
                         "%no~",   //   Normal (used on title page only)
                         "%nv~",   //  non-verse-content
                         "%ni~",   //  non-verse-content-indented
                         "%sp~",   //  spacer-after-prose
                         "%sb~",   //  spacer-before-chapter
                         "%sf~",   //  spacer-before-footnotes
                         "%si~",   //  spacer-inter-prose
                         "%vn~",   //  verses-narrative
                         "%nn~",   //  verses-non-indented-narrative
                         "%vp~",   //  verses-prose
                         "%vt~",   //  verses-title
                         "%fn~",   //  footnotes-normal
                         "%ti~",   //  Title
                         "%ha~",   //  Heading1
                         "%hb~",   //  Heading2
                };

            // *MUST* BE IN ORDER OF APPLICATION (ref: usx-gen-instructions.txt)
            // MarkerEnum.SMALL_CAPS not supported, using MarkerEnum.SMALL_CAPS_CHAR instead
            // Position corresponds to bit values
            AllCharStyles = new MarkerEnum[] {
                MarkerEnum.SMALL_CAPS_CHAR,        // 1 / bit 0
                MarkerEnum.SUPERSCRIPT,            // 2 / bit 1
                MarkerEnum.ITALICS,                // 4 / bit 2
                MarkerEnum.RED_TEXT,               // 8 / bit 3
            };

            // Is 1-to-1 with above
            AllCharStyleLetters = new char[] {
                'c',             // MarkerEnum.SMALL_CAPS_CHAR
                's',             // MarkerEnum.SUPERSCRIPT
                'i',             // MarkerEnum.ITALICS
                'r',             // MarkerEnum.RED_TEXT
            };

            ScanSuperscriptTextForVerseNumbersAndFootnotes_UnitTest();
        }
#pragma warning restore

        public static bool IsStyle(MarkerEnum marker)
        {
            int markerInt = (int)marker;
            if (markerInt >= (int)MarkerEnum.NORMAL && markerInt <= (int)MarkerEnum.HEADING2)
                return true;
            return false;
        }

        public static void ScanWordOutput(string INPUT_DIRECTORY,
                                          string INPUT_FILE_NAME)
        {
            string fqFileName = INPUT_DIRECTORY + @"\" + INPUT_FILE_NAME;


            if (!Path.Exists(INPUT_DIRECTORY))
            {
                Utils.LogFatal("{0} dir doesn't exist!", INPUT_DIRECTORY);
            }

            if (!File.Exists(fqFileName))
            {
                Utils.LogFatal("{0} file doesn't exist! {1}", INPUT_FILE_NAME, fqFileName);
            }

            Utils.LogEntry("Scanning Word output...");

            string[] docxTextFileAllLines = File.ReadAllLines(fqFileName);
            Utils.LogEntry("Read {0}. {1} lines. Scanning for hyper-text markers...", INPUT_FILE_NAME, docxTextFileAllLines.Length);

            AllParagraphs = new Paragraph[docxTextFileAllLines.Length];

            // Scan markers, save indices to them, strip them
            for (int i = 0; i < docxTextFileAllLines.Length; i++)
            {
                ScanForMarkersInParagraph(ref docxTextFileAllLines[i],
                                          out string paragraphStrippedOfMarkers,
                                          out MarkerEnum paragraphStyle,
                                          //out MarkerEnum[][] thisLineMarkers,
                                          out int[] markerBitField);

                Paragraph thisParagraph = new Paragraph
                {
                    WordOutputText = docxTextFileAllLines[i],
                    TextStrippedOfMarkers = paragraphStrippedOfMarkers,
                    ParagraphStyle = paragraphStyle,
                    MarkerBitField = markerBitField,
                };

                AllParagraphs[i] = thisParagraph;
            }
        }

        public static void ProcessWordScan()
        {
            Utils.LogEntry("Processing marked output...");

            // For some strange reason, Word appends a TOC.
            // This is not wanted, nor does it ever receive a style.
            // Detect it.
            // There are other misformatting, etc. which produce
            //  garbage lines/no style. Keep an eye on this.
            for (int i = 0; i < AllParagraphs.Length; i++)
            {
                if (AllParagraphs[i].ParagraphStyle == MarkerEnum.INVALID)
                    AllParagraphs[i].GarbageLine = true;
            }

            // Assume Word TOC junk are those last lines at end
            int endOfDocumentGarbageLineCount = 0;
            for (int i = AllParagraphs.Length - 1; i >= 0; i--)
            {
                if (AllParagraphs[i].GarbageLine)
                    endOfDocumentGarbageLineCount++;
                else
                    break;
            }

            // Delete presumed TOC
            Array.Resize(ref AllParagraphs, AllParagraphs.Length - endOfDocumentGarbageLineCount);

            // Transform markers
            for (int i = 0; i < AllParagraphs.Length; i++)
            {
                string thisParagraphText = AllParagraphs[i].TextStrippedOfMarkers;
                int thisParagraphTextLength = thisParagraphText.Length;
                //MarkerEnum[] thisMarker = AllParagraphs[i].Markers;
                //int[] thisMarkerIndices = AllParagraphs[i].MarkerIndices;

                //// delete style markers
                //if (AllParagraphs[i].IsLastMarkerAtEndOfLine)
                //{
                //    Array.Resize(ref thisMarker, thisMarkerIndices.Length - 1);
                //    Array.Resize(ref thisMarkerIndices, thisMarkerIndices.Length - 1);
                //}

                //// delete styles and formatting that are out beyond end of line
                //while (thisMarker.Length > 0)
                //{
                //    // If this style points beyond 'thisParagraphText', delete it
                //    // Consume styles until they are within 'thisParagraphText'
                //    if (thisMarkerIndices[thisMarkerIndices.Length - 1] == thisParagraphTextLength)
                //    {
                //        Array.Resize(ref thisMarker, thisMarkerIndices.Length - 1);
                //        Array.Resize(ref thisMarkerIndices, thisMarkerIndices.Length - 1);
                //    }
                //    else
                //    {
                //        break;
                //    }
                //}

                //// Array elements are 1-to-1 with values in 'thisParagraphText'
                //// It indicates with what each char is marked
                //// A value of '0' means it's not marked.
                //int[] perCharacterMap = new int[thisParagraphTextLength];

                //// Populate 'perCharacterMap' to reflect markers
                //for (int k = 0; k < thisMarker.Length; k++)
                //{
                //    MarkerEnum xenum = thisMarker[k];
                //    int xindex = thisMarkerIndices[k];
                //    if (xenum == MarkerEnum.SMALL_CAPS || xenum == MarkerEnum.SMALL_CAPS_CHAR)
                //        SetFormattingBit(ref perCharacterMap[xindex], MarkerEnum.SMALL_CAPS);
                //    else if (xenum == MarkerEnum.SUPERSCRIPT)
                //        SetFormattingBit(ref perCharacterMap[xindex], MarkerEnum.SUPERSCRIPT);
                //    else if (xenum == MarkerEnum.ITALICS)
                //        SetFormattingBit(ref perCharacterMap[xindex], MarkerEnum.ITALICS);
                //    else if (xenum == MarkerEnum.RED_TEXT)
                //        SetFormattingBit(ref perCharacterMap[xindex], MarkerEnum.RED_TEXT);
                //}

                // 'mapStartIndex'-- index in 'thisParagraphText' where a change in formatting
                //          starts.
                // 'mapLength'-- number of contiguous characters consumed by this formatting
                int[] mapBits = new int[thisParagraphTextLength];
                int[] mapStartIndices = new int[thisParagraphTextLength];
                int[] mapLength = new int[thisParagraphTextLength];
                int numMaps = -1;     // since increment-before-use, make it so 1st usage is at [0]

                int previousMapValue = -1;
                bool contiguousProcessingInProgress = false;

                int[] perCharacterMap = AllParagraphs[i].MarkerBitField;

                if (AllParagraphs[i].GarbageLine)
                {
                    continue;
                }

                // Scan for changes in formatting. Record index where formatting changed and
                //   number of char's (length) formatting continues for.
                for (int k = 0; k < perCharacterMap.Length; k++)
                {
                    int mapValue = perCharacterMap[k];

                    // Change in formatting w.r.t. last char?
                    if (mapValue != previousMapValue)
                    {
                        // Start of new contiguous range
                        // 1 of 2 scenarios:
                        //  #1 transition from no markers to marker(s)
                        //       'mapValue' == [some non-zero value] && 'previousMapValue' == '0'
                        //  #2 transition from one marker(s) to a different marker(s)
                        //       'mapValue' == [some non-zero value] && 'previousMapValue' == [some other non-zero value]
                        if (mapValue != 0)
                        {
                            contiguousProcessingInProgress = true;

                            numMaps++;                              // increment before use
                            mapBits[numMaps] = mapValue;
                            mapStartIndices[numMaps] = k;
                            mapLength[numMaps] = 1;
                        }
                        // End of a contiguous range?
                        //  #1  transition from a marker(s) to no markers
                        //     'mapValue' == 0 && 'previousMapValue' == [some non-zero value]
                        else
                        {
                            contiguousProcessingInProgress = false;
                        }
                    }
                    // Still processing same formatting as was last started?
                    else if (contiguousProcessingInProgress)
                    {
                        mapLength[numMaps] += 1;
                    }
                    // 'else' purposely omitted. No change in formatting and not processing formatting.

                    previousMapValue = mapValue;
                }

                // Since we're increment before use, must do a final increment
                numMaps++;
                // Resize arrays accordingly
                Array.Resize(ref mapBits, numMaps);
                Array.Resize(ref mapStartIndices, numMaps);
                Array.Resize(ref mapLength, numMaps);

                // Copy results
                AllParagraphs[i].FormattingBitMap = mapBits;
                AllParagraphs[i].FormattingIndices = mapStartIndices;
                AllParagraphs[i].FormattingLength = mapLength;

                string hyperTextParagraph = "";
                int previousNonHyperIndex = 0;

                for (int k = 0; k < numMaps; k++)
                {
                    int nonHyperIndex = mapStartIndices[k];
                    int nonHyperLength = nonHyperIndex - previousNonHyperIndex;

                    // copy text before this formatted text
                    string textBeforeFormatting = thisParagraphText.Substring(previousNonHyperIndex, nonHyperLength);
                    hyperTextParagraph += textBeforeFormatting;

                    int x = mapBits[k];
                    SetFormattingLetters(x, out string formattingLetters);

                    // Enforce the rule that all superscript formatted text cannot also have other formatting,
                    //  namely be italicized, small caps, etc.
                    if (formattingLetters.Contains("s") && formattingLetters.Length > 1)
                    {
                        Utils.LogEntry("Paragraph beginning with text {0} has superscript with other formatting. Reducing formatting to superscript only.",
                                          hyperTextParagraph.Substring(0, 20 <= hyperTextParagraph.Length ? 20 : hyperTextParagraph.Length));
                        formattingLetters = "s";
                    }

                    int formattedTextLength = mapLength[k];
                    string textWithinFormatting = thisParagraphText.Substring(nonHyperIndex, formattedTextLength);

                    bool isVerseNumberAndOrFootnote = false;
                    if (formattingLetters == "s")
                    {
                        isVerseNumberAndOrFootnote = ScanSuperscriptTextForVerseNumbersAndFootnotes(textWithinFormatting,
                                                                       out string[] verseAndFootnoteStack,
                                                                       out int[] verseNumberStack,
                                                                       out int charsConsumedThrowAway);

                        if (isVerseNumberAndOrFootnote)
                        {
                            // Add all the verse and/or footnotes within their own markup text,
                            // rather than within a single markup. This makes it
                            // easier to scan in next phase.
                            for (int p = 0; p < verseAndFootnoteStack.Length; p++)
                            {
                                string wrappedText = "";
                                if (verseNumberStack[p] > 0)    // is verse number?
                                {
                                    WrapInHypertext(string.Format("{0}", verseNumberStack[p]), formattingLetters, out wrappedText);
                                }
                                else                            // else, is footnote
                                {
                                    WrapInHypertext(string.Format("[{0}]", verseAndFootnoteStack[p]), formattingLetters, out wrappedText);
                                }

                                hyperTextParagraph += wrappedText;

                                // Point to next char after this formatted text
                                previousNonHyperIndex = nonHyperIndex + formattedTextLength;
                            }
                        }
                        // A superscript which is neither a verse number nor footnote.
                        // Things such as "nd" in "2nd", "rd" in "3rd", etc.
                        // Apply formatting to those, but assume space-only content is a mistake,
                        //   and string it of its superscript.
                        else
                        {
                            if (string.IsNullOrWhiteSpace(textWithinFormatting))
                            {
                                Utils.LogEntry("Superscripting of whitespace. Stripping formatting. |{0}|", textWithinFormatting);

                                hyperTextParagraph += textWithinFormatting;
                            }
                            else
                            {
                                WrapInHypertext(textWithinFormatting, formattingLetters, out string wrappedText);

                                hyperTextParagraph += wrappedText;
                            }

                            // Point to next char after this formatted text
                            previousNonHyperIndex = nonHyperIndex + formattedTextLength;
                        }
                    }
                    // Formatting other than superscript
                    else
                    {
                        WrapInHypertext(textWithinFormatting, formattingLetters, out string wrappedText);

                        // add text within formatting
                        hyperTextParagraph += wrappedText;

                        // Point to next char after this formatted text
                        previousNonHyperIndex = nonHyperIndex + formattedTextLength;
                    }
                }

                // add text from last markup to end
                if (numMaps > 0)
                {
                    int lastNonHyperLength = thisParagraphText.Length - mapStartIndices[numMaps - 1] - mapLength[numMaps - 1];
                    string lastChunkOfText = thisParagraphText.Substring(previousNonHyperIndex, lastNonHyperLength);
                    hyperTextParagraph += lastChunkOfText;
                }

                // bit of a hack...above won't put out any text if there were no markups...
                if (numMaps == 0 && thisParagraphText != null && thisParagraphText.Length > 0)
                {
                    hyperTextParagraph = thisParagraphText;
                }

                // Copy result
                AllParagraphs[i].ProcessedText = hyperTextParagraph;
            }

        }

        public static void PrintScannerOutput(string outputDirectory, string scannerOutputFileName)
        {
            if (!Path.Exists(outputDirectory))
            {
                Utils.LogFatal("Dir {0} doesn't exist. Needed to put {1} in.",
                    outputDirectory, scannerOutputFileName);
            }

            string fcFileName = outputDirectory + @"\" + scannerOutputFileName;

            Utils.LogEntry("Printing scanned output intermediate file {0}...", fcFileName);

            string[] lines = new string[100000];
            int p = 0;

            for (int i = 0; i < AllParagraphs.Length; i++)
            {
                string paragraphStyleBlank = "                                   ";    // 35 spcs
                string paragraphStyleText = paragraphStyleBlank;
                if (AllParagraphs[i].ParagraphStyle != MarkerEnum.INVALID)
                    paragraphStyleText = string.Format("{0,-35}", AllParagraphs[i].ParagraphStyle.ToString());

                if (AllParagraphs[i].GarbageLine)
                {
                    lines[p++] = string.Format("[garbage-line]");
                    continue;
                }

                string[] chunks = new string[1000];
                int chunkCount = 0;
                int textLength = AllParagraphs[i].ProcessedText.Length;
                const int MAX_CHARS_PER_LINE = 150;
                int numPasses = (textLength + MAX_CHARS_PER_LINE - 1) / MAX_CHARS_PER_LINE;

                for (int q = 0; q < numPasses; q++)
                {
                    if (q < numPasses - 1)
                        chunks[chunkCount++] = AllParagraphs[i].ProcessedText.Substring(q * MAX_CHARS_PER_LINE, MAX_CHARS_PER_LINE);
                    else
                        chunks[chunkCount++] = AllParagraphs[i].ProcessedText.Substring(q * MAX_CHARS_PER_LINE, textLength % MAX_CHARS_PER_LINE);
                }
                Array.Resize(ref chunks, chunkCount);

                for (int k = 0; k < chunks.Length; k++)
                {
                    if (k == 0)
                        lines[p++] = string.Format("{0}  *{1}*", paragraphStyleText, chunks[k]);
                    else
                        lines[p++] = string.Format("{0}  *{1}*", paragraphStyleBlank, chunks[k]);
                }

                if (chunkCount == 0)
                    lines[p++] = paragraphStyleText;
            }

            Array.Resize(ref lines, p);

            File.WriteAllLines(fcFileName, lines);
        }

#pragma warning disable CS8618
        private static string Debug_ParagraphBeforeDanglingFatal;
#pragma warning restore

        // marker[x][y] where 'x' is index in 'paragraphMinusMarkers' and 'y' is all markups
        private static void ScanForMarkersInParagraph(ref string paragraph,
                                                      out string paragraphMinusMarkers,
                                                      out MarkerEnum paragraphStyle,
                                                      //out MarkerEnum[][] markers,
                                                      out int[] markerBitField)
        {
            if (paragraph == null || paragraph.Length == 0)
            {
                paragraphStyle = MarkerEnum.INVALID;
                //markers = new MarkerEnum[0][];
                markerBitField = null!;
                paragraphMinusMarkers = "";
                return;
            }

            // Word adds whitespaces for paragraphs which have an indented starting line.
            // These are removed and eventually replaced with a usx formatting indicating indentation.
            int whitespaceCount = 0;
            for (int i = 0; i < paragraph.Length; i++)
            {
                char x = paragraph[i];
                if (char.IsWhiteSpace(x))
                {
                    whitespaceCount++;
                }
                else
                {
                    break;
                }
            }

            string paragraphWithNoIndentation = paragraph.Substring(whitespaceCount, paragraph.Length - whitespaceCount);

            if (paragraphWithNoIndentation.Length == 0)
            {
                paragraphStyle = MarkerEnum.INVALID;
                //markers = new MarkerEnum[0][];
                markerBitField = null!;
                paragraphMinusMarkers = "";
                return;
            }

            //markers = new MarkerEnum[10000][];
            paragraphStyle = MarkerEnum.INVALID;

            char[] paragraphArray = paragraphWithNoIndentation.ToCharArray();
            int[] paragraphArrayMarkerBitField = new int[paragraphArray.Length];
            int originalParagraphLength = paragraphArray.Length;
            int currentParagraphLength = originalParagraphLength;

            // Walk styles in reverse direction, undoing steps specified in 'usx-gen-instructions.txt'
            for (int styleIndex = AllCharStyles.Length - 1; styleIndex >= 0; styleIndex--)
            {
                char[] thisStyleText = AllMarkerText[(int)AllCharStyles[styleIndex]].ToCharArray();

                int dstParaIndex = 0;

                // Walk paragraph
                for (int srcParaIndex = 0; srcParaIndex < currentParagraphLength; srcParaIndex++)
                {
                    // Are we at a char style? Get its info.
                    bool didMatch = WhichMarker(ref paragraphArray,
                                                srcParaIndex,
                                                out MarkerEnum matchMarker,
                                                out int markerTextLength);

                    // Squeeze char style text from 'paragraphArray' and apply
                    //  char style to next char after the marker text.
                    // Since destination array shares same holder as source array,
                    //   and destination array is always smaller, we can just
                    //   overwrite 'paragraphArray' with squeezed/marker-deleted text.
                    // When text is squeezed out of 'paragraphArray', formatting is
                    //   remembered in 'paragraphArrayMarkerBitField', which also
                    //   must be moved during squeeze.
                    if (didMatch)
                    {
                        srcParaIndex += markerTextLength;

                        if (srcParaIndex < currentParagraphLength)
                        {
                            paragraphArray[dstParaIndex] = paragraphArray[srcParaIndex];
                            paragraphArrayMarkerBitField[dstParaIndex] = paragraphArrayMarkerBitField[srcParaIndex];
                            SetFormattingBit(ref paragraphArrayMarkerBitField[dstParaIndex], matchMarker);
                            dstParaIndex++;

                            // no loop counter rewind needed
                        }
                        else
                        {
                            string truncated = Debug_ParagraphBeforeDanglingFatal.Substring(Debug_ParagraphBeforeDanglingFatal.Length - 50 > 0 ? Debug_ParagraphBeforeDanglingFatal.Length - 50 : 0);
                            Utils.LogEntry("Paragraph with no style [section break?]. A bit after: {0}", truncated);

                            paragraphStyle = MarkerEnum.INVALID;
                            //markers = new MarkerEnum[0][];
                            markerBitField = null!;
                            paragraphMinusMarkers = "";
                            return;
                        }
                    }
                    // No match?--just copy over single char
                    else
                    {
                        paragraphArray[dstParaIndex] = paragraphArray[srcParaIndex];
                        paragraphArrayMarkerBitField[dstParaIndex] = paragraphArrayMarkerBitField[srcParaIndex];
                        dstParaIndex++;
                    }
                }

                // Done walking and contracting 'paragraphArray'.
                // Resize it for next scan of marker.
                currentParagraphLength = dstParaIndex;
            }

            // Slim array down to squeezed length
            Array.Resize(ref paragraphArray, currentParagraphLength);

            // Now that char markers have been removed, look for paragraph marker
            //   at end.
            int nominalParagraphMarkerLength = AllMarkerText[(int)MarkerEnum.NORMAL].Length;
            if (paragraphArray.Length >= nominalParagraphMarkerLength)
            {
                string expectedParagraphMarkerText = new string(paragraphArray,
                                                                paragraphArray.Length - nominalParagraphMarkerLength,
                                                                nominalParagraphMarkerLength);
                for (int i = 0; i < AllMarkerText.Length; i++)
                {
                    if (AllMarkerText[i] == expectedParagraphMarkerText)
                    {
                        paragraphStyle = (MarkerEnum)i;

                        // Remove paragraph style and reslim
                        Array.Resize(ref paragraphArray, paragraphArray.Length - nominalParagraphMarkerLength);
                        break;
                    }
                }
            }

            // Slim other one now
            currentParagraphLength = paragraphArray.Length;
            Array.Resize(ref paragraphArrayMarkerBitField, currentParagraphLength);
            markerBitField = paragraphArrayMarkerBitField;

            //// Fill in 'markers' from bit fields
            ////  'markers' are bit fields converted to an array of enums
            //markers = new MarkerEnum[currentParagraphLength][];

            //for (int i = 0; i < paragraphArray.Length; i++)
            //{
            //    int thisBitField = paragraphArrayMarkerBitField[i];
            //    if (thisBitField != 0)
            //    {
            //        MarkerEnum[] markerArray = new MarkerEnum[10];
            //        int markerCount = 0;

            //        for (int styleIndex = AllCharStyles.Length - 1; styleIndex >= 0; styleIndex--)
            //        {
            //            int shiftMask = 1 << styleIndex;
            //            int singleBit = (shiftMask & thisBitField);
            //            if (singleBit != 0)
            //            {
            //                markerArray[markerCount++] = AllCharStyles[styleIndex];
            //            }
            //        }

            //        Array.Resize(ref markerArray, markerCount);
            //        markers[i] = markerArray;
            //    }
            //}

            paragraphMinusMarkers = new string(paragraphArray);

            if (paragraphMinusMarkers.Length > 10)
                Debug_ParagraphBeforeDanglingFatal = paragraphMinusMarkers;
        }

#if false     // delete later
        private static void ScanForMarkersInParagraph(ref string paragraph,
                                                     out MarkerEnum[] markers,
                                                     out int[] markerIndices,   // indices in 'paragraphMinusMarkers'
                                                     out bool lastMarkerIsAtEndOfLine,
                                                     out string paragraphMinusMarkers)
        {
            if (paragraph == null || paragraph.Length == 0)
            {
                markers = new MarkerEnum[0];
                markerIndices = new int[0];
                lastMarkerIsAtEndOfLine = false;
                paragraphMinusMarkers = "";
                return;
            }

            // Word adds whitespaces for paragraphs which have an indented starting line.
            // These are removed and eventually replaced with a usx formatting indicating indentation.
            int whitespaceCount = 0;
            for (int i = 0; i < paragraph.Length; i++)
            {
                char x = paragraph[i];
                if (char.IsWhiteSpace(x))
                {
                    whitespaceCount++;
                }
                else
                {
                    break;
                }
            }

            string paragraphWithNoIndentation = paragraph.Substring(whitespaceCount, paragraph.Length - whitespaceCount);

            if (paragraphWithNoIndentation.Length == 0)
            {
                markers = new MarkerEnum[0];
                markerIndices = new int[0];
                lastMarkerIsAtEndOfLine = false;
                paragraphMinusMarkers = "";
                return;
            }

            markers = new MarkerEnum[10000];
            int[] markerIndicesInOriginalParagraph = new int[10000];
            int[] markerLengthInOriginalParagraph = new int[100000];
            lastMarkerIsAtEndOfLine = false;
            int numIndices = 0;

            int originalLength = paragraphWithNoIndentation.Length;
            for (int i = 0; i < originalLength; i++)
            {
                char x = paragraphWithNoIndentation[i];
                // CPU optimization
                // NOTE: WE'RE ASSUMING ALL MARKERS START WITH %
                if (x == '%')
                {
                    // Is % the start of a marker (probably is)?
                    // Tells us about the marker.
                    if (WhichMarker(ref paragraphWithNoIndentation,
                                    i,
                                    out MarkerEnum thisMarker,
                                    out string thisMarkerText,
                                    out int thisMarkerLength))
                    {
                        markers[numIndices] = thisMarker;
                        markerIndicesInOriginalParagraph[numIndices] = i;
                        markerLengthInOriginalParagraph[numIndices] = thisMarkerLength;
                        numIndices++;

                        i += thisMarkerLength - 1;    // point to last char in marker, effectively skipping over it
                                                      // keep in mind, loop will increment 'i' again, to point to next char after marker

                        // Is last char in marker the last char in the paragraph?
                        if (i == originalLength - 1)
                        {
                            lastMarkerIsAtEndOfLine = true;
                        }
                    }
                }
            }

            Array.Resize(ref markerIndicesInOriginalParagraph, numIndices);
            Array.Resize(ref markerLengthInOriginalParagraph, numIndices);
            Array.Resize(ref markers, numIndices);

            markerIndices = new int[numIndices];
            paragraphMinusMarkers = "";

            // Copy text snippets from original to 'paragraphMinusMarkers', excluding the markers themselves
            // Do like this:
            //   k = 0     0 to first marker
            //   k = 1     next char after first marker to 2nd marker
            //   k = 2     next char after 2nd marker to 3rd marker
            //   etc
            int startIndex = 0;
            for (int k = 0; k < numIndices; k++)
            {
                int copyLength = markerIndicesInOriginalParagraph[k] - startIndex;

                paragraphMinusMarkers += paragraphWithNoIndentation.Substring(startIndex, copyLength);

                markerIndices[k] = paragraphMinusMarkers.Length;

                startIndex += copyLength + markerLengthInOriginalParagraph[k];
            }

            // Copy from end of last marker to end of line
            // If 'lasterMarkerIsAtEndOfLine' == 0, then 'finalCopyLength' *MUST* be zero
            int finalCopyLength = originalLength - startIndex;
            paragraphMinusMarkers += paragraphWithNoIndentation.Substring(startIndex, finalCopyLength);
        }
#endif

        private static void ScanSuperscriptTextForVerseNumbersAndFootnotes_UnitTest()
        {
            string[] testStrings = new string[]
            {
                "1", "0", "10", "[A]", "21[B]",
                "[aa]", "18 [a]", "[b] [d]"
            };
            bool[] expectedOut_ReturnValue = new bool[]
            {
                true, true, true, true, true,
                true, true, true,
            };
            string[][] expectedOut_VerseAndFootnoteStack = new string[][]
            {
                new string[] { "1" }, new string[] { "0"}, new string[] { "10" }, new string[] { "A"}, new string[] {"21", "B"},
                new string[] { "aa"}, new string[] {"18", "a"}, new string[] {"b", "d"},
            };
            int[][] expectedOut_VerseNumberStack = new int[][]
            {
                new int[] {1 }, new int[] {0 }, new int[] {10 }, new int[] {0 }, new int[] {21, 0 },
                new int[] {0 }, new int[] {18, 0 }, new int[] {0, 0},
            };
            int[] expectedOut_CharsConsumed = new int[]
            {
                testStrings[0].Length, testStrings[1].Length, testStrings[2].Length, testStrings[3].Length, testStrings[4].Length,
                testStrings[5].Length, testStrings[6].Length, testStrings[7].Length,
            };
            
            for (int i = 0; i < testStrings.Length; i++)
            {
                bool ok1 = ScanSuperscriptTextForVerseNumbersAndFootnotes(testStrings[i],
                    out string[] verseAndFootnoteStack,
                    out int[] verseNumberStack,
                    out int charsConsumed);

                bool pass1 = ok1 == expectedOut_ReturnValue[i];
                bool pass2 = verseAndFootnoteStack.Length == expectedOut_VerseAndFootnoteStack[i].Length;
                if (pass2)
                {
                    for (int k = 0; k < verseAndFootnoteStack.Length; k++)
                        pass2 &= verseAndFootnoteStack[k] == expectedOut_VerseAndFootnoteStack[i][k];
                }
                bool pass3 = verseNumberStack.Length == expectedOut_VerseNumberStack[i].Length;
                if (pass3)
                {
                    for (int k = 0; k < verseNumberStack.Length; k++)
                        pass3 &= verseNumberStack[k] == expectedOut_VerseNumberStack[i][k];
                }
                bool pass4 = charsConsumed == expectedOut_CharsConsumed[i];

                if (!(pass1 && pass2 && pass3 && pass4))
                {
                    Utils.LogFatal("ScanSuperscriptTextForVerseNumbersAndFootnotes_UnitTest() UT failure!");
                }
            }
        }

        // Assume superscript text has 1 or more verse number and/or footnotes
        // 'inString'-- supersripted text (only)
        // 'verseAndFootnoteStack'-- verse numbers and footnotes in order they appear
        // 'verseNumberStack'-- redundant of 'verseAndFootnoteStack' for verse number put in int32 form
        // 'charsConsumed'-- length of this match
        //
        // Returns 'false' if neither verse number or footnote found.
        private static bool ScanSuperscriptTextForVerseNumbersAndFootnotes(string inString,
                                                          out string[] verseAndFootnoteStack,
                                                          out int[] verseNumberStack,
                                                          out int charsConsumed)
        {
            charsConsumed = 0;

            verseAndFootnoteStack = new string[10];
            verseNumberStack = new int[10];
            int stackCount = 0;

            // Loop around scanning for the multiple instances of verse numbers or footnotes
            string reducedString = inString;
            while (reducedString.Length > 0)
            {
                char firstChar = reducedString[0];

                // Verse number?
                if (Utils.ScanDecimalFromStream(ref reducedString, 0, out int scanValue, out int numDigitsConsumed))
                {
                    verseAndFootnoteStack[stackCount] = reducedString.Substring(0, numDigitsConsumed);
                    verseNumberStack[stackCount] = scanValue;
                    stackCount++;

                    charsConsumed += numDigitsConsumed;

                    // remove digits just scanned from string
                    reducedString = reducedString.Substring(numDigitsConsumed, reducedString.Length - numDigitsConsumed);
                }

                // Start of a footnote
                // Ex:  [a]    or   [B]     or     [bb]
                else if (firstChar == '[')
                {
                    // discard leading '['
                    reducedString = reducedString.Substring(1, reducedString.Length - 1);

                    int indexOfClosingBracket = reducedString.IndexOf(']', 0);
                    const int REASONABLE_SIZE_OF_TEXT_WITHIN_BRACKETS = 8;

                    if (indexOfClosingBracket == -1 || (indexOfClosingBracket >= REASONABLE_SIZE_OF_TEXT_WITHIN_BRACKETS))
                    {
                        Utils.LogFatal("Closing bracket missing in footnote!");
                    }

                    string textBetweenBrackets = reducedString.Substring(0, indexOfClosingBracket);

                    charsConsumed += 2;   // for both '[' and ']'
                    charsConsumed += textBetweenBrackets.Length;

                    // Resize 'reducedString' to eliminate closing bracket and text between brackets
                    // (we don't use 'reducedString' within next 'while' loop)
                    int footnoteAndClosingBracketLength = textBetweenBrackets.Length + 1;
                    reducedString = reducedString.Substring(footnoteAndClosingBracketLength, reducedString.Length - footnoteAndClosingBracketLength);

                    // loop inside [] looking for ordinary footnotes (ex: [d]) or
                    //  multiple comma-delimited footnotes (ex: [a,B])
                    while (textBetweenBrackets.Length > 0)
                    {
                        char firstCharInFootnote = textBetweenBrackets[0];

                        // delimiter? Throw it away.
                        if (",".Contains(firstCharInFootnote))
                        {
                            textBetweenBrackets = textBetweenBrackets.Substring(1, textBetweenBrackets.Length - 1);
                            continue;
                        }

                        char letter1 = textBetweenBrackets[0];
                        char letter2 = '*';
                        bool hasMultipleLetters = textBetweenBrackets.Length > 1;
                        if (hasMultipleLetters)
                            letter2 = textBetweenBrackets[1];

                        bool isUpperCase1 = letter1 >= 'a' && letter1 <= 'z';
                        bool isUpperCase2 = letter2 >= 'a' && letter2 <= 'z';
                        bool isLowerCase1 = letter1 >= 'A' && letter1 <= 'Z';
                        bool isLowerCase2 = letter2 >= 'A' && letter2 <= 'Z';

                        // Is this a 2-letter footnote?
                        if (hasMultipleLetters && (isUpperCase1 && isUpperCase2 || isLowerCase1 && isLowerCase2))
                        {
                            // Copy these 2 letters to stack
                            verseAndFootnoteStack[stackCount++] = textBetweenBrackets.Substring(0, 2);

                            // Throw away these 2 letters
                            textBetweenBrackets = textBetweenBrackets.Substring(2, textBetweenBrackets.Length - 2);
                        }
                        // A single-letter footnote?
                        else if (isUpperCase1 || isLowerCase1)
                        {
                            // Copy this single letter over to stack
                            verseAndFootnoteStack[stackCount++] = textBetweenBrackets.Substring(0, 1);

                            // Throw away this single letter
                            textBetweenBrackets = textBetweenBrackets.Substring(1, textBetweenBrackets.Length - 1);
                        }
                        // Misformatted footnote
                        else
                        {
                            Utils.LogFatal("Illegal character(2) in footnote!");
                        }
                    }
                }

                // Quietly discard filler. These extra chars are a corner case/slightly erroneous formatting:
                //  1. An empty verse followed by a footnote followed by another verse number
                //      ex:   "10[a] 11"
                //  2. A trailing space that's superscripted (really shouldn't be, though)
                //      ex: "10 " where trailing space is superscripted
                else if (" ".Contains(firstChar))
                {
                    reducedString = reducedString.Substring(1, reducedString.Length - 1);
                    charsConsumed++;
                }

                // Non-verse-number, non-footnoted superscript
                //  Ex: "2nd" where "nd" is superscripted but "2" is not
                else
                {
                    Array.Resize(ref verseAndFootnoteStack, stackCount);
                    Array.Resize(ref verseNumberStack, stackCount);

                    return stackCount > 0;
                }
            }

            if (stackCount == 0)
            {
                Utils.LogEntry("Expected to find either verse number or footnote but found neither. |{0}|", inString);
            }

            Array.Resize(ref verseAndFootnoteStack, stackCount);
            Array.Resize(ref verseNumberStack, stackCount);

            return stackCount > 0;
        }


        // If 'paragraph' starting at 'testIndex' points to a MarkerEnum,
        // set 'marker' to whichever one it points to, 'markerLength' to
        // that marker's length, and return 'true'.
        private static bool WhichMarker(ref char[] charArray,
                                        int charArrayOffset,
                                        out MarkerEnum marker,
                                        out int markerLength)
        {
            marker = MarkerEnum.INVALID;
            string markerText = null!;
            markerLength = -1;

            // Save CPU cycles
            if (charArray[charArrayOffset] != '%')
            {
                return false;
            }

            for (int i = 0; i < AllCharStyles.Length; i++)
            {
                markerText = AllMarkerText[(int)AllCharStyles[i]];
                char[] thisCharStyle = markerText.ToCharArray();

                if (Utils.CharArrayMatch(ref charArray, charArrayOffset, ref thisCharStyle, true))
                {
                    marker = AllCharStyles[i];
                    markerLength = thisCharStyle.Length;
                    return true;
                }
            }

            return false;
        }

        // If 'paragraph' starting at 'testIndex' points to a MarkerEnum,
        // set 'marker' to whichever one it points to, 'markerLength' to
        // that marker's length, and return 'true'.
        private static bool WhichMarker(ref string paragraph,
                                       int testIndex,
                                       out MarkerEnum marker,
                                       out string markerText,
                                       out int markerLength)
        {
            marker = (MarkerEnum)0;
            markerText = "";
            markerLength = 0;

            // https://stackoverflow.com/questions/203377/getting-the-max-value-of-an-enum
            //int minEnumValue = Enum.GetValues(typeof(MarkerEnum)).Cast<int>().Min();
            int minEnumValue = (int)MarkerEnum.INVALID + 1;
            int maxEnumValue = Enum.GetValues(typeof(MarkerEnum)).Cast<int>().Max();

            int maxLengthOfTestString = 4;    // hack! size of marker text
            int remainingTextInParagraphLength = paragraph.Length - testIndex;
            if (remainingTextInParagraphLength < maxLengthOfTestString)
                maxLengthOfTestString = remainingTextInParagraphLength;

            string paragraphTestSnippet = paragraph.Substring(testIndex, maxLengthOfTestString);
            paragraphTestSnippet = paragraphTestSnippet.ToLower();

            for (int i = minEnumValue; i <= maxEnumValue; i++)
            {
                MarkerEnum markerEnum = (MarkerEnum)i;
                markerText = AllMarkerText[i];

                // Markers could have been put in Word output as upper or lower case,
                // not just lower.
                if (string.Compare(paragraphTestSnippet, 0, markerText, 0, markerText.Length) == 0)
                {
                    marker = markerEnum;
                    markerLength = markerText.Length;
                    return true;
                }
            }

            return false;
        }

        private static void WrapInHypertext(string innerText,
                                           string formattingLetters,
                                           out string hyperText)
        {
            hyperText = string.Format("<{0}|", formattingLetters);

            hyperText += innerText;

            hyperText += string.Format("|{0}>", formattingLetters);
        }

        public static void SetFormattingBit(ref int bitField, MarkerEnum thisMarker)
        {
            int index = Array.IndexOf(AllCharStyles, thisMarker);

            if (index != -1)
            {
                bitField |= 1 << index;
            }
        }

        public static void SetFormattingLetters(int bitField, out string formattingLetters)
        {
            formattingLetters = "";
            int bitPosition = 1;

            for (int i = 0; i < AllCharStyleLetters.Length; i++)
            {
                if ((bitField & bitPosition) != 0)
                    formattingLetters += AllCharStyleLetters[i];

                bitPosition <<= 1;
            }
        }

        public static bool ScanForFormattingLetters(ref string inString, int offset, out MarkerEnum[] markers)
        {
            markers = new MarkerEnum[20];
            int count = 0;

            // Keep looping through string until a char won't match
            for (int i = offset; i < inString.Length; i++)
            {
                char x = inString[i];

                for (int k = 0; k < AllCharStyleLetters.Length; k++)
                {
                    // matched?
                    if (x == AllCharStyleLetters[k])
                    {
                        markers[count++] = AllCharStyles[k];
                        break;
                    }
                    // Last pass of loop and no matches found?
                    else if (k == AllCharStyleLetters.Length - 1)
                    {
                        Array.Resize(ref markers, count);
                        return count > 0;
                    }
                }
            }

            Array.Resize(ref markers, count);
            return count > 0;
        }
    }


}