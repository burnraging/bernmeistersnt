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
using static System.Net.Mime.MediaTypeNames;

namespace common_dll
{
    public static partial class UsxConverter
    {

        private static bool ScanChapterNumberFromHeading2Text(string heading2Text, BookEnum bookEnum, out int chapterNumber)
        {
            string lowerCaseText = heading2Text.ToLower();

            // Some books (1 John, etc.) have numbers in them; therefore, remove all book names
            //  to guarantee these numbers aren't mistakenly interpreted as chapter numbers.
            string fullBookName = BibleBooks.AllBooksFullNames[(int)bookEnum].ToLower();
            string abbrevBookName = BibleBooks.AllBooksAbbrev[(int)bookEnum].ToLower();
            string abbrev2BookName = BibleBooks.AllBooksAbbrev2[(int)bookEnum].ToLower();

            int fullBookNameIndex = lowerCaseText.IndexOf(fullBookName);
            int abbrevBookNameIndex = lowerCaseText.IndexOf(abbrevBookName);
            int abbrev2BookNameIndex = lowerCaseText.IndexOf(abbrev2BookName);

            if (fullBookNameIndex != -1)
                lowerCaseText = lowerCaseText.Remove(fullBookNameIndex, fullBookName.Length);
            else if (abbrevBookNameIndex != -1)
                lowerCaseText = lowerCaseText.Remove(abbrevBookNameIndex, abbrevBookName.Length);
            else if (abbrev2BookNameIndex != -1)
                lowerCaseText = lowerCaseText.Remove(abbrev2BookNameIndex, abbrev2BookName.Length);

            // Search string for first digit and scan it for chapter number
            for (int i = 0; i < lowerCaseText.Length; i++)
            {
                char x = lowerCaseText[i];

                if (Char.IsDigit(x))
                {
                    Utils.ScanDecimalFromStream(ref lowerCaseText, i, out chapterNumber, out int throwAwayNumCharsConsumed);
                    return true;
                }
            }

            chapterNumber = 0;
            return false;
        }


        // Remove verse number markups from a paragraph
        //   and break paragraph into fragments which are delineated by
        //   the verse number markups.
        // 'verseNumbers' points to locations between each
        //   'paragraphFragment', where verseNumbers[0] is before
        //    paragraphFragment[0] and verseNumber[N+1] is
        //    after paragraphFragment[N]
        //    This results in verseNumber.Length + 1 == paragraphFragments.Length
        private static void StripVersesFromParagraph(ref string paragraph,
                                                     BookEnum bookEnum,
                                                     int chapterNumber,
                                                     out string[] paragraphFragments,
                                                     out int[] verseNumbers)
        {
            paragraphFragments = new string[200];
            int paragraphFragmentCount = 0;
            verseNumbers = new int[200];
            int verseNumberCount = 0;

            int lastCopyFromIndex = 0;

            for (int i = 0; i < paragraph.Length; i++)
            {
                char thisChar = paragraph[i];
                if (thisChar == '<')
                {
                    bool isVerseOrFootnote = ScanVerseNumberOrFootnoteReference(ref paragraph,
                                                                                i,
                                                                                out int thisVerseNumber,
                                                                                out bool isVerseNumber,
                                                                                out string footnoteLettersThroway,
                                                                                out int charsConsumed,
                                                                                bookEnum,
                                                                                chapterNumber);
                    isVerseNumber &= isVerseOrFootnote;

                    if (isVerseNumber)
                    {
                        // copy over any text up to this verse markup
                        string thisSubstring = paragraph.Substring(lastCopyFromIndex, i - lastCopyFromIndex);
                        paragraphFragments[paragraphFragmentCount++] = thisSubstring;

                        lastCopyFromIndex = i + charsConsumed;   // point to char after marked up verse text

                        // Add this verse info
                        // Note that this points what would be the next char after the verse markup
                        //    in 'paragraph'
                        verseNumbers[verseNumberCount] = thisVerseNumber;
                        verseNumberCount++;

                        i += charsConsumed - 1;     // -1 b/c loop counter will inc.
                    }
                }
            }

            // Must do a final copy-over
            string finalSubstring = paragraph.Substring(lastCopyFromIndex, paragraph.Length - lastCopyFromIndex);
            paragraphFragments[paragraphFragmentCount++] = finalSubstring;

            Array.Resize(ref paragraphFragments, paragraphFragmentCount);
            Array.Resize(ref verseNumbers, verseNumberCount);

            // sanity check/assertion
            if (paragraphFragmentCount != verseNumberCount + 1)
                Utils.LogFatal("StripVersesFromParagraph() failure {0} chapter {1} at {2}",
                               bookEnum.ToString(),
                               chapterNumber,
                               paragraph.Substring(0, paragraph.Length > 50 ? 50 : paragraph.Length));
        }

