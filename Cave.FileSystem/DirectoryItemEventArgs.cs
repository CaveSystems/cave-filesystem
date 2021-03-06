﻿using System;

namespace Cave
{
    /// <summary>
    /// Provides directory item event arguments.
    /// </summary>
    /// <seealso cref="System.EventArgs" />
    public sealed class DirectoryItemEventArgs : EventArgs
    {
        /// <summary>Gets the directory.</summary>
        /// <value>The directory.</value>
        public DirectoryItem Directory { get; }

        /// <summary>Initializes a new instance of the <see cref="DirectoryItemEventArgs"/> class.</summary>
        /// <param name="dir">The dir.</param>
        public DirectoryItemEventArgs(DirectoryItem dir) => Directory = dir;
    }
}
