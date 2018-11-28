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
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Cave.FileSystem
{
    /// <summary>
    /// Provides an asynchronous file finder
    /// </summary>
    public class DirectoryFinder
    {
        bool m_WasStarted;

        /// <summary>
        /// The directory list of already found directories
        /// </summary>
        Queue<DirectoryItem> m_DirectoryList = new Queue<DirectoryItem>();

        /// <summary>
        /// this variable holds the base directory used to start the search
        /// </summary>
        string m_BaseDirectory;

        /// <summary>
        /// provides a directorymask used to filter the directories
        /// </summary>
        string m_DirectoryMask;

        /// <summary>
        /// the comparers used to (un)select a directory
        /// </summary>
        IDirectoryFinderComparer[] m_Comparer;

        /// <summary>
        /// If set to true the finder returns the deepest directory first (e.g. first /tmp/some/dir then /tmp/some).
        /// </summary>
        bool m_DeepestFirst;

        /// <summary>
        /// search currently active
        /// </summary>
        volatile bool m_SearchRunning;

        void m_Recurser(DirectoryItem current)
        {
            try
            {
                foreach (string fullDirectoryName in Directory.GetDirectories(current.FullPath, m_DirectoryMask))
                {
                    DirectoryItem directory = DirectoryItem.FromFullPath(m_BaseDirectory, fullDirectoryName);
                    foreach (IDirectoryFinderComparer comparer in m_Comparer)
                    {
                        if (!comparer.DirectoryMatches(directory))
                        {
                            directory = null;
                            break;
                        }
                    }
                    if (directory != null)
                    {
                        if (m_DeepestFirst)
                        {
                            //recurse first
                            m_Recurser(directory);
                        }
                        //then add to list
                        while (m_SearchRunning)
                        {
                            lock (this)
                            {
                                if (m_DirectoryList.Count < MaximumDirectoriesQueued)
                                {
                                    m_DirectoryList.Enqueue(directory);
                                    break;
                                }
                            }
                            Thread.Sleep(1);
                        }
                        if (!m_DeepestFirst)
                        {
                            //recurse later
                            m_Recurser(directory);
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
        /// runs the current search
        /// </summary>
        void m_SearchDirectories()
        {
            Thread.CurrentThread.Priority = ThreadPriority.Lowest;
            Thread.CurrentThread.IsBackground = true;
            if (VerboseMessages) Trace.TraceError("Starting directory search at {0}", m_BaseDirectory);
            m_Recurser(new DirectoryItem(m_BaseDirectory, "."));
            m_SearchRunning = false;
            if (VerboseMessages) Trace.TraceError("Completed directory search at {0}", m_BaseDirectory);
        }

        /// <summary>
        /// Prepares the directoryfinder with the specified comparers
        /// </summary>
        /// <param name="baseDirectory">the base directory the search starts at</param>
        /// <param name="directoryMask">the directory mask for subdirectories</param>
        /// <param name="comparer">the additionally used comparers</param>
        protected void m_Prepare(string baseDirectory, string directoryMask, params IDirectoryFinderComparer[] comparer)
        {
            if (m_SearchRunning) throw new InvalidOperationException(string.Format("Search is already running!"));
            if (baseDirectory == null) throw new ArgumentNullException("baseDirectory");
            m_BaseDirectory = Path.GetFullPath(baseDirectory);
            if (!Directory.Exists(m_BaseDirectory)) throw new DirectoryNotFoundException();
            m_DirectoryMask = directoryMask;
            m_Comparer = comparer;
            m_WasStarted = false;
        }

        /// <summary>
        /// creates a directoryfinder thread within the specified basedirectory
        /// </summary>
        /// <param name="baseDirectory">the base directory the search starts at</param>
        /// <param name="comparer">the additionally used comparers</param>
        public DirectoryFinder(string baseDirectory, params IDirectoryFinderComparer[] comparer)
        {
            m_Prepare(baseDirectory, "*", comparer);
        }

        /// <summary>
        /// creates a directoryfinder thread within the specified basedirectory
        /// </summary>
        /// <param name="baseDirectory">the base directory the search starts at</param>
        /// <param name="directoryMask">the directory mask to apply</param>
        /// <param name="comparer">the additionally used comparers</param>
        public DirectoryFinder(string baseDirectory, string directoryMask, params IDirectoryFinderComparer[] comparer)
        {
            m_Prepare(baseDirectory, directoryMask, comparer);
        }

        /// <summary>
        /// Retrieves the next file found.
        /// This function waits until a file is found or the search thread completes without finding any further items.
        /// </summary>
        /// <returns>Returns the next file found or null if the finder completed without finding any further files</returns>
        public DirectoryItem GetNext()
        {
            if (!m_SearchRunning && !m_WasStarted)
            {
                m_WasStarted = true;
                m_SearchRunning = true;
                Task.Factory.StartNew(m_SearchDirectories);
            }
            while (m_SearchRunning)
            {
                lock (this)
                {
                    if (m_DirectoryList.Count > 0) return m_DirectoryList.Dequeue();
                }
                Thread.Sleep(1);
            }
            lock (this)
            {
                if (m_DirectoryList.Count > 0) return m_DirectoryList.Dequeue();
            }
            return null;
        }

        /// <summary>
        /// If set to true the finder returns the deepest directory first (e.g. first /tmp/some/dir then /tmp/some).
        /// </summary>
        public bool DeepestFirst
        {
            get
            {
                return m_DeepestFirst;
            }
            set
            {
                if (m_WasStarted) throw new InvalidOperationException(string.Format("Finder was already started!"));
                m_DeepestFirst = value;
            }
        }

        /// <summary>
        /// Obtains the base directory of the search
        /// </summary>
        public string BaseDirectory
        {
            get
            {
                return m_BaseDirectory;
            }
        }

        /// <summary>
        /// Obtains whether the filefinder has completed the search task and all items have been read
        /// </summary>
        public bool Completed
        {
            get
            {
                if (!m_WasStarted) return false;
                if (m_SearchRunning) return false;
                return true;
            }
        }

        /// <summary>
        /// Gets / sets the maximum number of directories in queue
        /// </summary>
        public int MaximumDirectoriesQueued { get; set; } = 20;

        /// <summary>
        /// Closes the finder
        /// </summary>
        public void Close()
        {
            lock (this)
            {
                m_SearchRunning = false;
            }
        }

        /// <summary>Print verbose debug messages</summary>
#if DEBUG
        public bool VerboseMessages = true;
#else
        public bool VerboseMessages = false;
#endif
    }
}
