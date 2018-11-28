#region CopyRight 2018
/*
    Copyright (c) 2003-2018 Andreas Rohleder (andreas@rohleder.cc)
    All rights reserved
*/
#endregion
#region License LGPL-3
/*
    This program/library/sourcecode is free software; you can redistribute it
    and/or modify it under the terms of the GNU Lesser General Public License
    version 3 as published by the Free Software Foundation subsequent called
    the License.

    You may not use this program/library/sourcecode except in compliance
    with the License. The License is included in the LICENSE file
    found at the installation directory or the distribution package.

    Permission is hereby granted, free of charge, to any person obtaining
    a copy of this software and associated documentation files (the
    "Software"), to deal in the Software without restriction, including
    without limitation the rights to use, copy, modify, merge, publish,
    distribute, sublicense, and/or sell copies of the Software, and to
    permit persons to whom the Software is furnished to do so, subject to
    the following conditions:

    The above copyright notice and this permission notice shall be included
    in all copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
    EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
    MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
    NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
    LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
    OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
    WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
#endregion
#region Authors & Contributors
/*
   Author:
     Andreas Rohleder <andreas@rohleder.cc>

   Contributors:

 */
#endregion

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
        string m_BaseDirectory;
        string m_FileMask;
        string m_DirectoryMask;
        IFileFinderComparer[] m_Comparer;
        LinkedList<FileItem> m_FileList = new LinkedList<FileItem>();
        LinkedList<string> m_DirectoryList = new LinkedList<string>();

        void m_Start(string baseDirectory, string directoryMask, string fileMask, params IFileFinderComparer[] comparer)
        {
            if (DirectorySearchRunning || FileSearchRunning) throw new InvalidOperationException(string.Format("Search is already running!"));
            if (baseDirectory == null) throw new ArgumentNullException("baseDirectory");
            m_BaseDirectory = baseDirectory;
            if (!Directory.Exists(m_BaseDirectory)) throw new DirectoryNotFoundException();
            m_FileMask = fileMask;
            m_DirectoryMask = directoryMask;
            m_Comparer = comparer;
            DirectorySearchRunning = true;
            FileSearchRunning = true;
            Task.Factory.StartNew(m_SearchDirectories);
            Task.Factory.StartNew(m_SearchFiles);
        }

        private void m_SearchFiles()
        {
            while (true)
            {
                string currentDir;
                lock (m_DirectoryList)
                {
                    while (m_DirectoryList.Count == 0)
                    {
                        if (!DirectorySearchRunning)
                        {
                            FileSearchRunning = false;
                            return;
                        }
                        Monitor.Wait(m_DirectoryList);
                    }
                    currentDir = m_DirectoryList.First.Value;
                    m_DirectoryList.RemoveFirst();
                }

                foreach (string fileName in Directory.GetFiles(currentDir, m_FileMask))
                {
                    FileItem file = FileItem.FromFullPath(m_BaseDirectory, fileName);
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
                        while (FileSearchRunning)
                        {
                            lock (m_FileList)
                            {
                                if ((MaximumFilesQueued <= 0) || (m_FileList.Count < MaximumFilesQueued))
                                {
                                    FilesSeen++;
                                    m_FileList.AddLast(file);
                                    Monitor.Pulse(m_FileList);
                                    break;
                                }
                                else
                                {
                                    Monitor.Wait(m_FileList);
                                }
                            }
                        }
                    }
                }
            }
            throw new Exception("THIS SHOULD NEVER HAPPEN");
        }

        private void m_SearchDirectories()
        {
            LinkedList<string> list = new LinkedList<string>();
            list.AddFirst(m_BaseDirectory);
            while (list.Count > 0)
            {
                string currentDir = list.First.Value;
                lock (m_DirectoryList)
                {
                    m_DirectoryList.AddLast(currentDir);
                    Monitor.Pulse(m_DirectoryList);
                }
                list.RemoveFirst();
                DirectoriesSeen++;

                foreach (string dir in Directory.GetDirectories(currentDir, m_DirectoryMask))
                {
                    list.AddLast(dir);
                }
            }
            DirectorySearchRunning = false;
        }

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

        /// <summary>Gets a value indicating whether [the directory search is running].</summary>
        /// <value>
        /// <c>true</c> if [directory search is running]; otherwise, <c>false</c>.
        /// </value>
        public bool DirectorySearchRunning { get; private set; }

        /// <summary>Gets a value indicating whether [the file search is running].</summary>
        /// <value><c>true</c> if [file search is running]; otherwise, <c>false</c>.</value>
        public bool FileSearchRunning { get; private set; }

        /// <summary>
        /// Obtains the number of files seen while searching
        /// </summary>
        public int FilesSeen { get; private set; }

        /// <summary>
        /// Obtains the number of directories seen while searching
        /// </summary>
        public int DirectoriesSeen { get; private set; }

        /// <summary>
        /// Obtains the current progress (search and reading)
        /// </summary>
        public float Progress
        {
            get
            {
                if (DirectorySearchRunning) lock (m_DirectoryList) return (m_DirectoryList.Count / DirectoriesSeen) * 0.2f;
                if (FileSearchRunning) lock (m_FileList) return 0.2f + (m_FileList.Count / FilesSeen) * 0.4f;
                lock (m_FileList) return 0.6f + (m_FileList.Count / FilesSeen) * 0.4f;
            }
        }

        /// <summary>
        /// creates a filefinder thread within the specified basedirectory
        /// </summary>
        /// <param name="baseDirectory">the base directory the search starts at</param>
        /// <param name="comparer">the additionally used comparers</param>
        public FileFinder(string baseDirectory, params IFileFinderComparer[] comparer)
        {
            m_Start(baseDirectory, "*", "*", comparer);
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
            m_Start(baseDirectory, directoryMask, fileMask, comparer);
        }

        /// <summary>
        /// Retrieves all files already found. This may called repeatedly until Completed==true.
        /// </summary>
        /// <returns></returns>
        public FileItem[] Get()
        {
            lock (this)
            {
                FileItem[] items = new FileItem[m_FileList.Count];
                m_FileList.CopyTo(items, 0);
                m_FileList.Clear();
                return items;
            }
        }

        /// <summary>
        /// Retrieves all files already found. This may called repeatedly until Completed==true.
        /// </summary>
        /// <returns></returns>
        public FileItem[] Get(int maximum)
        {
            lock (this)
            {
                List<FileItem> result = new List<FileItem>(maximum);
                if (m_FileList.Count < maximum) maximum = m_FileList.Count;
                for (int i = 0; i < maximum; i++)
                {
                    result.Add(m_FileList.First.Value);
                    m_FileList.RemoveFirst();
                }
                return result.ToArray();
            }
        }

        /// <summary>
        /// Retrieves the next file found.
        /// This function waits until a file is found or the search thread completes without finding any further items.
        /// </summary>
        /// <returns>Returns the next file found or null if the finder completed without finding any further files</returns>
        public FileItem GetNext(Action waitAction = null)
        {
            while (FileSearchRunning)
            {
                lock (m_FileList)
                {
                    if (m_FileList.Count > 0)
                    {
                        FileItem result = m_FileList.First.Value;
                        m_FileList.RemoveFirst();
                        return result;
                    }
                    if (waitAction == null) Monitor.Wait(m_FileList);
                }
                waitAction?.Invoke();
            }
            lock (m_FileList)
            {
                if (m_FileList.Count > 0)
                {
                    FileItem result = m_FileList.First.Value;
                    m_FileList.RemoveFirst();
                    return result;
                }
            }
            return null;
        }

        /// <summary>
        /// Obtains the base directory of the search
        /// </summary>
        public string BaseDirectory { get { return m_BaseDirectory; } }

        /// <summary>
        /// Obtains whether the filefinder has completed the search task and all items have been read
        /// </summary>
        public bool Completed
        {
            get
            {
                if (FileSearchRunning) return false;
                return FilesQueued == 0;
            }
        }

        /// <summary>
        /// Obtains the number of queued files
        /// </summary>
        public int FilesQueued { get { lock (m_FileList) return m_FileList.Count; } }


        /// <summary>
        /// Closes the finder
        /// </summary>
        public void Close()
        {
            DirectorySearchRunning = false;
            FileSearchRunning = false;
            lock (m_FileList) { m_FileList.Clear(); Monitor.PulseAll(m_FileList); }
            lock (m_DirectoryList) { m_DirectoryList.Clear(); Monitor.PulseAll(m_DirectoryList); }
        }

        /// <summary>Releases the unmanaged resources used by this instance and optionally releases the managed resources.</summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "m_FileEvent")]
        void Dispose(bool disposing)
        {
            Close();
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
