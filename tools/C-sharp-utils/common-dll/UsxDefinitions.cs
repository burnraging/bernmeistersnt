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
    public static class UsxDefinitions
    {
#pragma warning disable CS8618
        public static string[] BibleBookNamesText;

        private static bool DidInit = false;


        public static void LoadBibleBookNames()
        {
            if (!DidInit)
            {
                DidInit = true;

                // from https://github.com/ubsicap/usx/blob/usx3.0.7/schema/usx.rnc
                BibleBookNamesText = new string[] {
                    "GEN", // Genesis
                    "EXO", // Exodus
                    "LEV", // Leviticus
                    "NUM", // Numbers
                    "DEU", // Deuteronomy
                    "JOS", // Joshua
                    "JDG", // Judges
                    "RUT", // Ruth
                    "1SA", // 1 Samuel
                    "2SA", // 2 Samuel
                    "1KI", // 1 Kings
                    "2KI", // 2 Kings
                    "1CH", // 1 Chronicles
                    "2CH", // 2 Chronicles
                    "EZR", // Ezra
                    "NEH", // Nehemiah
                    "EST", // Esther (Hebrew)
                    "JOB", // Job
                    "PSA", // Psalms
                    "PRO", // Proverbs
                    "ECC", // Ecclesiastes
                    "SNG", // Song of Songs
                    "ISA", // Isaiah
                    "JER", // Jeremiah
                    "LAM", // Lamentations
                    "EZK", // Ezekiel
                    "DAN", // Daniel (Hebrew)
                    "HOS", // Hosea
                    "JOL", // Joel
                    "AMO", // Amos
                    "OBA", // Obadiah
                    "JON", // Jonah
                    "MIC", // Micah
                    "NAM", // Nahum
                    "HAB", // Habakkuk
                    "ZEP", // Zephaniah
                    "HAG", // Haggai
                    "ZEC", // Zechariah
                    "MAL", // Malachi
                    "MAT", // Matthew
                    "MRK", // Mark
                    "LUK", // Luke
                    "JHN", // John
                    "ACT", // Acts
                    "ROM", // Romans
                    "1CO", // 1 Corinthians
                    "2CO", // 2 Corinthians
                    "GAL", // Galatians
                    "EPH", // Ephesians
                    "PHP", // Philippians
                    "COL", // Colossians
                    "1TH", // 1 Thessalonians
                    "2TH", // 2 Thessalonians
                    "1TI", // 1 Timothy
                    "2TI", // 2 Timothy
                    "TIT", // Titus
                    "PHM", // Philemon
                    "HEB", // Hebrews
                    "JAS", // James
                    "1PE", // 1 Peter
                    "2PE", // 2 Peter
                    "1JN", // 1 John
                    "2JN", // 2 John
                    "3JN", // 3 John
                    "JUD", // Jude
                    "REV", // Revelation
                };
#pragma warning restore
            }

        }
    }
}