        // Scan hyper-text markup
        //   ex:   <is|innerText|is>
        //
        // Scan 'inString' starting at 'offset'
        // Throw exception if markup is only partially correct.
        //
        //  'markers' is scanned version of letters between opening angle bracket + vertical bar
        // 'CharsConsumed' includes all text between opening and closing angle brackets inclusive
        private static bool ScanHyperText(ref string inString,
                                          int offset,
                                          out MarkerEnum[] markers,
                                          out string innerText,
                                          out int charsConsumed)
        {
            markers = null!;
            innerText = "";
            charsConsumed = 0;

            int index = offset;

            // Check to ensure we didn't exceed the string's length
            if (inString.Length - offset - 1 <= 0)
                return false;

            if (inString[index++] == '<')
            {
                int openingVerticalBarIndex = inString.IndexOf('|', index);

                int numberOfMarkers = openingVerticalBarIndex - index;

                // Does an opening vertical bar reasonably follow afer opening angle bracket?
                const int REASONABLE_NEVER_EXCEED_NUMBER_OF_MARKERS = 6;
                if (openingVerticalBarIndex != -1 && (numberOfMarkers <= REASONABLE_NEVER_EXCEED_NUMBER_OF_MARKERS))
                {
                    string markerLetters = inString.Substring(index, numberOfMarkers);

                    if (DocxTextScanner.ScanForFormattingLetters(ref markerLetters, 0, out markers))
                    {
                        // Were all the chars between '<' and '|' legitimate marker characters?
                        if (markers.Length != numberOfMarkers)
                        {
                            Utils.LogFatal("Invalid markers!");
                        }

                        // Advance to next char after opening vertical bar
                        index += numberOfMarkers + 1;

                        int closingVerticalBarIndex = inString.IndexOf('|', index);
                        if (closingVerticalBarIndex == -1)
                        {
                            Utils.LogFatal("Missing closing vertical-bar!");
                        }

                        innerText = inString.Substring(index, closingVerticalBarIndex - openingVerticalBarIndex - 1);

                        index += innerText.Length + 1;  // should be == 'closingVerticalBarIndex'+1

                        string repeatedMarkerLetters = inString.Substring(index, markers.Length);
                        if (markerLetters != repeatedMarkerLetters)
                        {
                            Utils.LogFatal("Marker letters should repeat but don't!");
                        }

                        // Skip past repeated marker letters
                        index += markerLetters.Length;

                        if (inString[index++] != '>')
                        {
                            Utils.LogFatal("Closing angle bracket in markup missing!");
                        }

                        charsConsumed = index - offset;
                        return true;
                    }
                }
            }

            markers = new MarkerEnum[0];
            innerText = "";
            charsConsumed = 0;
            return false;
        }

