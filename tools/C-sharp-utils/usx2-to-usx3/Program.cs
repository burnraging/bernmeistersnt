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
using common_dll;

class ConsoleWrapper   // usx2-to-usx3
{

    private const string USX2_TO_USX3_VERSION = "1.0";

    private static void HelpText()
    {
        Console.WriteLine("usx2-to-usx3 version {0}", USX2_TO_USX3_VERSION);
        Console.WriteLine("usx2-to-usx3 -i input-folder -o output-folder -t translation-name [-c copyright-file-name]");
        Console.WriteLine("usx2-to-usx3 -h");
        Environment.Exit(0);
    }

    static void Main(string[] args)
    {
        // delete these 4
        //ToUsx3.TOUSX3_INPUT_FOLDER = "usx2input";
        //ToUsx3.TOUSX3_OUTPUT_FOLDER = "usx3output";
        //ToUsx3.TOUSX3_TRANSLATION_NAME = "KJV";
        //ToUsx3.TOUSX3_COPYRIGHT_TEXT = new string[] { "insert-copyright" };

#if DEBUG
        args = new string[] { "-i", @"E:\Bernie\Ekklesia\the-bernmeisters-nt\C-sharp-utils\usx2input",      // input files
                              "-o", @"E:\Bernie\Ekklesia\the-bernmeisters-nt\C-sharp-utils\usx3output",
                              "-t", "KJV",
                              "-c", @"usx2to3-copyright.txt"};    // output files
#endif

        if (args == null || args.Length == 0)
        {
            HelpText();
            throw new Exception("suppresses warning");
        }

        int argsIndex = 0;
        string? inputFolder = null;
        string? outputFolder = null;
        string? translationName = null;
        string? copyrightFileName = null;

        while (argsIndex < args.Length)
        {
            if (args[argsIndex].ToLower() == "-i" && argsIndex + 1 < args.Length)
            {
                inputFolder = args[argsIndex + 1];
                argsIndex += 2;
            }
            else if (args[argsIndex].ToLower() == "-o" && argsIndex + 1 < args.Length)
            {
                outputFolder = args[argsIndex + 1];
                argsIndex += 2;
            }
            else if (args[argsIndex].ToLower() == "-o" && argsIndex + 1 < args.Length)
            {
                outputFolder = args[argsIndex + 1];
                argsIndex += 2;
            }
            else if (args[argsIndex].ToLower() == "-o" && argsIndex + 1 < args.Length)
            {
                outputFolder = args[argsIndex + 1];
                argsIndex += 2;
            }
            else if (args[argsIndex].ToLower() == "-t" && argsIndex + 1 < args.Length)
            {
                translationName = args[argsIndex + 1];
                argsIndex += 2;
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
        {
            Console.WriteLine("-i option missing");
            Environment.Exit(1);
        }
        else if (outputFolder == null)
        {
            Console.WriteLine("-o option missing");
            Environment.Exit(1);
        }
        else if (translationName == null)
        {
            Console.WriteLine("-t option missing");
            Environment.Exit(1);
        }

        string fqCopyrightFileName = "";
        bool hasCopyrightFile = false;
        if (copyrightFileName != null && copyrightFileName.Length > 0)
        {
            Utils.ConcatenateFqFileName(copyrightFileName, outputFolder, out fqCopyrightFileName);
            hasCopyrightFile = fqCopyrightFileName != null && fqCopyrightFileName.Length > 0;
        }

        if (inputFolder == null)
            throw new Exception("suppresses warning");

        ToUsx3.TOUSX3_INPUT_FOLDER = inputFolder;
        ToUsx3.TOUSX3_OUTPUT_FOLDER = outputFolder;
        ToUsx3.TOUSX3_TRANSLATION_NAME = translationName;

        Console.WriteLine("Using:");
        Console.WriteLine(" Input folder =           {0}", inputFolder);
        Console.WriteLine(" Output folder =          {0}", outputFolder);
        Console.WriteLine(" Translation name =       {0}", translationName);
        Console.WriteLine(" Copyright fq file name = {0}", hasCopyrightFile? fqCopyrightFileName : "[no copyright file]");

        if (hasCopyrightFile)
        {
            if (!File.Exists(fqCopyrightFileName))
            {
                Console.WriteLine("Copyright file not found!");
                Environment.Exit(1);
            }

            ToUsx3.TOUSX3_COPYRIGHT_TEXT = File.ReadAllLines(fqCopyrightFileName);
        }


        ToUsx3.ConvertBible();
        //DELETE??? SusxConverter.ReadBible(TOUSX3_OUTPUT_FOLDER, out SusxConverter.TreeObject[][] allUsxHeaps, out BookEnum[] allBookEnums);
    }
}
