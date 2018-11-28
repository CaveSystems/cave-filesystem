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

using Cave.Text;
using System;
using System.IO;

namespace Cave.FileSystem
{
    /// <summary>
    /// Provides access to a file location
    /// </summary>
    public class FileLocation
    {
        /// <summary>Performs an implicit conversion from <see cref="FileLocation"/> to <see cref="string"/>.</summary>
        /// <param name="location">The location.</param>
        /// <returns>The result of the conversion.</returns>
        public static implicit operator string(FileLocation location)
        {
            return location.ToString();
        }

        /// <summary>Gets or sets the root.</summary>
        /// <value>The root.</value>
        public RootLocation Root { get; set; }

        /// <summary>Gets or sets the name of the company.</summary>
        /// <value>The name of the company.</value>
        public string CompanyName { get; set; }

        /// <summary>Gets or sets the name of the file.</summary>
        /// <value>The name of the file.</value>
        public string FileName { get; set; }

        /// <summary>Gets or sets the extension.</summary>
        /// <value>The extension.</value>
        public string Extension { get; set; }

        /// <summary>Gets or sets the sub folder to use.</summary>
        /// <value>The sub folder.</value>
        public string SubFolder { get; set; }

        /// <summary>Initializes a new instance of the <see cref="FileLocation" /> class.</summary>
        /// <param name="root">The root folder. Is unset, this will be set to roaming user.</param>
        /// <param name="companyName">Name of the company. If unset, this will be set to the assemblies company name.</param>
        /// <param name="subFolder">The sub folder.</param>
        /// <param name="fileName">Name of the file. If unset, this will be set to the assemblies product name.</param>
        /// <param name="extension">The extension.</param>
        public FileLocation(RootLocation root = RootLocation.Program, string companyName = null, string subFolder = null, string fileName = null, string extension = null)
        {
            Root = root;
            SubFolder = subFolder;
            Extension = extension;
            switch (Platform.Type)
            {
                case PlatformType.BSD:
                case PlatformType.Linux:
                case PlatformType.UnknownUnix:
                FileName = fileName ?? AssemblyVersionInfo.Program.Product.ToLower().ReplaceInvalidChars(ASCII.Strings.Letters + ASCII.Strings.Digits, "-");
                break;
                default:
                CompanyName = companyName ?? AssemblyVersionInfo.Program.Company.ReplaceChars(Path.GetInvalidPathChars(), "_");
                FileName = fileName ?? AssemblyVersionInfo.Program.Product.ReplaceChars(Path.GetInvalidFileNameChars(), "_");
                break;
            }
        }

        /// <summary>Returns a <see cref="string" /> that represents this instance.</summary>
        /// <returns>A <see cref="string" /> that represents this instance.</returns>
        public override string ToString()
        {
            if (Root == RootLocation.Program)
            {
                return Path.Combine(GetRoot(), Path.Combine(SubFolder, FileName + Extension));
            }
            return Path.Combine(Path.Combine(GetRoot(), CompanyName), Path.Combine(SubFolder, FileName + Extension));
        }

        /// <summary>Gets the folder.</summary>
        /// <value>The folder.</value>
        public string Folder
        {
            get
            {
                if (Root == RootLocation.Program)
                {
                    return Path.Combine(GetRoot(), SubFolder);
                }
                return Path.Combine(GetRoot(), Path.Combine(CompanyName, SubFolder));
            }
        }

        public string ProgramDirectory
        {
            get => Path.GetDirectoryName(MainAssembly.Get().GetAssemblyFilePath());
        }

        private string GetRoot()
        {
            switch (Platform.Type)
            {
                case PlatformType.Android: return GetRootAndroid(Root);
                case PlatformType.Windows:
                case PlatformType.Xbox:
                case PlatformType.CompactFramework: return GetRootWindows(Root);
                default: return GetRootUnix(Root);
            }
        }

        private string GetRootAndroid(RootLocation root)
        {
            switch (root)
            {
                case RootLocation.AllUserConfig:
                case RootLocation.AllUsersData:
                {
                    var path = Path.Combine(ProgramDirectory, ".AllUsers");
                    Directory.CreateDirectory(path);
                    return path;
                }
                case RootLocation.LocalUserConfig:
                case RootLocation.LocalUserData:
                case RootLocation.RoamingUserConfig:
                case RootLocation.RoamingUserData:
                {
                    var path = Path.Combine(ProgramDirectory, ".User_" + Environment.UserName);
                    Directory.CreateDirectory(path);
                    return path;
                }

                case RootLocation.Program: return ProgramDirectory;

                default: throw new ArgumentOutOfRangeException(string.Format("RootLocation {0} unknown", root));
            }
        }

        private string GetRootWindows(RootLocation root)
        {
            switch (root)
            {
                case RootLocation.AllUserConfig:
                case RootLocation.AllUsersData: return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

                case RootLocation.LocalUserConfig:
                case RootLocation.LocalUserData: return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

                case RootLocation.RoamingUserConfig:
                case RootLocation.RoamingUserData: return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

                case RootLocation.Program: return ProgramDirectory;

                default: throw new ArgumentOutOfRangeException(string.Format("RootLocation {0} unknown", root));
            }
        }

        private string GetRootUnix(RootLocation root)
        {
            switch (root)
            {
                case RootLocation.AllUserConfig: return "/etc";
                case RootLocation.AllUsersData: return "/var/lib";

                case RootLocation.LocalUserConfig:
                case RootLocation.LocalUserData: return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

                case RootLocation.RoamingUserConfig:
                case RootLocation.RoamingUserData: return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

                case RootLocation.Program: return ProgramDirectory;

                default: throw new ArgumentOutOfRangeException(string.Format("RootLocation {0} unknown", root));
            }
        }
    }
}