        // Scan a superscripted verse number or footnote, a single footnote, or multiple of either/both
        // Results are put in order 'verseAndFootnoteStack' and 'verseNumberStack'
        // 'verseAndFootnoteStack' contains a single-letter "a" or double-letter "bb"
        //      if footnote, and the verse digits "11" in text (which is redundant with
        //      'verseNumberStack').
        // 'verseNumberStack' contains the actual integer verse number, and
        //   verse number as text is echoed in 'verseAndFootnoteStack'
        // 'charsConsumed' includes all marked up text: everything in angle brackets inclusive
        // Returns false if no verses or footnotes found
        private static bool ScanVerseNumberOrFootnoteReference(ref string inString,
                                                               int offset,
                                                               out int verseNumber,
                                                               out bool isVerseNumberNotFootnote,
                                                               out string footnoteLetters,
                                                               out int charsConsumed,
                                                               BookEnum bookEnum,
                                                               int chapterNumber)
        {
            verseNumber = -1;
            isVerseNumberNotFootnote = false;
            footnoteLetters = "";
            charsConsumed = 0;

            bool success = false;

            // Check to ensure we didn't exceed the string's length
            if (inString.Length - offset - 1 <= 0)
                return false;

            // Quick check to save CPU cycles
            if (inString[offset] == '<')
            {
                // Is this a hyper-text? Parse it.
                if (ScanHyperText(ref inString, offset, out MarkerEnum[] markers, out string verseNumberOrFootnoteText, out charsConsumed))
                {
                    // Is this hyper-text a superscript? If so, it must be a verse number or footnote
                    // Multiple formattings are allowed here, but not allowed in docx-text-scanner.
                    if (markers.Contains(MarkerEnum.SUPERSCRIPT))
                    {
                        // Verse number?
                        if (Utils.ScanDecimalFromStream(ref verseNumberOrFootnoteText, 0, out verseNumber, out int numDigitsConsumed))
                        {
                            isVerseNumberNotFootnote = true;
                            success = true;
                        }
                        // Start of a footnote
                        // Ex:  [a]    or   [B]     or     [bb]
                        else if (verseNumberOrFootnoteText[0] == '[')
                        {
                            string workingString = verseNumberOrFootnoteText;

                            // discard leading '['
                            workingString = workingString.Substring(1, workingString.Length - 1);

                            int indexOfClosingBracket = workingString.IndexOf(']', 0);
                            const int MAX_NUMBER_OF_LETTERS_IN_A_FOOTNOTE = 2;

                            if (indexOfClosingBracket == -1 || (indexOfClosingBracket > MAX_NUMBER_OF_LETTERS_IN_A_FOOTNOTE))
                            {
                                Utils.LogFatal("{0} chapter {1} v{2}: Closing bracket missing in footnote!", bookEnum.ToString(), chapterNumber, verseNumber);
                            }

                            // discard trailing ']'
                            workingString = workingString.Substring(0, verseNumberOrFootnoteText.Length - 2);

                            char letter1 = workingString[0];
                            char letter2 = '*';
                            bool hasMultipleLetters = workingString.Length > 1;
                            if (hasMultipleLetters)
                                letter2 = workingString[1];

                            bool isUpperCase1 = letter1 >= 'a' && letter1 <= 'z';
                            bool isUpperCase2 = letter2 >= 'a' && letter2 <= 'z';
                            bool isLowerCase1 = letter1 >= 'A' && letter1 <= 'Z';
                            bool isLowerCase2 = letter2 >= 'A' && letter2 <= 'Z';

                            // Is this a 2-letter footnote?
                            if (hasMultipleLetters && (isUpperCase1 && isUpperCase2 || isLowerCase1 && isLowerCase2))
                            {
                                footnoteLetters = workingString;
                                isVerseNumberNotFootnote = false;
                            }
                            // A single-letter footnote?
                            else if (isUpperCase1 || isLowerCase1)
                            {
                                footnoteLetters = workingString;
                                isVerseNumberNotFootnote = false;
                            }
                            // Misformatted footnote
                            else
                            {
                                Utils.LogFatal("{0} chapter {1} v{2}: Illegal character(s) in footnote!", bookEnum.ToString(), chapterNumber, verseNumber);
                            }

                            success = true;
                        }

                        // Non-verse-number, non-footnote superscript, though rare, is not an error
                        // An example would be the "th" in "4th"
                        else
                        {
                            Utils.LogEntry("{0} chapter {1} v{2}: Superscript text <{3}> which is neither a verse number or a footnote.",
                                           bookEnum.ToString(), chapterNumber, verseNumber, verseNumberOrFootnoteText);
                        }
                    }
                }
            }

            // No more left-angle brackets to end of paragraph
            else
            {
                charsConsumed = 0;
            }

            return success;
        }



        // Take an xml string ('singleString'), which will already have xml elements
        // in it, and break it into multiple lines. Also, for the text sections,
        // do xml substitutions (required by xml spec).
        //
        // 'targetLineLength'-- Ideal number of chars in line, not inc. indentations
        //          Actual line length will be target length to next space.
        // 'leadingIndentation'-- Indentation (all spaces) of first line
        // 'indentation'-- Indentation of 2nd, 3rd, etc. lines
        public static string[] SingleStringToMultiXmlStrings(string singleString,
                                                             int targetLineLength,
                                                             string leadingIndentation,
                                                             string indentation)
        {
            string escapedString = SingleStringXmlSubstitutions(singleString);

            int overallLineLengthFirstLine = targetLineLength - leadingIndentation.Length;
            int overallLineLength = targetLineLength - indentation.Length;

            string[] outStrings = new string[1000];
            int outStringsIndex = 0;

            int lastStringIndex = 0;

            for (int i = 0; i < singleString.Length; i++)
            {
                int offset;
                string thisIndentation;
                // If we're doing first line, use first line indentation;
                // otherwise, use default
                if (outStrings[0] == null)
                {
                    offset = lastStringIndex + overallLineLengthFirstLine;
                    thisIndentation = leadingIndentation;
                }
                else
                {
                    offset = lastStringIndex + overallLineLength;
                    thisIndentation = indentation;
                }

                // Look for next space after overall length.
                // This is where we'll break the previous line
                int nextSpaceIndex = Utils.NextPlaceToBreakXmlLine(ref singleString, offset);
                string fragment;
                if (nextSpaceIndex != -1)
                {
                    // Exclude 1 b/c we don't want to include the space char in the fragment.
                    // When the xml scanner in the app processes this, it'll add the space
                    // back in, which is according to xml spec.
                    int fragmentLength = nextSpaceIndex - lastStringIndex;    // normally would have a -1 for
                    fragment = thisIndentation + singleString.Substring(lastStringIndex, fragmentLength);
                    outStrings[outStringsIndex++] = fragment;

                    lastStringIndex = nextSpaceIndex + 1; // leap over the space
                    i = lastStringIndex - 1;              // -1 b/c loop will inc.
                }
                // copy to end of 'singleString'
                else
                {
                    // If there is a trailing space, we must include it in the last line
                    int fragmentLength = singleString.Length - lastStringIndex;
                    fragment = thisIndentation + singleString.Substring(lastStringIndex, fragmentLength);
                    outStrings[outStringsIndex++] = fragment;

                    break;
                }
            }

            Array.Resize(ref outStrings, outStringsIndex);
            return outStrings;

        }

