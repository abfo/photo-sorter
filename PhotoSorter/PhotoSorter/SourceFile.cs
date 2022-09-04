using System;
using System.Text;
using System.IO;
using Shell32;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;
using System.Globalization;

namespace PhotoSorter
{
    /// <summary>
    /// A source file that might be copied to the destination
    /// </summary>
    public class SourceFile
    {
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

        private readonly Regex r = new Regex(":");

        /// <summary>
        /// FileInfo for the file
        /// </summary>
        public FileInfo FileInfo { get; private set; }

        /// <summary>
        /// Date the file was taken, might be DateTime.MinValue if no date available
        /// </summary>
        public DateTime DateTaken { get; private set; }

        /// <summary>
        /// Destination subfolder, either 2022-09 or An Unknown Date
        /// </summary>
        public string DestFolder { get; private set; }

        /// <summary>
        /// Filename witout parenthesized numbers and spaces, used to check for likely source duplicates
        /// </summary>
        public string DuplicateCheckFileName { get; private set; }

        /// <summary>
        /// True if this is the duplicate that has been rejected for copying
        /// </summary>
        public bool IsRejectedDuplicate { get; set; }

        /// <summary>
        /// A source file that might be copied to the destination
        /// </summary>
        /// <param name="fileInfo">FileInfo for the file</param>
        public SourceFile(FileInfo fileInfo)
        {
            if (fileInfo == null) { throw new ArgumentNullException(nameof(fileInfo)); }
            FileInfo = fileInfo;
            DateTaken = GetDateTaken(FileInfo.FullName);
            DestFolder = DateTaken == DateTime.MinValue ? UnknownDateFolder : DateTaken.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            IsRejectedDuplicate = false;

            SetDuplicateCheckFileName();
        }

        private void SetDuplicateCheckFileName()
        {
            StringBuilder sb = new StringBuilder();

            bool copyChar = true;
            for(int i = 0; i < FileInfo.Name.Length; i++)
            {
                char c = FileInfo.Name[i];

                switch (c)
                {
                    case '(':
                        copyChar = false;
                        break;

                    case ')':
                        copyChar = true;
                        break;

                    case ' ':
                        // don't copy spaces
                        break;

                    default:
                        if (copyChar)
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }

            DuplicateCheckFileName = sb.ToString();
        }

        private DateTime GetDateTaken(string path)
        {
            DateTime taken = DateTime.MinValue;

            // try image proprties first - most likely to work
            taken = GetDateFromPropertyItem(path);

            if (taken == DateTime.MinValue)
            {
                // try shell columns next...
                foreach (string column in ShellColumns)
                {
                    taken = GetDateFromShellColumn(path, column);
                    if (taken > DateTime.MinValue) { break; }
                }
            }

            if (taken == DateTime.MinValue)
            {
                // try some filename patterns
                foreach (string pattern in FilenamePatterns)
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
            catch (Exception)
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
    }
}
