


class ConsoleWrapper   // verse-numbering
{
    // delete these later
    // public static string BASE_DIR = @"E:\Bernie\Ekklesia\the-bernmeisters-nt\C-sharp-utils";
    // public static string DOCX_TEXT_FILE_NAME = @"the-bernmeisters-new-testament-217-DESTRUCTIVE.txt";

    //** verse-scanner
    //public static string DOCX_TEXT_FILE_NAME_FOR_VERSE_SCANNER_ONLY = @"the-bernmeisters-new-testament-215-DESTRUCTIVE.txt";  // todo: merge with 'DOCX_TEXT_FILE_NAME' when common file format is used
    //public static string VERSES_BY_LINES_FILE_NAME = @"verses-by-lines.txt";

    private static void HelpText()
    {
        Console.WriteLine("verse-numbering [-i input-folder] -d input-file-name -o output-folder -f output-file-name");
        Console.WriteLine("verse-numbering -h");
        Environment.Exit(0);
    }
    static void Main(string[] args)
    {
        // *** deleteme start
        //verse_numbering.Top.INPUT_FOLDER = @"E:\Bernie\Ekklesia\the-bernmeisters-nt\C-sharp-utils";
        //verse_numbering.Top.INPUT_FILE_NAME = @"the-bernmeisters-new-testament-217-DESTRUCTIVE.txt";
        //verse_numbering.Top.OUTPUT_FOLDER = verse_numbering.Top.INPUT_FOLDER;
        //verse_numbering.Top.VERSES_BY_LINE_OUTPUT_FILE_NAME = @"verses-by-lines.txt";
        // **** deleteme end
#if DEBUG
        args = new string[] { "-i", @"E:\Bernie\Ekklesia\the-bernmeisters-nt\git-repo\tools\intermediate-files",  // input file's folder
                              "-d", @"the-bernmeisters-new-testament-223-DESTRUCTIVE-verse-numbering-MARKED-UP.txt",            // docx text file name (NOTE: different than usx-gen version!)
                              "-o", @"E:\Bernie\Ekklesia\the-bernmeisters-nt\git-repo\tools\intermediate-files",  //
                              "-f", @"verses-by-lines.txt" };
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
        string? outputFileName = null;

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
            else if (args[argsIndex].ToLower() == "-f" && argsIndex + 1 < args.Length)
            { 
                outputFileName = args[argsIndex + 1];
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

        verse_numbering.Top.INPUT_FOLDER = inputFolder;
        verse_numbering.Top.INPUT_FILE_NAME = docxTextFileName;
        verse_numbering.Top.OUTPUT_FOLDER = outputFolder;
        verse_numbering.Top.VERSES_BY_LINE_OUTPUT_FILE_NAME = outputFileName;

        Console.WriteLine("Using:");
        Console.WriteLine(" Input folder =  {0}", inputFolder);
        Console.WriteLine(" Input file =    {0}", docxTextFileName);
        Console.WriteLine(" Output folder = {0}", outputFolder);
        Console.WriteLine(" Output file =   {0}", outputFileName);
        Console.WriteLine("");

        verse_numbering.Top.Entry(); // BASE_DIR, DOCX_TEXT_FILE_NAME_FOR_VERSE_SCANNER_ONLY, BASE_DIR, "verse-scanner-output.txt");
    }
}
