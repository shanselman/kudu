﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Abstractions;
using System.Linq;

namespace Kudu.Core.Infrastructure
{
    internal static class FileSystemHelpers
    {
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Method is used, misdiagnosed due to linking of this file")]
        public static void DeleteDirectorySafe(string path, bool ignoreErrors = true)
        {
            DeleteFileSystemInfo(new DirectoryInfoWrapper(new DirectoryInfo(path)), ignoreErrors);
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Method is used, misdiagnosed due to linking of this file")]
        public static void DeleteDirectoryContentsSafe(string path, bool ignoreErrors = true)
        {
            DeleteDirectoryContentsSafe(new DirectoryInfoWrapper(new DirectoryInfo(path)), ignoreErrors);
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Method is used, misdiagnosed due to linking of this file")]
        public static IEnumerable<string> ListFiles(string path, SearchOption searchOption, params string[] lookupList)
        {
            if (!Directory.Exists(path))
            {
                return Enumerable.Empty<string>();
            }

            // Only lookup of type *.extension or path\file (no *) is supported
            if (lookupList.Any(lookup => lookup.LastIndexOf('*') > 0))
            {
                throw new NotSupportedException("lookup with a '*' that is not the first character is not supported");
            }

            lookupList = lookupList.Select(lookup => lookup.TrimStart('*')).ToArray();

            return Directory.EnumerateFiles(path, "*.*", searchOption)
                            .Where(filePath => lookupList.Any(lookup => filePath.EndsWith(lookup, StringComparison.OrdinalIgnoreCase)));
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Method is used, misdiagnosed due to linking of this file")]
        internal static string EnsureDirectory(string path)
        {
            return EnsureDirectory(new FileSystem(), path);
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Method is used, misdiagnosed due to linking of this file")]
        internal static string EnsureDirectory(IFileSystem fileSystem, string path)
        {
            if (!fileSystem.Directory.Exists(path))
            {
                fileSystem.Directory.CreateDirectory(path);
            }
            return path;
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Method is used, misdiagnosed due to linking of this file")]
        public static bool DeleteFileSafe(string path)
        {
            return DeleteFileSafe(new FileSystem(), path);
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Method is used, misdiagnosed due to linking of this file")]
        public static bool IsSubfolder(string parent, string child)
        {
            // normalize
            string parentPath = Path.GetFullPath(parent).TrimEnd('\\') + '\\';
            string childPath = Path.GetFullPath(child).TrimEnd('\\') + '\\';
            return childPath.StartsWith(parentPath, StringComparison.OrdinalIgnoreCase);
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Method is used, misdiagnosed due to linking of this file")]
        internal static bool DeleteFileSafe(IFileSystem fileSystem, string path)
        {
            try
            {
                if (fileSystem.File.Exists(path))
                {
                    fileSystem.File.Delete(path);
                    return true;
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (FileNotFoundException) { }

            return false;
        }

        // From MSDN: http://msdn.microsoft.com/en-us/library/bb762914.aspx
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Method is used, misdiagnosed due to linking of this file")]
        internal static void CopyDirectoryRecursive(IFileSystem fileSystem, string sourceDirPath, string destinationDirPath, bool overwrite = true)
        {
            // Get the subdirectories for the specified directory.
            var sourceDir = new DirectoryInfo(sourceDirPath);
            DirectoryInfo[] sourceSubDirs = sourceDir.GetDirectories();

            if (!sourceDir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirPath);
            }

            // If the destination directory doesn't exist, create it. 
            if (!fileSystem.Directory.Exists(destinationDirPath))
            {
                fileSystem.Directory.CreateDirectory(destinationDirPath);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] sourceFiles = sourceDir.GetFiles();
            foreach (FileInfo sourceFile in sourceFiles)
            {
                string destinationFilePath = Path.Combine(destinationDirPath, sourceFile.Name);
                fileSystem.File.Copy(sourceFile.FullName, destinationFilePath, overwrite);
            }

            // If copying subdirectories, copy them and their contents to new location. 
            foreach (DirectoryInfo sourceSubDir in sourceSubDirs)
            {
                string destinationSubDirPath = Path.Combine(destinationDirPath, sourceSubDir.Name);
                CopyDirectoryRecursive(fileSystem, sourceSubDir.FullName, destinationSubDirPath);
            }
        }

        private static void DeleteFileSystemInfo(FileSystemInfoBase fileSystemInfo, bool ignoreErrors)
        {
            if (!fileSystemInfo.Exists)
            {
                return;
            }

            try
            {
                fileSystemInfo.Attributes = FileAttributes.Normal;
            }
            catch
            {
                if (!ignoreErrors) throw;
            }

            var directoryInfo = fileSystemInfo as DirectoryInfoBase;

            if (directoryInfo != null)
            {
                DeleteDirectoryContentsSafe(directoryInfo, ignoreErrors);
            }

            DoSafeAction(fileSystemInfo.Delete, ignoreErrors);
        }

        private static void DeleteDirectoryContentsSafe(DirectoryInfoBase directoryInfo, bool ignoreErrors)
        {
            try
            {
                if (directoryInfo.Exists)
                {
                    foreach (var fsi in directoryInfo.GetFileSystemInfos())
                    {
                        DeleteFileSystemInfo(fsi, ignoreErrors);
                    }
                }
            }
            catch
            {
                if (!ignoreErrors) throw;
            }
        }

        private static void DoSafeAction(Action action, bool ignoreErrors)
        {
            try
            {
                OperationManager.Attempt(action);
            }
            catch
            {
                if (!ignoreErrors) throw;
            }
        }
    }
}
