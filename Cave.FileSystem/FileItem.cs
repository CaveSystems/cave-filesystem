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
using System.IO;

namespace Cave.FileSystem
{
    /// <summary>
    /// Provides a file path string handler
    /// </summary>
    public sealed class FileItem
    {
        #region static functionality

        /// <summary>
        /// Obtains a relative path
        /// </summary>
        /// <param name="fullPath">The full path of the file / directory</param>
        /// <param name="basePath">The base path of the file / directory</param>
        /// <returns></returns>
        public static string GetRelative(string fullPath, string basePath)
        {
            if (fullPath == null) throw new ArgumentNullException("fullPath");
            if (basePath == null) throw new ArgumentNullException("basePath");

            char[] chars = new char[] { '\\', '/' };
            string[] relative = fullPath.Split(chars, StringSplitOptions.RemoveEmptyEntries);
            string[] baseCheck = basePath.Split(chars, StringSplitOptions.RemoveEmptyEntries);
            StringComparison comparison = Platform.IsMicrosoft ? StringComparison.CurrentCultureIgnoreCase : StringComparison.CurrentCulture;
            for (int i = 0; i < baseCheck.Length; i++)
            {
                if (!string.Equals(baseCheck[i], relative[i], comparison)) throw new ArgumentException(string.Format("BasePath {0} is not a valid base for FullPath {1}!", basePath, fullPath));
            }
            return "." + Path.DirectorySeparatorChar + string.Join(Path.DirectorySeparatorChar.ToString(), relative, baseCheck.Length, relative.Length - baseCheck.Length);
        }


        /// <summary>
        /// Creates a new file instance from a specified base path and the full path to the file. (The
        /// subdirectories will be extracted.)
        /// </summary>
        /// <param name="baseDirectory">The base file</param>
        /// <param name="fullFilePath">The full path of the file</param>
        /// <returns></returns>
        public static FileItem FromFullPath(string baseDirectory, string fullFilePath)
        {
            string relativePath = GetRelative(fullFilePath, baseDirectory);
            return new FileItem(baseDirectory, relativePath);
        }

        /// <summary>
        /// Converts the fileitem to string containing the full path
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static implicit operator string(FileItem file)
        {
            if (file == null) return null;
            return file.FullPath;
        }
        #endregion

        /// <summary>
        /// Creates a new file instance from a specified base and sub directory path and fileName.
        /// </summary>
        /// <param name="baseDirectory">The base directory</param>
        /// <param name="subDirectoryAndName">The subdirectory and name of the file</param>
        public FileItem(string baseDirectory, string subDirectoryAndName)
        {
            BaseDirectory = Path.GetFullPath(baseDirectory);
            Relative = subDirectoryAndName;
            FullPath = Path.GetFullPath(Path.Combine(BaseDirectory, Relative));
        }

        /// <summary>
        /// Obtains the base directory (used when searching for this file)
        /// </summary>
        public string BaseDirectory { get; }

        /// <summary>
        /// Returns the relative path.
        /// </summary>
        public string Relative { get; }

        /// <summary>
        /// Obtains the full path of the file
        /// </summary>
        public string FullPath { get; }

        /// <summary>Gets the extension.</summary>
        /// <value>The extension.</value>
        public string Extension { get { return Path.GetExtension(FullPath); } }

        /// <summary>Gets the file name with extension.</summary>
        /// <value>The file name with extension.</value>
        public string Name { get { return Path.GetFileName(FullPath); } }

        /// <summary>Gets the directory.</summary>
        /// <value>The directory.</value>
        public string Directory
        {
            get
            {
                int i = FullPath.LastIndexOfAny(new char[] { '\\', '/' });
                return FullPath.Substring(0, i);
            }
        }

        /// <summary>
        /// Obtains the full path
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return FullPath;
        }
    }
}
