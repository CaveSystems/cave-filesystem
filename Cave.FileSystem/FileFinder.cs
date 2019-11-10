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
    public sealed class FileFinder : IDisposable
    {
        Task searchTask;
        IFileFinderComparer[] comparer;
        bool exit;

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

        /// <summary>
        /// Gets the number of files seen while searching.
        /// </summary>
        public int FilesSeen { get; private set; }

        /// <summary>Gets the files done.</summary>
        /// <value>The files done.</value>
        public int FilesDone { get; private set; }

        /// <summary>
        /// Gets the number of directories seen while searching.
        /// </summary>
        public int DirectoriesSeen { get; private set; }

        /// <summary>Gets the directories done.</summary>
        /// <value>The directories done.</value>
        public int DirectoriesDone { get; private set; }

        /// <summary>
        /// Gets the file mask applied while searching.
        /// </summary>
        public string FileMask { get; private set; }

        /// <summary>
        /// Gets the directory mask applied while searching.
        /// </summary>
        public string DirectoryMask { get; private set; }

        /// <summary>Gets the file system entries seen.</summary>
        /// <value>The file system entries seen.</value>
        public int Seen => DirectoriesSeen + FilesSeen;

        /// <summary>Gets the file system entries done.</summary>
        /// <value>The file system entries done.</value>
        public int Done => DirectoriesDone + FilesDone;

        /// <summary>The found file event</summary>
        public event EventHandler<FileItemEventArgs> FoundFile;

        /// <summary>The found directory event</summary>
        public event EventHandler<DirectoryItemEventArgs> FoundDirectory;

        /// <summary>
        /// Called on each error
        /// </summary>
        public event EventHandler<ErrorEventArgs> Error;

        /// <summary>
        /// runs the current search.
        /// </summary>
        void SearchDirectories()
        {
            Thread.CurrentThread.Priority = ThreadPriority.Lowest;
            Thread.CurrentThread.IsBackground = true;

            var queue = new Queue<Task>();
            DirectoriesSeen = 1;
            {
                var directoryWalkerList = new Stack<string>();
                directoryWalkerList.Push(BaseDirectory);
                int directoriesDone = 0;
                while (directoryWalkerList.Count > 0)
                {
                    if (exit)
                    {
                        break;
                    }

                    string currentDirectory = directoryWalkerList.Pop();
                    DirectoriesDone++;

                    while (queue.Peek().IsCompleted)
                    {
                        queue.Dequeue();
                    }

                    if (queue.Count > 0)
                    {
                        Task.WaitAny(queue.ToArray());
                    }

                    queue.Enqueue(Task.Factory.StartNew(() =>
                    {
                        SearchFiles(currentDirectory);
                        if (exit)
                        {
                            return;
                        }

                        FoundDirectory?.Invoke(this, new DirectoryItemEventArgs(DirectoryItem.FromFullPath(BaseDirectory, currentDirectory)));
                    }));

                    directoriesDone++;
                    try
                    {
                        foreach (string directory in Directory.GetDirectories(currentDirectory, DirectoryMask))
                        {
                            if (exit)
                            {
                                break;
                            }

                            DirectoriesSeen++;
                            directoryWalkerList.Push(FileSystem.Combine(BaseDirectory, directory));
                        }
                    }
                    catch (Exception ex)
                    {
                        Error?.Invoke(this, new ErrorEventArgs(ex));
                    }
                }
            }
            Task.WaitAll(queue.ToArray());
        }

        void SearchFiles(string currentDirectory)
        {
            try
            {
                foreach (string fullFileName in Directory.GetFiles(currentDirectory, FileMask))
                {
                    if (exit)
                    {
                        return;
                    }

                    var file = FileItem.FromFullPath(BaseDirectory, fullFileName);
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
                        FoundFile?.Invoke(this, new FileItemEventArgs(file));
                    }
                }
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new ErrorEventArgs(ex));
            }
        }

        /// <summary>
        /// Prepares the filefinder with the specified comparers.
        /// </summary>
        /// <param name="baseDirectory">Base directory to start the search at.</param>
        /// <param name="directoryMask">Directory mask to use during search.</param>
        /// <param name="fileMask">File mask to use during search.</param>
        /// <param name="comparer">Comparer to use during search.</param>
        void Prepare(string baseDirectory, string directoryMask, string fileMask, params IFileFinderComparer[] comparer)
        {
            BaseDirectory = baseDirectory ?? throw new ArgumentNullException("baseDirectory");
            if (!Directory.Exists(BaseDirectory))
            {
                throw new DirectoryNotFoundException();
            }

            FileMask = fileMask;
            DirectoryMask = directoryMask;
            this.comparer = comparer;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileFinder"/> class.
        /// </summary>
        /// <param name="baseDirectory">Base directory to start the search at.</param>
        /// <param name="comparer">Comparer to use during search.</param>
        public FileFinder(string baseDirectory, params IFileFinderComparer[] comparer) => Prepare(baseDirectory, "*", "*", comparer);

        /// <summary>
        /// Initializes a new instance of the <see cref="FileFinder"/> class.
        /// </summary>
        /// <param name="baseDirectory">Base directory to start the search at.</param>
        /// <param name="directoryMask">Directory mask to use during search.</param>
        /// <param name="fileMask">File mask to use during search.</param>
        /// <param name="comparer">Comparer to use during search.</param>
        public FileFinder(string baseDirectory, string directoryMask, string fileMask, params IFileFinderComparer[] comparer) => Prepare(baseDirectory, directoryMask, fileMask, comparer);

        /// <summary>Starts the search task.</summary>
        /// <exception cref="InvalidOperationException">Search is already running.</exception>
        public void Start()
        {
            if (searchTask != null)
            {
                throw new InvalidOperationException("Search is already running!");
            }

            searchTask = Task.Factory.StartNew(SearchDirectories);
        }

        /// <summary>Waits for completion of the search task.</summary>
        /// <exception cref="InvalidOperationException">Search was not started!.</exception>
        public void Wait()
        {
            if (searchTask == null)
            {
                throw new InvalidOperationException("Search was not started!");
            }

            searchTask.Wait();
        }

        /// <summary>
        /// Gets the base directory of the search.
        /// </summary>
        public string BaseDirectory { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the filefinder has completed the search task and all items have been read.
        /// </summary>
        public bool Completed => searchTask.IsCompleted;

        /// <summary>Stops this instance.</summary>
        public void Stop() => exit = true;

        /// <summary>
        /// Closes the finder.
        /// </summary>
        public void Close()
        {
            lock (this)
            {
                searchTask.Wait();
            }
        }

        /// <summary>Releases the unmanaged resources used by this instance and optionally releases the managed resources.</summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        void Dispose(bool disposing)
        {
            if (disposing)
            {
                exit = true;
                searchTask.Dispose();
            }
        }

        /// <summary>
        /// Releases all resources used by the this instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
