using Shell32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PhotoSorter
{
    /// <summary>
    /// Sorts photos (and videos) from a source folder to year and month folders in a destination folder.
    /// Does not copy duplicates. 
    /// </summary>
    public class Sorter
    {
        private string _sourceFolder;
        private string _destinationFolder;
        private FolderHash _folderHash;
        private string _folderHashFolder;
        private readonly Regex r = new Regex(":");

        private readonly string[] ShellColumns = new string[] {
            "Media created",
            "Date taken"
        };

        private readonly string[] FilenamePatterns = new string[] {
            "IMG_",
            "BURST",
            "IMG-",
            "GIF_Action_"
        };

        private const string UnknownDateFolder = "An Unknown Date";

        public event EventHandler<SorterLogEventArgs> Log;

        /// <summary>
        /// Sorts photos (and videos) from a source folder to year and month folders in a destination folder.
        /// Does not copy duplicates. 
        /// </summary>
        /// <param name="sourceFolder">Source folder (must exist)</param>
        /// <param name="destinationFolder">Destination folder (must exist)</param>
        public Sorter(string sourceFolder, string destinationFolder)
        {
            if (string.IsNullOrWhiteSpace(sourceFolder)) { throw new ArgumentNullException("sourceFolder"); }
            if (string.IsNullOrWhiteSpace(destinationFolder)) { throw new ArgumentNullException("destinationFolder"); }
            if (!Directory.Exists(sourceFolder)) { throw new DirectoryNotFoundException($"{sourceFolder} does not exist"); }
            if (!Directory.Exists(destinationFolder)) { throw new DirectoryNotFoundException($"{destinationFolder} does not exist"); }

            _sourceFolder = sourceFolder;
            _destinationFolder = destinationFolder;
        }

        /// <summary>
        /// Sort the photos (and videos) from source to destination
        /// </summary>
        public void Sort()
        {
            DirectoryInfo di = new DirectoryInfo(_sourceFolder);
            FileInfo[] files = di.GetFiles("*.*", SearchOption.AllDirectories);

            OnLog($"Processing {files.Length:n0} files from {_sourceFolder} to {_destinationFolder}.");

            foreach (FileInfo file in files)
            {
                if (file.Length == 0) { continue; }

                DateTime dateTaken = GetDateTaken(file.FullName);
                string dateFolder = dateTaken == DateTime.MinValue ? UnknownDateFolder : dateTaken.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                string destFolder = Path.Combine(_destinationFolder, dateFolder);

                if (!Directory.Exists(destFolder))
                {
                    Directory.CreateDirectory(destFolder);
                }

                // load folder hashes if needed
                if ((_folderHash == null) || (_folderHashFolder != dateFolder))
                {
                    _folderHash = new FolderHash(destFolder);
                    _folderHashFolder = dateFolder;
                }

                // hash the current file
                string hash = FolderHash.GetHashForPath(file.FullName);
                if (_folderHash.ContainsHash(hash))
                {
                    OnLog($"{file.Name} already exists in {destFolder}, deleting.");
                    File.Delete(file.FullName);
                }
                else
                {
                    // make sure we have a unique filename
                    int unique = 0;
                    string destfile;
                    while (true)
                    {
                        if (unique == 0)
                        {
                            destfile = Path.Combine(destFolder, file.Name);
                        }
                        else
                        {
                            destfile = Path.Combine(destFolder, string.Format("{0}_{1:0000}{2}", Path.GetFileNameWithoutExtension(file.Name), unique, Path.GetExtension(file.Name)));
                        }
                        if (!File.Exists(destfile)) { break; }
                        unique++;
                    }

                    OnLog($"Moving {file.Name} to {destfile}.");

                    // move the file
                    File.Move(file.FullName, destfile);

                    // add to the folder hash
                    _folderHash.AddFile(Path.GetFileName(destfile), hash);
                }
            }
        }

        private DateTime GetDateTaken(string path)
        {
            DateTime taken = DateTime.MinValue;

            // try image proprties first - most likely to work
            taken = GetDateFromPropertyItem(path);
            
            if (taken == DateTime.MinValue)
            {
                // try shell columns next...
                foreach(string column in ShellColumns)
                {
                    taken = GetDateFromShellColumn(path, column);
                    if (taken > DateTime.MinValue) { break; }
                }
            }

            if (taken == DateTime.MinValue)
            {
                // try some filename patterns
                foreach(string pattern in FilenamePatterns)
                {
                    taken = GetDateFromFilename(path, pattern);
                    if (taken > DateTime.MinValue) { break; }   
                }
            }

            // patch bad video dates... https://feedback.photoshop.com/photoshop_family/topics/mac-lightroom-mp4-creation-date-always-66-years-off
            if ((taken > DateTime.MinValue) && (taken.Year < 1970))
            {
                taken = taken.AddYears(66);
            }

            return taken;
        }

        private DateTime GetDateFromPropertyItem(string path)
        {
            DateTime date = DateTime.MinValue;

            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    using (Image myImage = Image.FromStream(fs, false, false))
                    {
                        PropertyItem propItem = myImage.GetPropertyItem(36867);
                        string dateTaken = r.Replace(Encoding.UTF8.GetString(propItem.Value), "-", 2);
                        return DateTime.Parse(dateTaken);
                    }
                }
            }
            catch (Exception)
            {
                // keep trying...
            }

            return date;
        }

        private DateTime GetDateFromFilename(string path, string prefix)
        {
            DateTime date = DateTime.MinValue;

            try
            {
                int index = path.IndexOf(prefix);
                if (index >= 0)
                {
                    string year = path.Substring(index + prefix.Length, 4);
                    string month = path.Substring(index + prefix.Length + 4, 2);
                    string day = path.Substring(index + prefix.Length + 6, 2);
                    date = new DateTime(Convert.ToInt32(year), Convert.ToInt32(month), Convert.ToInt32(day));
                }
            }
            catch (Exception )
            {
                // keep trying...
            }

            return date;
        }

        private static DateTime GetDateFromShellColumn(string path, string column)
        {
            // media created, see https://stackoverflow.com/questions/8351713/how-can-i-extract-the-date-from-the-media-created-column-of-a-video-file
            // also see https://stackoverflow.com/questions/22382010/what-options-are-available-for-shell32-folder-getdetailsof
            try
            {
                Shell shell = new Shell();
                Folder folder = shell.NameSpace(Path.GetDirectoryName(path));

                // find the right property
                int mediaCreated = 0;
                for (int i = 0; i < 0xFFFF; i++)
                {
                    string name = folder.GetDetailsOf(null, i);
                    if (name == column)
                    {
                        mediaCreated = i;
                        break;
                    }
                }

                FolderItem file = folder.ParseName(Path.GetFileName(path));
                string value = folder.GetDetailsOf(file, mediaCreated);

                if (!string.IsNullOrWhiteSpace(value))
                {
                    StringBuilder clean = new StringBuilder();
                    foreach (char c in value)
                    {
                        if (c == (char)8206) { continue; }
                        if (c == (char)8207) { continue; }
                        clean.Append(c);
                    }
                    value = clean.ToString().Trim();
                    return DateTime.Parse(value);
                }
            }
            catch (Exception)
            {
                // keep trying
            }

            return DateTime.MinValue;
        }

        private void OnLog(string logMessage)
        {
            if (string.IsNullOrWhiteSpace(logMessage)) { return; }
            Log?.Invoke(this, new SorterLogEventArgs(logMessage));
        }
    }
}