        // Translate the xml characters in the text portion of an xml string
        public static string SingleStringXmlSubstitutions(string singleString)
        {
            string outString = "";
            int textLastIndex = 0;

            if (singleString == null)
                return outString;

            for (int i = 0; i < singleString.Length; i++)
            {
                int commentStartIndex = singleString.IndexOf("<!--", i);
                int elementStartIndex = singleString.IndexOf("<", i);
                bool hasCommentStart = commentStartIndex != -1;
                bool hasElementStart = elementStartIndex != -1;
                bool jumpToComment = hasCommentStart && !(hasElementStart && commentStartIndex < elementStartIndex);
                bool jumpToElement = hasElementStart && !(hasCommentStart && elementStartIndex < commentStartIndex);

                if (jumpToComment)
                {
                    // copy text before
                    string text = singleString.Substring(textLastIndex, commentStartIndex - textLastIndex);
                    ApplyXmlCharacterSubstitutions(ref text, out string escapedText);
                    outString += escapedText;

                    // copy comment
                    int commentEndIndex = singleString.IndexOf("-->", commentStartIndex);
                    string commentString = singleString.Substring(commentStartIndex, commentEndIndex + 1 - commentStartIndex);
                    outString += commentString;

                    // jump over comment
                    textLastIndex = commentEndIndex + 1;
                    i = textLastIndex - 1;
                }
                else if (jumpToElement)
                {
                    // copy text before
                    string text = singleString.Substring(textLastIndex, elementStartIndex - textLastIndex);
                    ApplyXmlCharacterSubstitutions(ref text, out string escapedText);
                    outString += escapedText;

                    // copy comment
                    int elementEndIndex = singleString.IndexOf(">", elementStartIndex);
                    string elementString = singleString.Substring(elementStartIndex, elementEndIndex + 1 - elementStartIndex);
                    outString += elementString;

                    // jump over comment
                    textLastIndex = elementEndIndex + 1;
                    i = textLastIndex - 1;
                }
                else
                {
                    string finalText = singleString.Substring(textLastIndex, singleString.Length + 1 - textLastIndex);
                    ApplyXmlCharacterSubstitutions(ref finalText, out string escapedText);
                    outString += escapedText;
                    break;
                }
            }

            return outString;
        }

        // Do substitutions required by XML standard.
        // Returns 'true' if any translations done.
        private static bool ApplyXmlCharacterSubstitutions(ref string inString, out string outString)
        {
            outString = "";

            if (inString == null || inString.Length == 0)
            {
                return false;
            }

            char[] xmlEscapeChars = new char[] { '<', '>', '\'', '"', '&' };
            string[] xmlEscapeSequences = new string[] { "&lt;", "&gt;", "&apos;", "&quot;", "&amp;" };  // must be 1-to-1 with 'xmlEscapeChars'

            int lastNonFormattedTextCopyOverIndex = 0;

            bool substitutionDone = false;

            for (int i = 0; i < inString.Length; i++)
            {
                int indexOfInterest = inString.IndexOfAny(xmlEscapeChars, i);

                // Is there a next character (which will be at 'indexOfInterest') that needs escaping?
                if (indexOfInterest != -1)
                {
                    // Copy previous text block, which didn't have any chars to be substituted
                    outString += inString.Substring(lastNonFormattedTextCopyOverIndex, indexOfInterest - lastNonFormattedTextCopyOverIndex);

                    char thisChar = inString[indexOfInterest];
                    int indexOfEscapeChar = -1;
                    for (int k = 0; k < xmlEscapeChars.Length; k++)
                    {
                        if (thisChar == xmlEscapeChars[k])
                        {
                            indexOfEscapeChar = k;
                            break;
                        }
                    }

                    if (indexOfEscapeChar < 0 || indexOfEscapeChar >= xmlEscapeSequences.Length)
                    {
                        Utils.LogFatal("Sanity check fail in ApplyCharacterFormatting(), index {0} in {0}", indexOfInterest, inString);
                    }

                    // Make substitution
                    outString += xmlEscapeSequences[indexOfEscapeChar];
                    substitutionDone = true;

                    i = indexOfInterest;          // +1 inc. by loop counter will put next 'i' at next char after escaped char
                    lastNonFormattedTextCopyOverIndex = i + 1;   // point to next char after escaped char
                }
                // No more chars to escape
                else
                {
                    // Copy over remaining chars to end of 'inString'. These chars don't have any substitutions
                    outString += inString.Substring(lastNonFormattedTextCopyOverIndex, inString.Length - lastNonFormattedTextCopyOverIndex);
                    break;
                }
            }

            return substitutionDone;
        }

