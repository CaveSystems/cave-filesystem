using System;

namespace Cave
{
    /// <summary>
    /// Provides file item event arguments.
    /// </summary>
    /// <seealso cref="System.EventArgs" />
    public class FileItemEventArgs : EventArgs
    {
        /// <summary>Gets the file.</summary>
        /// <value>The file.</value>
        public FileItem File { get; }

        /// <summary>Initializes a new instance of the <see cref="FileItemEventArgs"/> class.</summary>
        /// <param name="file">The file.</param>
        public FileItemEventArgs(FileItem file) => File = file;
    }
}
