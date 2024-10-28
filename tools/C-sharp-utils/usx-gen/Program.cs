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
//using common_dll;

//#define UNABRIDGED_VERSION

class ConsoleWrapper
{

    private const string USX_GEN_VERSION = "1.0";

    private static void HelpText()
    {
        Console.WriteLine("usx-gen version {0}", USX_GEN_VERSION);
        Console.WriteLine("usx-gen [-i input-folder] -d input-file-name -o output-folder [-unabridged | -abridged]");
        Console.WriteLine("usx-gen -h");
        Environment.Exit(0);
    }

    static void Main(string[] args)
    {
        // App settings not setable from command line
        common_dll.UsxConverter.ASSUME_EACH_BOOK_HAS_AN_INTRODUCTION = true;
        common_dll.UsxConverter.INTRODUCTION_USES_NO_HEADING2 = true;
        common_dll.UsxConverter.SCAN_CHAPTER_NUMBER_FROM_TITLE = true;
        common_dll.UsxConverter.MAX_CHARS_PER_LINE = 100;
        common_dll.UsxConverter.SCAN_AND_MARKUP_STRONGS_NUMBERS = true;

#if DEBUG
        // ** delete start
        //public static string DOCX_TEXT_FILE_NAME = @"the-bernmeisters-new-testament-217-DESTRUCTIVE.txt";

        //public static string INPUT_FOLDER = @"E:\Bernie\Ekklesia\the-bernmeisters-nt\C-sharp-utils";
        //public static string OUTPUT_FOLDER_OTHER = @"";
        //public static string OUTPUT_FOLDER_USX_UNABRIDGED = @"output-unabridged";
        //public static string OUTPUT_FOLDER_USX_ABRIDGED = @"output-abridged";
        // *** delete end

        args = new string[] { "-i", @"E:\Bernie\Ekklesia\the-bernmeisters-nt\git-repo\tools\intermediate-files", // intermediate file's folder
#if UNABRIDGED_VERSION
                              "-d", @"the-bernmeisters-new-testament-220-DESTRUCTIVE-usx-gen-MARKED-UP.txt",     // docx selective search&replace text file name
                              "-o", @"E:\Bernie\Ekklesia\the-bernmeisters-nt\git-repo\releases\usx-unabridged",  // unabridged edition's output folder
                              "-unabridged" };
#else
                              "-d", @"the-bernmeisters-new-testament-v1-abridged-DESTRUCTIVE-usx-gen-MARKED-UP.txt",               // docx selective search&replacetext file name
                              "-o", @"E:\Bernie\Ekklesia\the-bernmeisters-nt\git-repo\releases\usx-abridged",    // abridged edition's output folder
                              "-abridged" };
#endif

#endif

        if (args == null || args.Length == 0)
        {
            HelpText();
            throw new Exception("suppresses warning");
        }

        int argsIndex = 0;
        string? inputFolder = null;
        string? docxTextFileName = null;
        string? outputFolder = null;
        bool isUnabridgedEdition = false;

        while (argsIndex < args.Length)
        {
            if (args[argsIndex].ToLower() == "-i" && argsIndex + 1 < args.Length)
            {
                inputFolder = args[argsIndex + 1];
                argsIndex += 2;
            }
            else if (args[argsIndex].ToLower() == "-d" && argsIndex + 1 < args.Length)
            {
                docxTextFileName = args[argsIndex + 1];
                argsIndex += 2;
            }
            else if (args[argsIndex].ToLower() == "-o" && argsIndex + 1 < args.Length)
            {
                outputFolder = args[argsIndex + 1];
                argsIndex += 2;
            }
            else if (args[argsIndex].ToLower() == "-unabridged")
            {
                isUnabridgedEdition = true;
                argsIndex += 1;
            }
            else if (args[argsIndex].ToLower() == "-abridged")
            {
                // no need to set anything
                argsIndex += 1;
            }
            else if (args[argsIndex].ToLower() == "-h")
                HelpText();
            else
            {
                Console.WriteLine("Unsupported argument: {0}", args[argsIndex]);
                Environment.Exit(1);
            }
        }

