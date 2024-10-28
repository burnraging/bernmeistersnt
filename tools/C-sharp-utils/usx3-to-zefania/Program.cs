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

class ConsoleWrapper    // usx3-to-zefania
{
    private const string USX3_TO_ZEFANIA_VERSION = "1.0";

    //DELETEME public static string BASE_DIR = @"E:\Bernie\Ekklesia\the-bernmeisters-nt\C-sharp-utils";

    // todo!!!:
    public static bool TOZ_SUPPRESS_PARA_BREAKS = true;

    private static void HelpText()
    {
        Console.WriteLine("usx3-t-zefania version {0}", USX3_TO_ZEFANIA_VERSION);
        Console.WriteLine("usx3-to-zefania -i input-folder -o output-folder -f output-file-name -s info-file-name");
        Console.WriteLine("usx3-to-zefania -h");
        Console.WriteLine("'info-file-name' is a text file that must be placed in the output folder before running this tool.");
        Console.WriteLine("'info-file-name' must have 5 lines. Each line is a different INFORMATION field:");
        Console.WriteLine("  line #1: Bible name");
        Console.WriteLine("  line #2: Bible abbreviation");
        Console.WriteLine("  line #3: Creator");
        Console.WriteLine("  line #4: Publisher");
        Console.WriteLine("  line #5: Language abbreviation");
        Environment.Exit(0);
    }

    static void Main(string[] args)
    {
        // folder containing usx files to be converted to Zefania
        //SusxConverter.TOZ_USX_SOURCE_FOLDER = "usx3output";

        //SusxConverter.TOZ_OUTPUT_FOLDER = "zefania";


        SusxConverter.ZSUPPRESS_PARA_BREAKS = true;
        SusxConverter.ZDEBUG_ADD_STYLE_ENDING_COMMENTS = false;

        SusxConverter.PRINT_DEBUG_FILES = false;
        SusxConverter.DEBUG_PRINT_USX_FOLDER = "usxprintdebug";
#if DEBUG
        args = new string[] { "-i", @"E:\Bernie\Ekklesia\the-bernmeisters-nt\git-repo\releases\usx-abridged",        // input folder, not output
                              "-o", @"E:\Bernie\Ekklesia\the-bernmeisters-nt\git-repo\releases\zefania-abridged",    // output folder
                              "-f", @"the-bernmeisters-nt-v1-zefania-2005.xml",                                      // output file name
                              "-s", @"zefania-info-file.txt"};                                                       // information fields
#endif

        if (args == null || args.Length == 0)
        {
            HelpText();
            throw new Exception("suppresses warning");
        }

        int argsIndex = 0;
        string? inputFolder = null;
        string? outputFolder = null;
        string? outputFileName = null;
        string? informationFileName = null;

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
            else if (args[argsIndex].ToLower() == "-f" && argsIndex + 1 < args.Length)
            {
                outputFileName = args[argsIndex + 1];
                argsIndex += 2;
            }
            else if (args[argsIndex].ToLower() == "-s" && argsIndex + 1 < args.Length)
            {
                informationFileName = args[argsIndex + 1];
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
        else if (informationFileName == null)
        {
            Console.WriteLine("-s option missing");
            Environment.Exit(1);
        }

        string fqInformationFileName = "";
        if (informationFileName != null && informationFileName.Length > 0)
        {
            Utils.ConcatenateFqFileName(informationFileName, outputFolder, out fqInformationFileName);
        }

        if (inputFolder == null)
            throw new Exception("suppresses warning");

        Console.WriteLine("Using:");
        Console.WriteLine(" Input folder =           {0}", inputFolder);
        Console.WriteLine(" Output folder =          {0}", outputFolder);
        Console.WriteLine(" Output file name =        {0}", outputFileName);
        Console.WriteLine(" Information file name = {0}", fqInformationFileName);

        SusxConverter.TOZ_USX_SOURCE_FOLDER = inputFolder;
        SusxConverter.TOZ_OUTPUT_FOLDER = outputFolder;
        // !!!!! fill in output file name !!!!!!!!!!!

        if (!File.Exists(fqInformationFileName))
        {
            Console.WriteLine("Information file doesn't exist!");
            Environment.Exit(1);
        }

        string[] allInformationLines = File.ReadAllLines(fqInformationFileName);

        if (allInformationLines.Length != 5)
        {
            Console.WriteLine("There must be 5 lines in information file!");
            Environment.Exit(1);
        }

        // </INFORMATION> fill-ins
        //SusxConverter.TOZ_BIBLE_NAME = "my-bible-name";
        //SusxConverter.TOZ_BIBLE_ABBREV = "ABC";
        //SusxConverter.TOZ_CREATOR = "creator-name";
        //SusxConverter.TOZ_PUBLISHER = "my-publisher";
        //SusxConverter.TOZ_LANGUAGE_ABBREV = "ENG";
        SusxConverter.TOZ_BIBLE_NAME = allInformationLines[0];
        SusxConverter.TOZ_BIBLE_ABBREV = allInformationLines[1];
        SusxConverter.TOZ_CREATOR = allInformationLines[2];
        SusxConverter.TOZ_PUBLISHER = allInformationLines[3];
        SusxConverter.TOZ_LANGUAGE_ABBREV = allInformationLines[4];


        //????? SusxConverter.ReadBible(SusxConverter.TOZ_USX_SOURCE_FOLDER, out SusxConverter.TreeObject[][] allUsxHeaps, out BookEnum[] allBookEnums);
        SusxConverter.ZConvert("zefania-output.xml");
    }


}