        // Detect if 'inString' starting at 'offset' is an MS Word document Strong's reference,
        //   which is of the form "(some-word/Strong’s 12345)" or "(some-word/Strong’s G12345)"
        // If so, return true after setting the output variables.
        private static bool ParseStrongsReference(ref string inString,
                                                  int offset,
                                                  out int strongsNumber,
                                                  out bool hasletterAtEndOfStrongsNumber,
                                                  out char trailingStrongsLetter,
                                                  out bool isHebrewNotGreek,
                                                  out string displayWord,
                                                  out int charsConsumed)
        {
            strongsNumber = 0;
            hasletterAtEndOfStrongsNumber = false;
            trailingStrongsLetter = '*';
            isHebrewNotGreek = false;
            displayWord = "";
            charsConsumed = 0;

            // Note that "Strong's " (which uses a different UTF-8 single-quote char)
            //   is not equivalent to below.
            string strongsKeyword = "Strong’s ";
            // Chars which aren't permitted in "some-word", i.e., between left-paren and slash 
            char[] illegitimateChars = new char[]
                {' ', ',', '.', ';', '?', '!', '<', '>'};

            const int MAX_SIZE_OF_SOME_WORD = 25;   // max size of word like "ἐπιούσιος"
                                                    // +20 is for all not inc. word "ἐπιούσιος" in "(ἐπιούσιος/Strong’s 1967"
                                                    // This might run past encoding
            const int TOTAL_MAX_SIZE_OF_STRONGS_REFERENCE = MAX_SIZE_OF_SOME_WORD + 20;  //

            // 'isolatedText' is a fragment of 'inString' which is easier on the eyes
            int inStringLength = inString.Length;
            int spanOfIsolatedText = inStringLength - offset;
            if (spanOfIsolatedText > TOTAL_MAX_SIZE_OF_STRONGS_REFERENCE)
                spanOfIsolatedText = TOTAL_MAX_SIZE_OF_STRONGS_REFERENCE;
            string isolatedText = inString.Substring(offset, spanOfIsolatedText);

            if (isolatedText.StartsWith("("))
            {
                // 'scanIndex' is index in 'isolatedText', NOT index in 'inString'
                int scanIndex = 1;        // point to next char after left-paren
                int slashIndex = isolatedText.IndexOf("/", scanIndex);
                int illegitimateCharIndex = isolatedText.IndexOfAny(illegitimateChars, scanIndex);

                // Was a slash found within MAX_SIZE_OF_SOME_WORD of the left-paren?
                bool hasSlash = slashIndex != -1 && (slashIndex - offset <= MAX_SIZE_OF_SOME_WORD);
                // Are there any illegitimat chars between the left-paren and the slash?
                bool hasIllegitimateChars = illegitimateCharIndex != 0 && hasSlash
                                            && illegitimateCharIndex < slashIndex;

                if (hasSlash && !hasIllegitimateChars)
                {
                    displayWord = isolatedText.Substring(1, slashIndex - 1);   // 1/-1 to not inc. left-paren

                    scanIndex = slashIndex + 1;   // point to next char after slash
                    bool isStrongsKeyword = isolatedText.IndexOf(strongsKeyword, scanIndex) == scanIndex;

                    if (isStrongsKeyword)
                    {
                        scanIndex += strongsKeyword.Length;

                        bool isHebrewNumber = isolatedText[scanIndex] == 'H';
                        bool isGreekNumber = isolatedText[scanIndex] == 'G';
                        bool neitherIsExplicitlySelected = !(isHebrewNumber || isGreekNumber);
                        isHebrewNotGreek = isHebrewNumber;

                        if (!neitherIsExplicitlySelected)  // skip past 'H' or 'G'
                            scanIndex++;

                        bool isNumber = Utils.ScanDecimalFromStream(ref isolatedText,
                                                                    scanIndex,
                                                                    out strongsNumber,
                                                                    out int lengthOfStrongsNumber);
                        if (isNumber)
                        {
                            scanIndex += lengthOfStrongsNumber;   // skip over number

                            // Some Strong's numbers have a letter after them,
                            //  for example "01234a".
                            trailingStrongsLetter = isolatedText[scanIndex];
                            trailingStrongsLetter = char.ToLower(trailingStrongsLetter);
                            hasletterAtEndOfStrongsNumber = trailingStrongsLetter >= 'a' && trailingStrongsLetter <= 'z';
                            if (hasletterAtEndOfStrongsNumber)
                            {
                                scanIndex++;
                            }

                            bool hasRightParen = isolatedText[scanIndex] == ')';
                            if (hasRightParen)
                            {
                                charsConsumed = scanIndex;

                                return true;

                            }
                        }
                    }
                }
            }

            return false;
        }

