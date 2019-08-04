﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PhotoSorter
{
    /// <summary>
    /// Hashes contents of a folder to prevent creation of duplicates
    /// </summary>
    public class FolderHash
    {
        private readonly string _folder;
        private readonly string _savePath;
        private Dictionary<string, string> _fileHashes;

        private const string SaveFileName = ".pshashfile";

        /// <summary>
        /// Hashes contents of a folder to prevent creation of duplicates
        /// </summary>
        /// <param name="folder">Folder to hash</param>
        public FolderHash(string folder)
        {
            if(string.IsNullOrWhiteSpace(folder)) { throw new ArgumentNullException("folder"); }
            if (!Directory.Exists(folder)) { throw new DirectoryNotFoundException($"{folder} does not exist"); }

            _folder = folder;
            _savePath = Path.Combine(_folder, SaveFileName);

            Load();
        }

        /// <summary>
        /// Adds a file to the folder hash (does not copy the file)
        /// </summary>
        /// <param name="name">Filename</param>
        /// <param name="hash">The hash for the file (from GetHashForPath)</param>
        public void AddFile(string name, string hash)
        {
            if (string.IsNullOrWhiteSpace(name)) { throw new ArgumentNullException("name"); }
            if (string.IsNullOrWhiteSpace(hash)) { throw new ArgumentNullException("hash"); }

            AddFile(name, hash, true);
        }

        /// <summary>
        /// Checks to see if the folder already contains a file with a given hash
        /// </summary>
        /// <param name="hash">File hash to check for</param>
        /// <returns>True if the hash is found</returns>
        public bool ContainsHash(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash)) { throw new ArgumentNullException("hash"); }
            return _fileHashes.ContainsKey(hash);
        }

        /// <summary>
        /// Gets the (MD5) hash for a path
        /// </summary>
        /// <param name="path">Path of a file to hash</param>
        /// <returns>String containing a hash of the file (MD5)</returns>
        public static string GetHashForPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) { throw new ArgumentNullException("path"); }
            if (!File.Exists(path)) { throw new FileNotFoundException($"{path} not found"); }

            string hash;

            using (MD5 md5Hash = MD5.Create())
            {
                using (Stream s = File.OpenRead(path))
                {
                    byte[] data = md5Hash.ComputeHash(s);
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < data.Length; i++)
                    {
                        sb.Append(data[i].ToString("x2"));
                    }
                    hash = sb.ToString();
                }
            }

            return hash;
        }

        private void AddFile(string name, string hash, bool save)
        {
            if (!_fileHashes.ContainsKey(hash))
            {
                _fileHashes.Add(hash, name);

                if (save)
                {
                    Save();
                }
            }
        }

        private void Save()
        {
            string json = JsonConvert.SerializeObject(_fileHashes, Formatting.Indented);
            File.WriteAllText(_savePath, json);
        }

        private void Load()
        {
            _fileHashes = null;

            if (File.Exists(_savePath))
            {
                try
                {
                    string json = File.ReadAllText(_savePath);
                    _fileHashes = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                }
                catch (Exception)
                {
                    _fileHashes = null;
                }
            }

            if (_fileHashes == null)
            {
                _fileHashes = new Dictionary<string, string>();
            }

            HashExistingFolderContents();
        }

        private void HashExistingFolderContents()
        {
            DirectoryInfo di = new DirectoryInfo(_folder);
            FileInfo[] files = di.GetFiles("*.*", SearchOption.TopDirectoryOnly);
            foreach(FileInfo file in files)
            {
                if (file.Name == SaveFileName) { continue; }
                if (_fileHashes.ContainsValue(file.Name)) { continue; }

                string hash = GetHashForPath(file.FullName);
                AddFile(file.Name, hash, false);
            }
        }
    }
}