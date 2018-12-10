using System;
using System.IO;

namespace Cave.FileSystem
{
    /// <summary>
    /// Provides a directory item. Directories are always rooted!
    /// </summary>
    public sealed class DirectoryItem
    {
        /// <summary>
        /// Obtains a relative path
        /// </summary>
        /// <param name="fullPath">The full path of the file / directory</param>
        /// <param name="basePath">The base path of the file / directory</param>
        /// <returns></returns>
        public static string GetRelative(string fullPath, string basePath)
        {
            if (fullPath == null)
            {
                throw new ArgumentNullException("fullPath");
            }

            if (basePath == null)
            {
                throw new ArgumentNullException("basePath");
            }

            char[] chars = new char[] { '\\', '/' };
            string[] relative = fullPath.Split(chars, StringSplitOptions.RemoveEmptyEntries);
            string[] baseCheck = basePath.Split(chars, StringSplitOptions.RemoveEmptyEntries);
            StringComparison comparison = Platform.IsMicrosoft ? StringComparison.CurrentCultureIgnoreCase : StringComparison.CurrentCulture;
            for (int i = 0; i < baseCheck.Length; i++)
            {
                if (!string.Equals(baseCheck[i], relative[i], comparison))
                {
                    throw new ArgumentException(string.Format("BasePath {0} is not a valid base for FullPath {1}!", basePath, fullPath));
                }
            }
            return "." + Path.DirectorySeparatorChar + string.Join(Path.DirectorySeparatorChar.ToString(), relative, baseCheck.Length, relative.Length - baseCheck.Length);
        }

        /// <summary>
        /// Creates a new directory instance from a specified base path and the full path to the directory. (The
        /// subdirectories will be extracted.)
        /// </summary>
        /// <param name="baseDirectory">The base directory</param>
        /// <param name="fullDirectory">The full path of the directory</param>
        public static DirectoryItem FromFullPath(string baseDirectory, string fullDirectory)
        {
            return new DirectoryItem(baseDirectory, GetRelative(fullDirectory, baseDirectory));
        }

        /// <summary>
        /// Creates a new directory instance from a specified base and sub directory path.
        /// </summary>
        /// <param name="baseDirectory">The base directory</param>
        /// <param name="subDirectory">The subdirectory</param>
        public DirectoryItem(string baseDirectory, string subDirectory)
        {
            BaseDirectory = Path.GetFullPath(baseDirectory);
            Relative = subDirectory;
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
        /// Obtains the full path of the directory
        /// </summary>
        public string FullPath => Path.GetFullPath(Path.Combine(BaseDirectory, Relative));
    }
}