        // wrap "some-word" in USX encoding for Strong's reference and replace
        // original text. Note that this is tight scanning and being even a single bit off will cause
        // the scan to fail. One thing in particular to note is that the reference text must be of the
        // same char style in order to match.
        //
        // Ex: "blah-blah-blah (ἐπιούσιος/Strong’s 1967) blah-blah"
        private static void ApplyStrongsNumbersTransformations(ref string inString, out string outString)
        {
            outString = "";

            int previousCopyFromIndex = 0;

            int inStringLength = inString.Length;
            for (int i = 0; i < inStringLength; i++)
            {
                bool justDidConversion = false;

                // Save CPU cycle and FFWD to next left paren
                int leftParenIndex = inString.IndexOf("(", i);
                if (leftParenIndex != -1)
                {
                    bool parseOk = ParseStrongsReference(ref inString,
                                                         leftParenIndex,
                                                         out int strongsNumber,
                                                         out bool hasLetterAtEndOfStrongsNumber,
                                                         out char trailingStrongsLetter,
                                                         out bool isHebrewNotGreek,
                                                         out string displayWord,
                                                         out int strongsReferenceTotalNumberOfChars);
                    if (parseOk)
                    {
                        // ex outputs:
                        //    "<char style="w" strong="G05485">gracious</char>"
                        //    "<char style="w" strong="G05485:a">gracious</char>"
                        //
                        string usxStrongsEncoding = UsxCharEncode_StrongsReference(displayWord,
                                                                                   isHebrewNotGreek,
                                                                                   strongsNumber,
                                                                                   hasLetterAtEndOfStrongsNumber,
                                                                                   trailingStrongsLetter);

                        // Copy text previous to this Strong's reference
                        int previousCopyLength = leftParenIndex - previousCopyFromIndex;
                        string previousStringToCopy = inString.Substring(previousCopyFromIndex, previousCopyLength);
                        outString += previousStringToCopy;

                        // Copy new encoding text
                        outString += usxStrongsEncoding;

                        // Advance counters/pointers. Skip to next char past the old reference.
                        i = leftParenIndex + strongsReferenceTotalNumberOfChars - 1;              // -1 b/c loop counter will advance
                        previousCopyFromIndex = i + 1;

                        justDidConversion = true;
                    }
                }

                // No left paren found? We're done. Copy over last
                if (leftParenIndex == -1)
                {
                    int finalCopyLength = inStringLength - previousCopyFromIndex;
                    string finalTextToCopyOver = inString.Substring(previousCopyFromIndex, finalCopyLength);
                    outString += finalTextToCopyOver;
                    break;
                }
                // We encountered a left paren or that and some more text that at first
                //  glance could've been the start of a Strong's coversion, but turned
                //  out to be just an left-paren.
                else if (!justDidConversion)
                {
                    // Skip past left paren and keep looking
                    i = leftParenIndex;        // loop counter will inc. 'i' and make it point to next char
                }
            }
        }

