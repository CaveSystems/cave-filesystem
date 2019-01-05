namespace Cave
{
    /// <summary>
    /// Provides an interface for <see cref="FileFinder"/> file comparer
    /// </summary>
    public interface IDirectoryFinderComparer
    {
        /// <summary>
        /// Determines whether a directory matches the wanted criterias or not
        /// </summary>
        /// <param name="directory"></param>
        /// <returns></returns>
        bool DirectoryMatches(DirectoryItem directory);
    }
}