        if (inputFolder == null)
            inputFolder = outputFolder;

        if (outputFolder == null)
        {
            Console.WriteLine("-o option missing");
            Environment.Exit(1);
        }
        else if (docxTextFileName == null)
        {
            Console.WriteLine("-d option missing");
            Environment.Exit(1);
        }

        if (inputFolder == null)
            throw new Exception("suppresses warning");

        Console.WriteLine("Using:");
        Console.WriteLine(" Input folder =     {0}", inputFolder);
        Console.WriteLine(" Output folder =    {0}", outputFolder);
        Console.WriteLine(" Output file name = {0}", docxTextFileName);
        Console.WriteLine(" Edition = {0}", isUnabridgedEdition ? "Unabridged" : "Abridged");
        Console.WriteLine("");

        //********  Start modify for your translation ********

        common_dll.UsxConverter.USX_TRANSLATION_VERSION = "1";

        if (!isUnabridgedEdition)
        {
            FetchCopyrightTextAbridged(out common_dll.UsxConverter.USX_COPYRIGHT_TEXT);
            common_dll.UsxConverter.USX_TRANSLATION_NAME = "The Bernmeister's NT";
            common_dll.UsxConverter.PROCESSED_TEXT_OUTPUT_FILE_NAME_ABRIDGED = @"processed-text-out-abridged.txt";
        }
        else
        {
            FetchCopyrightTextUnabridged(out common_dll.UsxConverter.USX_COPYRIGHT_TEXT);
            common_dll.UsxConverter.USX_TRANSLATION_NAME = @"The Bernmeister's NT, Unabridged";
            common_dll.UsxConverter.PROCESSED_TEXT_OUTPUT_FILE_NAME_UNABRIDGED = @"processed-text-out-unabridged.txt";
        }
        //******** End modify for your translation

        common_dll.DocxTextScanner.Init();
        common_dll.DocxTextScanner.ScanWordOutput(inputFolder, docxTextFileName);
        common_dll.DocxTextScanner.ProcessWordScan();
        // next line for debug only!
        common_dll.DocxTextScanner.PrintScannerOutput(inputFolder, "usx-gen-scanner-out.txt");
        common_dll.UsxConverter.Init();
        common_dll.UsxConverter.TransferParagraphs(ref common_dll.DocxTextScanner.AllParagraphs);
        common_dll.UsxConverter.ParseIntoVerses();
        common_dll.UsxConverter.ProcessCharacterFormatting();
        common_dll.UsxConverter.WriteProcessedResults(isUnabridgedEdition, outputFolder);
        common_dll.UsxConverter.GenerateUsxXml(isUnabridgedEdition, outputFolder);
        // delete these
        //common_dll.UsxConverter.WriteProcessedResults(!isUnabridgedEdition, inputFolder, outputFolder);    // abridged
        //common_dll.UsxConverter.GenerateUsxXml(!isUnabridgedEdition, INPUT_FOLDER); // abridged edition
    }


    private static void FetchCopyrightTextUnabridged(out string[] lines)
    {
        lines = new string[]
        {
                "The Bernmeister's New Testament, Unabridged Edition",
                "Copyright © 2024 Bernard M. Woodland",
                string.Format("Version {0}", common_dll.UsxConverter.USX_TRANSLATION_VERSION),
                "Public domain",
        };
    }

    private static void FetchCopyrightTextAbridged(out string[] lines)
    {
        lines = new string[]
        {
                "The Bernmeister's New Testament, Abridged Edition",
                "Copyright © 2024 Bernard M. Woodland",
                string.Format("Version {0}", common_dll.UsxConverter.USX_TRANSLATION_VERSION),
                "Public domain",
        };
    }

}