        // Two substitutions done:
        // 1) DocxTextScanner (like <i|xyz|i>)
        //       Converted to usx char formatting, like <char style="it">xyz</char>
        // 2) XML substitutions
        //       like &lt;
        //
        // 'outString_Unabridged' and 'outString':
        //    'outString' is for the abridged edition.
        //    The abridged edition has no footnotes+no footnote references
        private static void ApplyCharacterFormattingToSingleString(ref string inString,
                                                                   out string outString_Unabridged,
                                                                   out string outString,
                                                                   BookEnum bookEnum,
                                                                   int chapterNumber,
                                                                   int verseNumber)
        {
            outString_Unabridged = "";
            outString = "";

            if (inString == null || inString.Length == 0)
            {
                return;
            }

            MarkerEnum[] marker = new MarkerEnum[]
                                  {
                                       MarkerEnum.ITALICS,
                                       MarkerEnum.SUPERSCRIPT,
                                       //MarkerEnum.SUBSCRIPT,   // not supported in usx (as of this writing)
                                       MarkerEnum.BOLD,
                                       MarkerEnum.RED_TEXT,
                                       MarkerEnum.SMALL_CAPS_CHAR,
                                       MarkerEnum.SMALL_CAPS
                                  };
            // Must be 1-to-1 with above
            string[] usxMarkupText = new string[]
            {
                "<char style=\"it\">",
                "<char style=\"sup\">",
                //"<char style=\"\">",
                "<char style=\"bd\">",
                "<char style=\"wj\">",
                "<char style=\"sc\">",     // 
                "<char style=\"sc\">",     // same as above
            };
            string usxEndingText = "</char>";

            string preStrongsInputString_Unabridged = "";
            string preStrongsInputString = "";

            int lastNonFormattedTextCopyOverIndex = 0;

            for (int i = 0; i < inString.Length; i++)
            {
                int leftAngleBracketIndex = inString.IndexOf('<', i);

                // Found a left-angle bracket?
                if (leftAngleBracketIndex != -1)
                {
                    // Is this a hypertext markup?
                    // If so, extract the contents
                    bool isHyperText = ScanHyperText(ref inString,
                                                     leftAngleBracketIndex,
                                                     out MarkerEnum[] markersForThisMarkup,
                                                     out string innerText,
                                                     out int charsConsumed);

                    if (isHyperText)
                    {
                        // ==> start sanity check
                        // This checks code sanity NOT MS Word contents.
                        // It should pass.
                        int supportedMarkupCount = 0;
                        for (int q = 0; q < markersForThisMarkup.Length; q++)
                        {
                            for (int r = 0; r < marker.Length; r++)
                            {
                                if (marker[r] == markersForThisMarkup[q])
                                {
                                    supportedMarkupCount++;
                                    break;
                                }
                            }
                        }
                        if (supportedMarkupCount != markersForThisMarkup.Length)
                        {
                            Utils.LogFatal("{0} chapter {1} v{2}: Sanity check fail in ApplyCharacterFormatting(), fix code!", bookEnum, chapterNumber, verseNumber);
                        }
                        // ==> end sanity check

                        bool isVerseNumberOrFootnote = ScanVerseNumberOrFootnoteReference(ref inString,
                                                               leftAngleBracketIndex,
                                                               out int verseNumberThrowaway,
                                                               out bool isVerseNumberNotFootnote,
                                                               out string footnoteLettersThrowaway,
                                                               out int charsConsumedFootnoteThrowaway,
                                                               bookEnum,
                                                               chapterNumber);
                        bool isFootnote = isVerseNumberOrFootnote && !isVerseNumberNotFootnote;
                        bool isItalics = markersForThisMarkup.Length == 1 && markersForThisMarkup[0] == MarkerEnum.ITALICS;

                        // Copy previous text.
                        // Apply XML translations, first, however.
                        // (This text will be the same for abridged and non-abridged)
                        int previousUnformattedTextLength = leftAngleBracketIndex - lastNonFormattedTextCopyOverIndex;
                        string previousUnformattedText = inString.Substring(lastNonFormattedTextCopyOverIndex, previousUnformattedTextLength);
                        ApplyXmlCharacterSubstitutions(ref previousUnformattedText, out string previousUnformattedButXmlTranslatedText);
                        preStrongsInputString_Unabridged += previousUnformattedButXmlTranslatedText;
                        preStrongsInputString += previousUnformattedButXmlTranslatedText;

                        // Apply usx-style hypertext markup(s)
                        // This substitutes the DocxTextScanner-style markups (ex: <i|xyz|i>)
                        //   with the usx-equivalent (ex: <char style="it>).
                        // Note that usx markups have one style per markup,
                        //   whereas the DocxTextScanner markups can/do have multiple styles per markup.
                        // Therefore, we must nest multiple usx markups
                        for (int q = 0; q < markersForThisMarkup.Length; q++)
                        {
                            int index = Array.IndexOf(marker, markersForThisMarkup[q]);
                            preStrongsInputString_Unabridged += usxMarkupText[index];
                            if (!(isFootnote || isItalics))
                                preStrongsInputString += usxMarkupText[index];
                        }

                        // Apply XML translations to marked up inner text.
                        // Then add it too.
                        // We want to strip the footnotes entirely from the unabridged
                        //   edition, but want to remove the italics formatting but keep the text.
                        ApplyXmlCharacterSubstitutions(ref innerText, out string xmlTranslatedInnerText);
                        preStrongsInputString_Unabridged += xmlTranslatedInnerText;
                        if (!isFootnote)
                            preStrongsInputString += xmlTranslatedInnerText;

                        // Apply closings for usx markup(s)
                        for (int q = 0; q < markersForThisMarkup.Length; q++)
                        {
                            preStrongsInputString_Unabridged += usxEndingText;
                            if (!(isFootnote || isItalics))
                                preStrongsInputString += usxEndingText;
                        }

                        // Advance pointers
                        i = leftAngleBracketIndex + charsConsumed - 1;   // loop counter will inc. +1
                        lastNonFormattedTextCopyOverIndex = i + 1;       // point to next char after closing right angle bracket of hypertext
                    }
                    // Just a reqular angle bracket? (Not the beginning of a hypertext?)
                    // Format it and pass it through
                    else
                    {
                        string angleBracketString = inString[leftAngleBracketIndex].ToString();
                        ApplyXmlCharacterSubstitutions(ref angleBracketString, out string xmlTranslatedAngleBracketString);
                        preStrongsInputString_Unabridged += xmlTranslatedAngleBracketString;
                        preStrongsInputString += xmlTranslatedAngleBracketString;    // same for unabridged

                        // No need to advance 'i'
                        lastNonFormattedTextCopyOverIndex = i + 1;   // step over this non-hypertext right angle bracket
                    }
                }
                // We didn't find a left angle from 'i' to end of string.
                // Apply finishing copy-over and we're done
                else
                {
                    int previousUnformattedTextLength = inString.Length - lastNonFormattedTextCopyOverIndex;
                    string previousUnformattedText = inString.Substring(lastNonFormattedTextCopyOverIndex, previousUnformattedTextLength);
                    ApplyXmlCharacterSubstitutions(ref previousUnformattedText, out string previousUnformattedButXmlTranslatedText);
                    preStrongsInputString_Unabridged += previousUnformattedButXmlTranslatedText;
                    preStrongsInputString += previousUnformattedButXmlTranslatedText;

                    break;
                }
            }

            // A bit of a hack putting this at the end...Apply Strong's transformations.
#pragma warning disable CS8629
            if (SCAN_AND_MARKUP_STRONGS_NUMBERS.Value)
#pragma warning restore
            {
                ApplyStrongsNumbersTransformations(ref preStrongsInputString_Unabridged, out outString_Unabridged);
                ApplyStrongsNumbersTransformations(ref preStrongsInputString, out outString);
            }
            else
            {
                outString_Unabridged = preStrongsInputString_Unabridged;
                outString = preStrongsInputString;
            }
        }

