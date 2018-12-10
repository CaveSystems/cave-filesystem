namespace Cave.FileSystem
{
    /// <summary>
    /// Provides an interface for <see cref="FileFinder"/> file comparer
    /// </summary>
    public interface IFileFinderComparer
    {
        /// <summary>
        /// Determines whether a file matches the wanted criterias or not
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        bool FileMatches(FileItem file);
    }
}
