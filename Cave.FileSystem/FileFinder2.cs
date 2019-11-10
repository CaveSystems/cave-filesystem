using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Cave
{
    /// <summary>
    /// Provides an asynchronous file finder.
    /// </summary>
    public sealed class FileFinder2 : IDisposable
    {
        readonly LinkedList<FileItem> fileList = new LinkedList<FileItem>();
        readonly LinkedList<string> directoryList = new LinkedList<string>();
        IFileFinderComparer[] comparer;

        void Start(string baseDirectory, string directoryMask, string fileMask, params IFileFinderComparer[] comparer)
        {
            if (DirectorySearchRunning || FileSearchRunning)
            {
                throw new InvalidOperationException("Search is already running!");
            }

            BaseDirectory = baseDirectory ?? throw new ArgumentNullException("baseDirectory");
            if (!Directory.Exists(BaseDirectory))
            {
                throw new DirectoryNotFoundException();
            }

            FileMask = fileMask;
            DirectoryMask = directoryMask;
            this.comparer = comparer;
            DirectorySearchRunning = true;
            FileSearchRunning = true;
            Task.Factory.StartNew(SearchDirectories);
            Task.Factory.StartNew(SearchFiles);
        }

        void SearchFiles()
        {
            while (true)
            {
                string currentDir;
                lock (directoryList)
                {
                    while (directoryList.Count == 0)
                    {
                        if (!DirectorySearchRunning)
                        {
                            FileSearchRunning = false;
                            return;
                        }
                        Monitor.Wait(directoryList);
                    }
                    currentDir = directoryList.First.Value;
                    directoryList.RemoveFirst();
                }

                foreach (string fileName in Directory.GetFiles(currentDir, FileMask))
                {
                    var file = FileItem.FromFullPath(BaseDirectory, fileName);
                    foreach (IFileFinderComparer comparer in comparer)
                    {
                        if (!comparer.FileMatches(file))
                        {
                            file = null;
                            break;
                        }
                    }

                    if (file != null)
                    {
                        while (FileSearchRunning)
                        {
                            lock (fileList)
                            {
                                if ((MaximumFilesQueued <= 0) || (fileList.Count < MaximumFilesQueued))
                                {
                                    FilesSeen++;
                                    fileList.AddLast(file);
                                    Monitor.Pulse(fileList);
                                    break;
                                }
                                else
                                {
                                    Monitor.Wait(fileList);
                                }
                            }
                        }
                    }
                }
            }
            throw new Exception("THIS SHOULD NEVER HAPPEN");
        }

        void SearchDirectories()
        {
            var list = new LinkedList<string>();
            list.AddFirst(BaseDirectory);
            while (list.Count > 0)
            {
                string currentDir = list.First.Value;
                lock (directoryList)
                {
                    directoryList.AddLast(currentDir);
                    Monitor.Pulse(directoryList);
                }
                list.RemoveFirst();
                DirectoriesSeen++;

                foreach (string dir in Directory.GetDirectories(currentDir, DirectoryMask))
                {
                    list.AddLast(dir);
                }
            }
            DirectorySearchRunning = false;
        }

        /// <summary>Print verbose debug messages.</summary>
#if DEBUG
        public bool VerboseMessages = true;
#else
        public bool VerboseMessages = false;
#endif

        /// <summary>
        /// Gets or sets the maximum number of files in queue.
        /// </summary>
        public int MaximumFilesQueued { get; set; }

        /// <summary>Gets a value indicating whether [the directory search is running].</summary>
        /// <value>
        /// <c>true</c> if [directory search is running]; otherwise, <c>false</c>.
        /// </value>
        public bool DirectorySearchRunning { get; private set; }

        /// <summary>Gets a value indicating whether [the file search is running].</summary>
        /// <value><c>true</c> if [file search is running]; otherwise, <c>false</c>.</value>
        public bool FileSearchRunning { get; private set; }

        /// <summary>
        /// Gets the number of files seen while searching.
        /// </summary>
        public int FilesSeen { get; private set; }

        /// <summary>
        /// Gets the number of directories seen while searching.
        /// </summary>
        public int DirectoriesSeen { get; private set; }

        /// <summary>
        /// Gets the current progress (search and reading).
        /// </summary>
        public float Progress
        {
            get
            {
                if (DirectorySearchRunning)
                {
                    lock (directoryList)
                    {
                        return directoryList.Count / DirectoriesSeen * 0.2f;
                    }
                }

                if (FileSearchRunning)
                {
                    lock (fileList)
                    {
                        return 0.2f + (fileList.Count / FilesSeen * 0.4f);
                    }
                }

                lock (fileList)
                {
                    return 0.6f + (fileList.Count / FilesSeen * 0.4f);
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileFinder2"/> class.
        /// </summary>
        /// <param name="baseDirectory">Base directory to start the search at.</param>
        /// <param name="comparer">Comparer to use during search.</param>
        public FileFinder2(string baseDirectory, params IFileFinderComparer[] comparer) => Start(baseDirectory, "*", "*", comparer);

        /// <summary>
        /// Initializes a new instance of the <see cref="FileFinder2"/> class.
        /// </summary>
        /// <param name="baseDirectory">Base directory to start the search at.</param>
        /// <param name="directoryMask">Directory mask to use during search.</param>
        /// <param name="fileMask">File mask to use during search.</param>
        /// <param name="comparer">Comparer to use during search.</param>
        public FileFinder2(string baseDirectory, string directoryMask, string fileMask, params IFileFinderComparer[] comparer) => Start(baseDirectory, directoryMask, fileMask, comparer);

        /// <summary>
        /// Retrieves all files already found. This may called repeatedly until Completed==true.
        /// </summary>
        /// <returns>Returns (dequeues) all files already found.</returns>
        public FileItem[] Get()
        {
            lock (this)
            {
                var items = new FileItem[fileList.Count];
                fileList.CopyTo(items, 0);
                fileList.Clear();
                return items;
            }
        }

        /// <summary>
        /// Retrieves (dequeues) all files already found. This may called repeatedly until Completed==true.
        /// </summary>
        /// <param name="maximum">Maximum number of items to return.</param>
        /// <returns>Returns an array of files.</returns>
        public FileItem[] Get(int maximum)
        {
            lock (this)
            {
                var result = new List<FileItem>(maximum);
                if (fileList.Count < maximum)
                {
                    maximum = fileList.Count;
                }

                for (int i = 0; i < maximum; i++)
                {
                    result.Add(fileList.First.Value);
                    fileList.RemoveFirst();
                }
                return result.ToArray();
            }
        }

        /// <summary>
        /// Retrieves the next file found.
        /// This function waits until a file is found or the search thread completes without finding any further items.
        /// </summary>
        /// <param name="waitAction">An action to call when entering wait for next search results.</param>
        /// <returns>Returns the next file found or null if the finder completed without finding any further files.</returns>
        public FileItem GetNext(Action waitAction = null)
        {
            while (FileSearchRunning)
            {
                lock (fileList)
                {
                    if (fileList.Count > 0)
                    {
                        FileItem result = fileList.First.Value;
                        fileList.RemoveFirst();
                        return result;
                    }
                    if (waitAction == null)
                    {
                        Monitor.Wait(fileList);
                    }
                }
                waitAction?.Invoke();
            }
            lock (fileList)
            {
                if (fileList.Count > 0)
                {
                    FileItem result = fileList.First.Value;
                    fileList.RemoveFirst();
                    return result;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets the file mask applied while searching.
        /// </summary>
        public string FileMask { get; private set; }

        /// <summary>
        /// Gets the directory mask applied while searching.
        /// </summary>
        public string DirectoryMask { get; private set; }

        /// <summary>
        /// Gets the base directory of the search.
        /// </summary>
        public string BaseDirectory { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the filefinder has completed the search task and all items have been read.
        /// </summary>
        public bool Completed => FileSearchRunning ? false : FilesQueued == 0;

        /// <summary>
        /// Gets the number of queued files.
        /// </summary>
        public int FilesQueued
        {
            get { lock (fileList) { return fileList.Count; } }
        }

        /// <summary>
        /// Closes the finder.
        /// </summary>
        public void Close()
        {
            DirectorySearchRunning = false;
            FileSearchRunning = false;
            lock (fileList)
            {
                fileList.Clear();
                Monitor.PulseAll(fileList);
            }
            lock (directoryList)
            {
                directoryList.Clear();
                Monitor.PulseAll(directoryList);
            }
        }

        /// <summary>
        /// Releases all resources used by the this instance.
        /// </summary>
        public void Dispose()
        {
            Close();
            GC.SuppressFinalize(this);
        }
    }
}