        // Break this up according to XML standard.
        // Replace first space character after 'targetMaxCharsPerLine' and start a new line
        private static void BreakStringIntoMultipleStrings(ref string inString, int targetMaxCharsPerLine, out string[] multipleStrings)
        {
            multipleStrings = new string[500];
            int numStrings = 0;

            int lastCopyFromIndex = 0;

            int maxLength = inString.Length;
            for (int i = 0; i < maxLength; i++)
            {
                int remainingLengthToEndOfString = maxLength - i;

                int thisCopyLength = targetMaxCharsPerLine;

                int compensationForEliminatedSpaceChar = 0;

                // Remaining chars in 'inString' fit within target length?
                // Then no need to break this up further
                if (thisCopyLength >= remainingLengthToEndOfString)
                {
                    thisCopyLength = remainingLengthToEndOfString;
                }
                else
                {
                    // Search for next space char after target length
                    bool foundSpaceChar = false;
                    for (int k = i + thisCopyLength; k < maxLength; k++)
                    {
                        if (inString[k] == ' ')
                        {
                            // NOTE: according to XML spec, when a continuation line
                            //   is encountered, XML will insert a single space
                            //   when combining into one final line.
                            thisCopyLength = k - i;
                            compensationForEliminatedSpaceChar = 1;
                            foundSpaceChar = true;
                            break;
                        }
                    }

                    // If no space can be found to end of string, then just copy
                    //   over remainder of string. That's the best that can be done.
                    if (!foundSpaceChar)
                    {
                        thisCopyLength = remainingLengthToEndOfString;
                    }
                }

                string singleLineString = inString.Substring(lastCopyFromIndex, thisCopyLength);
                multipleStrings[numStrings++] = singleLineString;

                lastCopyFromIndex += thisCopyLength + compensationForEliminatedSpaceChar;
                i = lastCopyFromIndex - 1;    // -1 b/c loop counter will increment
            }

            Array.Resize(ref multipleStrings, numStrings);
        }



    }
}