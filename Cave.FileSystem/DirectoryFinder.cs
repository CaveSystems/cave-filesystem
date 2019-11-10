using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Cave
{
    /// <summary>
    /// Provides an asynchronous file finder.
    /// </summary>
    public class DirectoryFinder
    {
        /// <summary>
        /// The directory list of already found directories.
        /// </summary>
        readonly Queue<DirectoryItem> directoryList = new Queue<DirectoryItem>();

        /// <summary>
        /// the comparers used to (un)select a directory.
        /// </summary>
        IDirectoryFinderComparer[] comparer;

        /// <summary>
        /// Finder started.
        /// </summary>
        bool wasStarted;

        /// <summary>
        /// If set to true the finder returns the deepest directory first (e.g. first /tmp/some/dir then /tmp/some).
        /// </summary>
        bool deepestFirst;

        /// <summary>
        /// search currently active.
        /// </summary>
        volatile bool searchRunning;

        void RecursiveSearch(DirectoryItem current)
        {
            try
            {
                foreach (string fullDirectoryName in Directory.GetDirectories(current.FullPath, DirectoryMask))
                {
                    var directory = DirectoryItem.FromFullPath(BaseDirectory, fullDirectoryName);
                    foreach (IDirectoryFinderComparer comparer in comparer)
                    {
                        if (!comparer.DirectoryMatches(directory))
                        {
                            directory = null;
                            break;
                        }
                    }
                    if (directory != null)
                    {
                        if (deepestFirst)
                        {
                            // recursive search in directory first
                            RecursiveSearch(directory);
                        }

                        // then add items to list
                        while (searchRunning)
                        {
                            lock (this)
                            {
                                if (directoryList.Count < MaximumDirectoriesQueued)
                                {
                                    directoryList.Enqueue(directory);
                                    break;
                                }
                            }
                            Thread.Sleep(1);
                        }
                        if (!deepestFirst)
                        {
                            // recursive search later
                            RecursiveSearch(directory);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("Cannot get directory listing for {0}.\n{1}", current, ex);
            }
        }

        /// <summary>
        /// runs the current search.
        /// </summary>
        void SearchDirectories()
        {
            Thread.CurrentThread.Priority = ThreadPriority.Lowest;
            Thread.CurrentThread.IsBackground = true;
            if (VerboseMessages)
            {
                Trace.TraceError("Starting directory search at {0}", BaseDirectory);
            }

            RecursiveSearch(new DirectoryItem(BaseDirectory, "."));
            searchRunning = false;
            if (VerboseMessages)
            {
                Trace.TraceError("Completed directory search at {0}", BaseDirectory);
            }
        }

        /// <summary>
        /// Prepares the directoryfinder with the specified comparers.
        /// </summary>
        /// <param name="baseDirectory">the base directory the search starts at.</param>
        /// <param name="directoryMask">the directory mask for subdirectories.</param>
        /// <param name="comparer">the additionally used comparers.</param>
        protected void Prepare(string baseDirectory, string directoryMask, params IDirectoryFinderComparer[] comparer)
        {
            if (searchRunning)
            {
                throw new InvalidOperationException(string.Format("Search is already running!"));
            }

            if (baseDirectory == null)
            {
                throw new ArgumentNullException("baseDirectory");
            }

            BaseDirectory = Path.GetFullPath(baseDirectory);
            if (!Directory.Exists(BaseDirectory))
            {
                throw new DirectoryNotFoundException();
            }

            DirectoryMask = directoryMask;
            this.comparer = comparer;
            wasStarted = false;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DirectoryFinder"/> class.
        /// </summary>
        /// <param name="baseDirectory">the base directory the search starts at.</param>
        /// <param name="comparer">the additionally used comparers.</param>
        public DirectoryFinder(string baseDirectory, params IDirectoryFinderComparer[] comparer) => Prepare(baseDirectory, "*", comparer);

        /// <summary>
        /// Initializes a new instance of the <see cref="DirectoryFinder"/> class.
        /// </summary>
        /// <param name="baseDirectory">the base directory the search starts at.</param>
        /// <param name="directoryMask">the directory mask to apply.</param>
        /// <param name="comparer">the additionally used comparers.</param>
        public DirectoryFinder(string baseDirectory, string directoryMask, params IDirectoryFinderComparer[] comparer) => Prepare(baseDirectory, directoryMask, comparer);

        /// <summary>
        /// Retrieves the next file found.
        /// This function waits until a file is found or the search thread completes without finding any further items.
        /// </summary>
        /// <returns>Returns the next file found or null if the finder completed without finding any further files.</returns>
        public DirectoryItem GetNext()
        {
            if (!searchRunning && !wasStarted)
            {
                wasStarted = true;
                searchRunning = true;
                Task.Factory.StartNew(SearchDirectories);
            }
            while (searchRunning)
            {
                lock (this)
                {
                    if (directoryList.Count > 0)
                    {
                        return directoryList.Dequeue();
                    }
                }
                Thread.Sleep(1);
            }
            lock (this)
            {
                if (directoryList.Count > 0)
                {
                    return directoryList.Dequeue();
                }
            }
            return null;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the finder returns the deepest directory first (e.g. first /tmp/some/dir then /tmp/some).
        /// </summary>
        public bool DeepestFirst
        {
            get => deepestFirst;
            set
            {
                if (wasStarted)
                {
                    throw new InvalidOperationException(string.Format("Finder was already started!"));
                }

                deepestFirst = value;
            }
        }

        /// <summary>
        /// Gets the base directory of the search.
        /// </summary>
        public string BaseDirectory { get; private set; }

        /// <summary>
        /// Gets a directorymask used to filter the directories.
        /// </summary>
        public string DirectoryMask { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the filefinder has completed the search task and all items have been read.
        /// </summary>
        public bool Completed => !wasStarted ? false : !searchRunning;

        /// <summary>
        /// Gets or sets the maximum number of directories in queue.
        /// </summary>
        public int MaximumDirectoriesQueued { get; set; } = 20;

        /// <summary>
        /// Closes the finder.
        /// </summary>
        public void Close()
        {
            lock (this)
            {
                searchRunning = false;
            }
        }

        /// <summary>Print verbose debug messages.</summary>
#if DEBUG
        public bool VerboseMessages = true;
#else
        public bool VerboseMessages = false;
#endif
    }
}
