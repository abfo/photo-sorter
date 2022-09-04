using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

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

        private readonly string[] DoNotMoveExtensions = new string[] {
            ".json",
            ".pshashfile"
        };

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
            OnLog($"PhotoSorter moving from {_sourceFolder} to {_destinationFolder}.");

            Dictionary<string, IList<SourceFile>> sourceFilesByMonth = LoadSourceFiles();

            foreach (string month in sourceFilesByMonth.Keys)
            {
                OnLog($"Processing {month}.");

                string destFolder = Path.Combine(_destinationFolder, month);

                if (!Directory.Exists(destFolder))
                {
                    Directory.CreateDirectory(destFolder);
                }

                _folderHash = new FolderHash(destFolder);
                _folderHashFolder = month;

                // check for any source duplicates in the month
                IList<SourceFile> files = sourceFilesByMonth[month];
                CheckForSourceDuplicates(files);

                foreach (SourceFile file in files)
                {
                    if (file.FileInfo.Length == 0) { continue; }
                    if (file.IsRejectedDuplicate) { continue; }

                    // hash the current file
                    string hash = FolderHash.GetHashForPath(file.FileInfo.FullName);
                    if (_folderHash.ContainsHash(hash))
                    {
                        OnLog($"{file.FileInfo.Name} already exists in {destFolder}, deleting.");
                        File.Delete(file.FileInfo.FullName);
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
                                destfile = Path.Combine(destFolder, file.FileInfo.Name);
                            }
                            else
                            {
                                destfile = Path.Combine(destFolder, string.Format("{0}_{1:0000}{2}", Path.GetFileNameWithoutExtension(file.FileInfo.Name), unique, Path.GetExtension(file.FileInfo.Name)));
                            }
                            if (!File.Exists(destfile)) { break; }
                            unique++;
                        }

                        OnLog($"Moving {file.FileInfo.Name} to {destfile}.");

                        // move the file
                        File.Move(file.FileInfo.FullName, destfile);

                        // add to the folder hash
                        _folderHash.AddFile(Path.GetFileName(destfile), hash);
                    }
                }
            }
        }

        private void CheckForSourceDuplicates(IList<SourceFile> files)
        {
            foreach (SourceFile file1 in files)
            {
                foreach (SourceFile file2 in files)
                {
                    if (file2 == file1) { continue; }
                    if (file2.DateTaken == DateTime.MinValue) { continue; }
                    if (!File.Exists(file1.FileInfo.FullName)) { continue; }
                    if (!File.Exists(file2.FileInfo.FullName)) { continue; }

                    if ((file1.DuplicateCheckFileName == file2.DuplicateCheckFileName)
                        && (file1.DateTaken == file2.DateTaken))
                    {
                        string victim;

                        // kill the smaller file (or the second if thye happen to be the same length
                        if (file1.FileInfo.Length >= file2.FileInfo.Length)
                        {
                            victim = file2.FileInfo.FullName;
                            file2.IsRejectedDuplicate = true;
                        } 
                        else
                        {
                            victim = file1.FileInfo.FullName;
                            file1.IsRejectedDuplicate = true;
                        }

                        string hash = FolderHash.GetHashForPath(victim);
                        bool destDeleted = _folderHash.RemoveFile(hash);

                        OnLog($"{victim} is a duplicate by filename and date taken, deleting (destination duplicate deleted = {destDeleted}).");
                        File.Delete(victim);
                    }
                }
            }
        }

        private Dictionary<string, IList<SourceFile>> LoadSourceFiles()
        {
            OnLog("Loading source files...");

            Dictionary<string, IList<SourceFile>> sourceFilesByMonth = new Dictionary<string, IList<SourceFile>>();

            DirectoryInfo di = new DirectoryInfo(_sourceFolder);
            FileInfo[] files = di.GetFiles("*.*", SearchOption.AllDirectories);

            foreach (FileInfo file in files)
            {
                bool deleted = false;

                foreach(string extension in DoNotMoveExtensions)
                {
                    if (string.Compare(extension, Path.GetExtension(file.Name), StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        OnLog($"{file.FullName} has a do not move extension, deleting.");
                        File.Delete(file.FullName);
                        deleted = true;
                        break;
                    }
                }

                if (deleted) { continue; }

                SourceFile sourceFile = new SourceFile(file);
                if (!sourceFilesByMonth.ContainsKey(sourceFile.DestFolder))
                {
                    sourceFilesByMonth.Add(sourceFile.DestFolder, new List<SourceFile>());
                }
                sourceFilesByMonth[sourceFile.DestFolder].Add(sourceFile);
            }

            return sourceFilesByMonth;
        }

        private void OnLog(string logMessage)
        {
            if (string.IsNullOrWhiteSpace(logMessage)) { return; }
            Log?.Invoke(this, new SorterLogEventArgs(logMessage));
        }
    }
}
