using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PhotoSorter
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Version version = Assembly.GetEntryAssembly().GetName().Version;

            Console.WriteLine($"PhotoSorter {version.Major}.{version.Minor:00} by Robert Ellison");
            Console.WriteLine($"Full instructions at https://ithoughthecamewithyou.com/");

            string sourceFolder;
            string destinationFolder;

            if (TryParseArgs(args, out sourceFolder, out destinationFolder))
            {
                try
                {
                    Sorter sorter = new Sorter(sourceFolder, destinationFolder);
                    sorter.Log += Sorter_Log;
                    sorter.Sort();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    Console.WriteLine();
                    Console.WriteLine(ex);
                }
            }
            else
            {
                Console.WriteLine("Usage: PhotoSorter [Source Folder] [Destination Folder]");
                Console.WriteLine("Use full paths to folders, folders must exist");
            }
        }

        private static void Sorter_Log(object sender, SorterLogEventArgs e)
        {
            Console.WriteLine(e.LogMessage);
        }

        private static bool TryParseArgs(string[] args, out string sourceFolder, out string destinationFolder)
        {
            sourceFolder = null;
            destinationFolder = null;
            bool argsGood = false;

            if ((args != null) && (args.Length == 2))
            {
                sourceFolder = args[0];
                destinationFolder = args[1];

                if (Directory.Exists(sourceFolder) && Directory.Exists(destinationFolder))
                {
                    argsGood = true;
                }
            }

            return argsGood;
        }
    }
}
