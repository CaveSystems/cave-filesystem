using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Cave.FileSystem
{
    /// <summary>
    /// Provides an asynchronous file finder
    /// </summary>
    public sealed class FileFinder : IDisposable
    {
        const string Name = "FileFinder";
        Task m_SearchTask;
        string m_BaseDirectory;
        string m_FileMask;
        string m_DirectoryMask;
        IFileFinderComparer[] m_Comparer;
        bool m_Exit;

        /// <summary>Print verbose debug messages</summary>
#if DEBUG
        public bool VerboseMessages = true;
#else
        public bool VerboseMessages = false;
#endif
        /// <summary>
        /// the maximum number of files in queue
        /// </summary>
        public int MaximumFilesQueued { get; set; }

        /// <summary>
        /// Obtains the number of files seen while searching
        /// </summary>
        public int FilesSeen { get; private set; }

        /// <summary>Gets the files done.</summary>
        /// <value>The files done.</value>
        public int FilesDone { get; private set; }

        /// <summary>
        /// Obtains the number of directories seen while searching
        /// </summary>
        public int DirectoriesSeen { get; private set; }

        /// <summary>Gets the directories done.</summary>
        /// <value>The directories done.</value>
        public int DirectoriesDone { get; private set; }

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
        /// runs the current search
        /// </summary>
        void SearchDirectories()
        {
            Thread.CurrentThread.Priority = ThreadPriority.Lowest;
            Thread.CurrentThread.IsBackground = true;

            Queue<Task> queue = new Queue<Task>();
            DirectoriesSeen = 1;
            {
                Stack<string> directoryWalkerList = new Stack<string>();
                directoryWalkerList.Push(m_BaseDirectory);
                //directoryList.Push(m_BaseDirectory);
                int directoriesDone = 0;
                while (directoryWalkerList.Count > 0)
                {
                    if (m_Exit)
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

                    queue.Enqueue(Task.Factory.StartNew(delegate
                    {
                        SearchFiles(currentDirectory);
                        if (m_Exit)
                        {
                            return;
                        }

                        FoundDirectory?.Invoke(this, new DirectoryItemEventArgs(DirectoryItem.FromFullPath(m_BaseDirectory, currentDirectory)));
                    }));

                    directoriesDone++;
                    try
                    {
                        foreach (string directory in Directory.GetDirectories(currentDirectory, m_DirectoryMask))
                        {
                            if (m_Exit)
                            {
                                break;
                            }

                            DirectoriesSeen++;
                            directoryWalkerList.Push(FileSystem.Combine(m_BaseDirectory, directory));
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
                foreach (string fullFileName in Directory.GetFiles(currentDirectory, m_FileMask))
                {
                    if (m_Exit)
                    {
                        return;
                    }

                    FileItem file = FileItem.FromFullPath(m_BaseDirectory, fullFileName);
                    foreach (IFileFinderComparer comparer in m_Comparer)
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
        /// Prepares the filefinder with the specified comparers
        /// </summary>
        /// <param name="baseDirectory">the base directory the search starts at</param>
        /// <param name="directoryMask">the directory mask for subdirectories</param>
        /// <param name="fileMask">the file mask</param>
        /// <param name="comparer">the additionally used comparers</param>
        void Prepare(string baseDirectory, string directoryMask, string fileMask, params IFileFinderComparer[] comparer)
        {
            if (baseDirectory == null)
            {
                throw new ArgumentNullException("baseDirectory");
            }

            m_BaseDirectory = baseDirectory;
            if (!Directory.Exists(m_BaseDirectory))
            {
                throw new DirectoryNotFoundException();
            }

            m_FileMask = fileMask;
            m_DirectoryMask = directoryMask;
            m_Comparer = comparer;
        }

        /// <summary>
        /// creates a filefinder thread within the specified basedirectory
        /// </summary>
        /// <param name="baseDirectory">the base directory the search starts at</param>
        /// <param name="comparer">the additionally used comparers</param>
        public FileFinder(string baseDirectory, params IFileFinderComparer[] comparer)
        {
            Prepare(baseDirectory, "*", "*", comparer);
        }

        /// <summary>
        /// creates a filefinder thread within the specified basedirectory
        /// </summary>
        /// <param name="baseDirectory"></param>
        /// <param name="directoryMask"></param>
        /// <param name="fileMask"></param>
        /// <param name="comparer"></param>
        public FileFinder(string baseDirectory, string directoryMask, string fileMask, params IFileFinderComparer[] comparer)
        {
            Prepare(baseDirectory, directoryMask, fileMask, comparer);
        }

        /// <summary>Starts the search task.</summary>
        /// <exception cref="InvalidOperationException"></exception>
        public void Start()
        {
            if (m_SearchTask != null)
            {
                throw new InvalidOperationException(string.Format("Search is already running!"));
            }

            m_SearchTask = Task.Factory.StartNew(SearchDirectories);
        }

        /// <summary>Waits for completion of the search task.</summary>
        /// <exception cref="InvalidOperationException"></exception>
        public void Wait()
        {
            if (m_SearchTask == null)
            {
                throw new InvalidOperationException(string.Format("Search was not started!"));
            }

            m_SearchTask.Wait();
        }

        /// <summary>
        /// Obtains the base directory of the search
        /// </summary>
        public string BaseDirectory => m_BaseDirectory;

        /// <summary>
        /// Obtains whether the filefinder has completed the search task and all items have been read
        /// </summary>
        public bool Completed => m_SearchTask.IsCompleted;

        /// <summary>Stops this instance.</summary>
        public void Stop()
        {
            m_Exit = true;
        }

        /// <summary>
        /// Closes the finder
        /// </summary>
        public void Close()
        {
            lock (this)
            {
                m_SearchTask.Wait();
            }
        }

        /// <summary>Gets the name of the log source.</summary>
        /// <value>The name of the log source.</value>
        public string LogSourceName => Name + " " + m_BaseDirectory;

        /// <summary>Releases the unmanaged resources used by this instance and optionally releases the managed resources.</summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "m_FileEvent")]
        void Dispose(bool disposing)
        {
            //base.Dispose(disposing);
            if (disposing)
            {
                m_Exit = true;
                m_SearchTask.Dispose();
            }
        }

        /// <summary>
        /// Releases all resources used by the this instance
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
