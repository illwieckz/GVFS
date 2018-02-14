using RGFS.FunctionalTests.FileSystemRunners;
using RGFS.FunctionalTests.Should;
using RGFS.FunctionalTests.Tools;
using RGFS.Tests.Should;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace RGFS.FunctionalTests.Tests.LongRunningEnlistment
{
    [TestFixture]
    public class WorkingDirectoryTests : TestsWithLongRunningEnlistment
    {
        private enum CreationDisposition
        {
            CreateNew = 1,        // CREATE_NEW
            CreateAlways = 2,     // CREATE_ALWAYS    
            OpenExisting = 3,     // OPEN_EXISTING    
            OpenAlways = 4,       // OPEN_ALWAYS      
            TruncateExisting = 5  // TRUNCATE_EXISTING
        }

        [TestCaseSource(typeof(FileRunnersAndFolders), FileRunnersAndFolders.TestRunners)]
        public void ShrinkFileContents(FileSystemRunner fileSystem, string parentFolder)
        {
            string filename = Path.Combine(parentFolder, "ShrinkFileContents");
            string originalVirtualContents = "0123456789";
            fileSystem.WriteAllText(this.Enlistment.GetVirtualPathTo(filename), originalVirtualContents);
            this.Enlistment.GetVirtualPathTo(filename).ShouldBeAFile(fileSystem).WithContents(originalVirtualContents);

            string newText = "112233";
            fileSystem.WriteAllText(this.Enlistment.GetVirtualPathTo(filename), newText);
            this.Enlistment.GetVirtualPathTo(filename).ShouldBeAFile(fileSystem).WithContents(newText);
            fileSystem.DeleteFile(this.Enlistment.GetVirtualPathTo(filename));

            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, filename, parentFolder);
        }

        [TestCaseSource(typeof(FileRunnersAndFolders), FileRunnersAndFolders.TestRunners)]
        public void GrowFileContents(FileSystemRunner fileSystem, string parentFolder)
        {
            string filename = Path.Combine(parentFolder, "GrowFileContents");
            string originalVirtualContents = "112233";
            fileSystem.WriteAllText(this.Enlistment.GetVirtualPathTo(filename), originalVirtualContents);
            this.Enlistment.GetVirtualPathTo(filename).ShouldBeAFile(fileSystem).WithContents(originalVirtualContents);

            string newText = "0123456789";
            fileSystem.WriteAllText(this.Enlistment.GetVirtualPathTo(filename), newText);
            this.Enlistment.GetVirtualPathTo(filename).ShouldBeAFile(fileSystem).WithContents(newText);
            fileSystem.DeleteFile(this.Enlistment.GetVirtualPathTo(filename));

            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, filename, parentFolder);
        }

        [TestCaseSource(typeof(FileRunnersAndFolders), FileRunnersAndFolders.TestRunners)]
        public void FilesAreBufferedAndCanBeFlushed(FileSystemRunner fileSystem, string parentFolder)
        {
            string filename = Path.Combine(parentFolder, "FilesAreBufferedAndCanBeFlushed");
            string filePath = this.Enlistment.GetVirtualPathTo(filename);

            byte[] buffer = System.Text.Encoding.ASCII.GetBytes("Some test data");

            using (FileStream writeStream = File.Open(filePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                writeStream.Write(buffer, 0, buffer.Length);

                using (FileStream readStream = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                    readStream.Length.ShouldEqual(0);
                    writeStream.Flush();
                    readStream.Length.ShouldEqual(buffer.Length);

                    byte[] readBuffer = new byte[buffer.Length];
                    readStream.Read(readBuffer, 0, readBuffer.Length).ShouldEqual(readBuffer.Length);
                    readBuffer.ShouldMatchInOrder(buffer);
                }
            }

            fileSystem.DeleteFile(filePath);
        }

        [TestCaseSource(typeof(FileRunnersAndFolders), FileRunnersAndFolders.TestFolders)]
        public void FileAttributesAreUpdated(string parentFolder)
        {
            string filename = Path.Combine(parentFolder, "FileAttributesAreUpdated");
            FileSystemRunner fileSystem = FileSystemRunner.DefaultRunner;

            string virtualFile = this.Enlistment.GetVirtualPathTo(filename);
            virtualFile.ShouldNotExistOnDisk(fileSystem);

            File.Create(virtualFile).Dispose();
            virtualFile.ShouldBeAFile(fileSystem);

            // Update defaults. FileInfo is not batched, so each of these will create a separate Open-Update-Close set.
            FileInfo before = new FileInfo(virtualFile);
            DateTime testValue = DateTime.Now + TimeSpan.FromDays(1);
            before.CreationTime = testValue;
            before.LastAccessTime = testValue;
            before.LastWriteTime = testValue;
            before.Attributes = FileAttributes.Hidden;

            // FileInfo caches information. We can refresh, but just to be absolutely sure...
            virtualFile.ShouldBeAFile(fileSystem).WithInfo(testValue, testValue, testValue, FileAttributes.Hidden);

            File.Delete(virtualFile);
            virtualFile.ShouldNotExistOnDisk(fileSystem);
        }

        [TestCaseSource(typeof(FileRunnersAndFolders), FileRunnersAndFolders.TestFolders)]
        public void FolderAttributesAreUpdated(string parentFolder)
        {
            string folderName = Path.Combine(parentFolder, "FolderAttributesAreUpdated");
            string virtualFolder = this.Enlistment.GetVirtualPathTo(folderName);
            Directory.CreateDirectory(virtualFolder);

            FileSystemRunner fileSystem = FileSystemRunner.DefaultRunner;

            virtualFolder.ShouldBeADirectory(fileSystem);

            // Update defaults. DirectoryInfo is not batched, so each of these will create a separate Open-Update-Close set.
            DirectoryInfo before = new DirectoryInfo(virtualFolder);
            DateTime testValue = DateTime.Now + TimeSpan.FromDays(1);
            before.CreationTime = testValue;
            before.LastAccessTime = testValue;
            before.LastWriteTime = testValue;
            before.Attributes = FileAttributes.Hidden;

            // DirectoryInfo caches information. We can refresh, but just to be absolutely sure...
            virtualFolder.ShouldBeADirectory(fileSystem)
                .WithInfo(testValue, testValue, testValue, FileAttributes.Hidden | FileAttributes.Directory, ignoreRecallAttributes: false);

            Directory.Delete(virtualFolder);
        }

        [TestCase]
        public void UnhydratedFileAttributesAreUpdated()
        {
            FileSystemRunner fileSystem = FileSystemRunner.DefaultRunner;

            string filename = @"RGFS\RGFS\RGFS.csproj";
            string virtualFile = this.Enlistment.GetVirtualPathTo(filename);

            // Update defaults. FileInfo is not batched, so each of these will create a separate Open-Update-Close set.
            FileInfo before = new FileInfo(virtualFile);
            DateTime testValue = DateTime.Now + TimeSpan.FromDays(1);
            before.CreationTime = testValue;
            before.LastAccessTime = testValue;
            before.LastWriteTime = testValue;
            before.Attributes = FileAttributes.Hidden;

            // FileInfo caches information. We can refresh, but just to be absolutely sure...
            virtualFile.ShouldBeAFile(fileSystem).WithInfo(testValue, testValue, testValue, FileAttributes.Hidden);
        }

        [TestCase]
        public void UnhydratedFolderAttributesAreUpdated()
        {
            FileSystemRunner fileSystem = FileSystemRunner.DefaultRunner;

            string folderName = @"RGFS\RGFS\CommandLine";
            string virtualFolder = this.Enlistment.GetVirtualPathTo(folderName);

            // Update defaults. DirectoryInfo is not batched, so each of these will create a separate Open-Update-Close set.
            DirectoryInfo before = new DirectoryInfo(virtualFolder);
            DateTime testValue = DateTime.Now + TimeSpan.FromDays(1);
            before.CreationTime = testValue;
            before.LastAccessTime = testValue;
            before.LastWriteTime = testValue;
            before.Attributes = FileAttributes.Hidden;

            // DirectoryInfo caches information. We can refresh, but just to be absolutely sure...
            virtualFolder.ShouldBeADirectory(fileSystem)
                .WithInfo(testValue, testValue, testValue, FileAttributes.Hidden | FileAttributes.Directory, ignoreRecallAttributes: true);
        }

        [TestCaseSource(typeof(FileRunnersAndFolders), FileRunnersAndFolders.TestRunners)]
        public void CannotWriteToReadOnlyFile(FileSystemRunner fileSystem, string parentFolder)
        {
            string filename = Path.Combine(parentFolder, "CannotWriteToReadOnlyFile");
            string virtualFilePath = this.Enlistment.GetVirtualPathTo(filename);
            virtualFilePath.ShouldNotExistOnDisk(fileSystem);

            // Write initial contents
            string originalContents = "Contents of ReadOnly file";
            fileSystem.WriteAllText(virtualFilePath, originalContents);
            virtualFilePath.ShouldBeAFile(fileSystem).WithContents(originalContents);

            // Make file read only
            FileInfo fileInfo = new FileInfo(virtualFilePath);
            fileInfo.Attributes = FileAttributes.ReadOnly;

            // Verify that file cannot be written to
            string newContents = "New contents for file";
            fileSystem.WriteAllTextShouldFail<UnauthorizedAccessException>(virtualFilePath, newContents);
            virtualFilePath.ShouldBeAFile(fileSystem).WithContents(originalContents);

            // Cleanup
            fileInfo.Attributes = FileAttributes.Normal;
            fileSystem.DeleteFile(virtualFilePath);
            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, filename, parentFolder);
        }

        [TestCaseSource(typeof(FileRunnersAndFolders), FileRunnersAndFolders.TestRunners)]
        public void ReadonlyCanBeSetAndUnset(FileSystemRunner fileSystem, string parentFolder)
        {
            string filename = Path.Combine(parentFolder, "ReadonlyCanBeSetAndUnset");
            string virtualFilePath = this.Enlistment.GetVirtualPathTo(filename);
            virtualFilePath.ShouldNotExistOnDisk(fileSystem);

            string originalContents = "Contents of ReadOnly file";
            fileSystem.WriteAllText(virtualFilePath, originalContents);

            // Make file read only
            FileInfo fileInfo = new FileInfo(virtualFilePath);
            fileInfo.Attributes = FileAttributes.ReadOnly;
            virtualFilePath.ShouldBeAFile(fileSystem).WithAttribute(FileAttributes.ReadOnly);
            
            // Clear read only
            fileInfo.Attributes = FileAttributes.Normal;
            virtualFilePath.ShouldBeAFile(fileSystem).WithoutAttribute(FileAttributes.ReadOnly);

            // Cleanup
            fileSystem.DeleteFile(virtualFilePath);
            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, filename, parentFolder);
        }

        [TestCaseSource(typeof(FileRunnersAndFolders), FileRunnersAndFolders.TestRunners)]
        public void ChangeVirtualNTFSFileNameCase(FileSystemRunner fileSystem, string parentFolder)
        {
            string oldFilename = Path.Combine(parentFolder, "ChangePhysicalFileNameCase.txt");
            string newFilename = Path.Combine(parentFolder, "changephysicalfilenamecase.txt");
            string fileContents = "Hello World";
            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, oldFilename, parentFolder);

            fileSystem.WriteAllText(this.Enlistment.GetVirtualPathTo(oldFilename), fileContents);
            this.Enlistment.GetVirtualPathTo(oldFilename).ShouldBeAFile(fileSystem).WithContents(fileContents);
            this.Enlistment.GetVirtualPathTo(oldFilename).ShouldBeAFile(fileSystem).WithCaseMatchingName(Path.GetFileName(oldFilename));

            fileSystem.MoveFile(this.Enlistment.GetVirtualPathTo(oldFilename), this.Enlistment.GetVirtualPathTo(newFilename));
            this.Enlistment.GetVirtualPathTo(newFilename).ShouldBeAFile(fileSystem).WithContents(fileContents);
            this.Enlistment.GetVirtualPathTo(newFilename).ShouldBeAFile(fileSystem).WithCaseMatchingName(Path.GetFileName(newFilename));

            fileSystem.DeleteFile(this.Enlistment.GetVirtualPathTo(newFilename));

            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, newFilename, parentFolder);
        }

        [TestCaseSource(typeof(FileRunnersAndFolders), FileRunnersAndFolders.TestRunners)]
        public void ChangeVirtualNTFSFileName(FileSystemRunner fileSystem, string parentFolder)
        {
            string oldFilename = Path.Combine(parentFolder, "ChangePhysicalFileName.txt");
            string newFilename = Path.Combine(parentFolder, "NewFileName.txt");
            string fileContents = "Hello World";
            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, oldFilename, parentFolder);
            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, newFilename, parentFolder);

            fileSystem.WriteAllText(this.Enlistment.GetVirtualPathTo(oldFilename), fileContents);
            this.Enlistment.GetVirtualPathTo(oldFilename).ShouldBeAFile(fileSystem).WithContents(fileContents);
            this.Enlistment.GetVirtualPathTo(newFilename).ShouldNotExistOnDisk(fileSystem);

            fileSystem.MoveFile(this.Enlistment.GetVirtualPathTo(oldFilename), this.Enlistment.GetVirtualPathTo(newFilename));
            this.Enlistment.GetVirtualPathTo(newFilename).ShouldBeAFile(fileSystem).WithContents(fileContents);
            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, oldFilename, parentFolder);

            fileSystem.DeleteFile(this.Enlistment.GetVirtualPathTo(newFilename));
            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, newFilename, parentFolder);
        }

        [TestCaseSource(typeof(FileRunnersAndFolders), FileRunnersAndFolders.TestRunners)]
        public void MoveVirtualNTFSFileToVirtualNTFSFolder(FileSystemRunner fileSystem, string parentFolder)
        {
            string testFolderName = Path.Combine(parentFolder, "test_folder");
            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, testFolderName, parentFolder);

            fileSystem.CreateDirectory(this.Enlistment.GetVirtualPathTo(testFolderName));
            this.Enlistment.GetVirtualPathTo(testFolderName).ShouldBeADirectory(fileSystem);

            string testFileName = Path.Combine(parentFolder, "test.txt");
            string testFileContents = "This is the contents of a test file";
            fileSystem.WriteAllText(this.Enlistment.GetVirtualPathTo(testFileName), testFileContents);
            this.Enlistment.GetVirtualPathTo(testFileName).ShouldBeAFile(fileSystem).WithContents(testFileContents);

            string newTestFileVirtualPath = Path.Combine(
                this.Enlistment.GetVirtualPathTo(testFolderName),
                Path.GetFileName(testFileName));

            fileSystem.MoveFile(this.Enlistment.GetVirtualPathTo(testFileName), newTestFileVirtualPath);

            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, testFileName, parentFolder);
            newTestFileVirtualPath.ShouldBeAFile(fileSystem).WithContents(testFileContents);

            fileSystem.DeleteFile(newTestFileVirtualPath);
            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, Path.Combine(testFolderName, Path.GetFileName(testFileName)), parentFolder);

            fileSystem.DeleteDirectory(this.Enlistment.GetVirtualPathTo(testFolderName));
            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, testFolderName, parentFolder);
        }

        [TestCaseSource(typeof(FileSystemRunner), FileSystemRunner.TestRunners)]
        public void MoveWorkingDirectoryFileToDotGitFolder(FileSystemRunner fileSystem)
        {
            string testFolderName = ".git";
            this.Enlistment.GetVirtualPathTo(testFolderName).ShouldBeADirectory(fileSystem);

            string testFileName = "test.txt";
            this.Enlistment.GetVirtualPathTo(testFileName).ShouldNotExistOnDisk(fileSystem);

            string testFileContents = "This is the contents of a test file";
            fileSystem.WriteAllText(this.Enlistment.GetVirtualPathTo(testFileName), testFileContents);
            this.Enlistment.GetVirtualPathTo(testFileName).ShouldBeAFile(fileSystem).WithContents(testFileContents);

            string newTestFileVirtualPath = Path.Combine(this.Enlistment.GetVirtualPathTo(testFolderName), testFileName);
            fileSystem.MoveFile(this.Enlistment.GetVirtualPathTo(testFileName), newTestFileVirtualPath);
            this.Enlistment.GetVirtualPathTo(testFileName).ShouldNotExistOnDisk(fileSystem);
            newTestFileVirtualPath.ShouldBeAFile(fileSystem).WithContents(testFileContents);

            fileSystem.DeleteFile(newTestFileVirtualPath);
            newTestFileVirtualPath.ShouldNotExistOnDisk(fileSystem);
        }

        [TestCaseSource(typeof(FileSystemRunner), FileSystemRunner.TestRunners)]
        public void MoveDotGitFileToWorkingDirectoryFolder(FileSystemRunner fileSystem)
        {
            string testFolderName = "test_folder";
            this.Enlistment.GetVirtualPathTo(testFolderName).ShouldNotExistOnDisk(fileSystem);

            fileSystem.CreateDirectory(this.Enlistment.GetVirtualPathTo(testFolderName));
            this.Enlistment.GetVirtualPathTo(testFolderName).ShouldBeADirectory(fileSystem);

            string sourceFileFolder = ".git";
            string testFileName = "config";
            string sourceFileVirtualPath = Path.Combine(this.Enlistment.GetVirtualPathTo(sourceFileFolder), testFileName);
            string testFileContents = sourceFileVirtualPath.ShouldBeAFile(fileSystem).WithContents();

            string targetTestFileVirtualPath = Path.Combine(this.Enlistment.GetVirtualPathTo(testFolderName), testFileName);

            fileSystem.MoveFile(sourceFileVirtualPath, targetTestFileVirtualPath);
            sourceFileVirtualPath.ShouldNotExistOnDisk(fileSystem);

            targetTestFileVirtualPath.ShouldBeAFile(fileSystem).WithContents(testFileContents);

            fileSystem.MoveFile(targetTestFileVirtualPath, sourceFileVirtualPath);
            sourceFileVirtualPath.ShouldBeAFile(fileSystem).WithContents(testFileContents);
            targetTestFileVirtualPath.ShouldNotExistOnDisk(fileSystem);

            fileSystem.DeleteDirectory(this.Enlistment.GetVirtualPathTo(testFolderName));
            this.Enlistment.GetVirtualPathTo(testFolderName).ShouldNotExistOnDisk(fileSystem);
        }

        [TestCaseSource(typeof(FileRunnersAndFolders), FileRunnersAndFolders.TestRunners)]
        public void MoveVirtualNTFSFileToOverwriteVirtualNTFSFile(FileSystemRunner fileSystem, string parentFolder)
        {
            string targetFilename = Path.Combine(parentFolder, "TargetFile.txt");
            string sourceFilename = Path.Combine(parentFolder, "SourceFile.txt");
            string targetFileContents = "The Target";
            string sourceFileContents = "The Source";
            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, targetFilename, parentFolder);
            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, sourceFilename, parentFolder);

            fileSystem.WriteAllText(this.Enlistment.GetVirtualPathTo(targetFilename), targetFileContents);
            this.Enlistment.GetVirtualPathTo(targetFilename).ShouldBeAFile(fileSystem).WithContents(targetFileContents);

            fileSystem.WriteAllText(this.Enlistment.GetVirtualPathTo(sourceFilename), sourceFileContents);
            this.Enlistment.GetVirtualPathTo(sourceFilename).ShouldBeAFile(fileSystem).WithContents(sourceFileContents);

            fileSystem.ReplaceFile(this.Enlistment.GetVirtualPathTo(sourceFilename), this.Enlistment.GetVirtualPathTo(targetFilename));

            this.Enlistment.GetVirtualPathTo(targetFilename).ShouldBeAFile(fileSystem).WithContents(sourceFileContents);
            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, sourceFilename, parentFolder);

            fileSystem.DeleteFile(this.Enlistment.GetVirtualPathTo(targetFilename));

            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, targetFilename, parentFolder);
        }

        [TestCaseSource(typeof(FileRunnersAndFolders), FileRunnersAndFolders.TestRunners)]
        public void MoveVirtualNTFSFileToInvalidFolder(FileSystemRunner fileSystem, string parentFolder)
        {
            string testFolderName = Path.Combine(parentFolder, "test_folder");
            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, testFolderName, parentFolder);

            string testFileName = Path.Combine(parentFolder, "test.txt");
            string testFileContents = "This is the contents of a test file";
            fileSystem.WriteAllText(this.Enlistment.GetVirtualPathTo(testFileName), testFileContents);
            this.Enlistment.GetVirtualPathTo(testFileName).ShouldBeAFile(fileSystem).WithContents(testFileContents);

            string newTestFileVirtualPath = Path.Combine(
                this.Enlistment.GetVirtualPathTo(testFolderName),
                Path.GetFileName(testFileName));

            fileSystem.MoveFileShouldFail(this.Enlistment.GetVirtualPathTo(testFileName), newTestFileVirtualPath);
            newTestFileVirtualPath.ShouldNotExistOnDisk(fileSystem);

            this.Enlistment.GetVirtualPathTo(testFileName).ShouldBeAFile(fileSystem).WithContents(testFileContents);

            fileSystem.DeleteFile(this.Enlistment.GetVirtualPathTo(testFileName));
            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, testFileName, parentFolder);
        }

        [TestCaseSource(typeof(FileRunnersAndFolders), FileRunnersAndFolders.TestRunners)]
        public void DeletedFilesCanBeImmediatelyRecreated(FileSystemRunner fileSystem, string parentFolder)
        {
            string filename = Path.Combine(parentFolder, "DeletedFilesCanBeImmediatelyRecreated");
            string filePath = this.Enlistment.GetVirtualPathTo(filename);
            filePath.ShouldNotExistOnDisk(fileSystem);

            string testData = "Some test data";

            fileSystem.WriteAllText(filePath, testData);

            fileSystem.DeleteFile(filePath);

            // Do not check for delete. Doing so removes a race between deleting and writing.
            // This write will throw if the problem exists.
            fileSystem.WriteAllText(filePath, testData);

            filePath.ShouldBeAFile(fileSystem).WithContents().ShouldEqual(testData);
            fileSystem.DeleteFile(filePath);
        }

        [TestCaseSource(typeof(FileRunnersAndFolders), FileRunnersAndFolders.TestCanDeleteFilesWhileTheyAreOpenRunners)]
        public void CanDeleteFilesWhileTheyAreOpen(FileSystemRunner fileSystem, string parentFolder)
        {
            string filename = Path.Combine(parentFolder, "CanDeleteFilesWhileTheyAreOpen");
            string filePath = this.Enlistment.GetVirtualPathTo(filename);

            byte[] buffer = System.Text.Encoding.ASCII.GetBytes("Some test data for writing");

            using (FileStream deletableWriteStream = File.Open(filePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
            {
                deletableWriteStream.Write(buffer, 0, buffer.Length);
                deletableWriteStream.Flush();

                using (FileStream deletableReadStream = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
                {
                    byte[] readBuffer = new byte[buffer.Length];

                    deletableReadStream.Read(readBuffer, 0, readBuffer.Length).ShouldEqual(readBuffer.Length);
                    readBuffer.ShouldMatchInOrder(buffer);

                    fileSystem.DeleteFile(filePath);
                    filePath.ShouldBeAFile(fileSystem);

                    deletableWriteStream.Write(buffer, 0, buffer.Length);
                    deletableWriteStream.Flush();
                }
            }

            filePath.ShouldNotExistOnDisk(fileSystem);
        }

        [TestCase]
        public void CanDeleteHydratedFilesWhileTheyAreOpenForWrite()
        {
            FileSystemRunner fileSystem = FileSystemRunner.DefaultRunner;
            string fileName = "RGFS.sln";
            string virtualPath = this.Enlistment.GetVirtualPathTo(fileName);

            virtualPath.ShouldBeAFile(fileSystem);

            using (Stream stream = new FileStream(virtualPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
            using (StreamReader reader = new StreamReader(stream))
            {
                // First line is empty, so read two lines
                string line = reader.ReadLine() + reader.ReadLine();
                line.Length.ShouldNotEqual(0);

                File.Delete(virtualPath);

                // Open deleted files should still exist
                virtualPath.ShouldBeAFile(fileSystem);

                using (StreamWriter writer = new StreamWriter(stream))
                {
                    writer.WriteLine("newline!");
                    writer.Flush();

                    virtualPath.ShouldBeAFile(fileSystem);
                }
            }

            virtualPath.ShouldNotExistOnDisk(fileSystem);
        }

        [TestCase]
        public void ProjectedBlobFileTimesMatchHead()
        {
            // TODO: 467539 - Update all runners to support getting create/modify/access times
            FileSystemRunner fileSystem = FileSystemRunner.DefaultRunner;

            string filename = "AuthoringTests.md";
            string headFileName = ".git\\logs\\HEAD";
            this.Enlistment.GetVirtualPathTo(headFileName).ShouldBeAFile(fileSystem);

            FileInfo headFileInfo = new FileInfo(this.Enlistment.GetVirtualPathTo(headFileName));
            FileInfo fileInfo = new FileInfo(this.Enlistment.GetVirtualPathTo(filename));

            fileInfo.CreationTime.ShouldEqual(headFileInfo.CreationTime);

            // Last access and last write can get set outside the test, make sure that are at least
            // as recent as the creation time on the HEAD file, and no later than now
            fileInfo.LastAccessTime.ShouldBeAtLeast(headFileInfo.CreationTime);
            fileInfo.LastWriteTime.ShouldBeAtLeast(headFileInfo.CreationTime);
            fileInfo.LastAccessTime.ShouldBeAtMost(DateTime.Now);
            fileInfo.LastWriteTime.ShouldBeAtMost(DateTime.Now);
        }

        [TestCase]
        public void ProjectedBlobFolderTimesMatchHead()
        {
            // TODO: 467539 - Update all runners to support getting create/modify/access times
            FileSystemRunner fileSystem = FileSystemRunner.DefaultRunner;

            string folderName = @"RGFS\RGFS.Tests";
            string headFileName = ".git\\logs\\HEAD";
            this.Enlistment.GetVirtualPathTo(headFileName).ShouldBeAFile(fileSystem);

            FileInfo headFileInfo = new FileInfo(this.Enlistment.GetVirtualPathTo(headFileName));
            DirectoryInfo folderInfo = new DirectoryInfo(this.Enlistment.GetVirtualPathTo(folderName));

            folderInfo.CreationTime.ShouldEqual(headFileInfo.CreationTime);

            // Last access and last write can get set outside the test, make sure that are at least
            // as recent as the creation time on the HEAD file, and no later than now
            folderInfo.LastAccessTime.ShouldBeAtLeast(headFileInfo.CreationTime);
            folderInfo.LastWriteTime.ShouldBeAtLeast(headFileInfo.CreationTime);
            folderInfo.LastAccessTime.ShouldBeAtMost(DateTime.Now);
            folderInfo.LastWriteTime.ShouldBeAtMost(DateTime.Now);
        }

        [TestCaseSource(typeof(FileRunnersAndFolders), FileRunnersAndFolders.TestRunners)]
        public void NonExistentItemBehaviorIsCorrect(FileSystemRunner fileSystem, string parentFolder)
        {
            string nonExistentItem = Path.Combine(parentFolder, "BadFolderName");
            string nonExistentItem2 = Path.Combine(parentFolder, "BadFolderName2");

            string virtualPathToNonExistentItem = this.Enlistment.GetVirtualPathTo(nonExistentItem).ShouldNotExistOnDisk(fileSystem);
            string virtualPathToNonExistentItem2 = this.Enlistment.GetVirtualPathTo(nonExistentItem2).ShouldNotExistOnDisk(fileSystem);

            fileSystem.MoveFile_FileShouldNotBeFound(virtualPathToNonExistentItem, virtualPathToNonExistentItem2);
            fileSystem.DeleteFile_FileShouldNotBeFound(virtualPathToNonExistentItem);
            fileSystem.ReplaceFile_FileShouldNotBeFound(virtualPathToNonExistentItem, virtualPathToNonExistentItem2);
            fileSystem.ReadAllText_FileShouldNotBeFound(virtualPathToNonExistentItem);

            // TODO #457434
            // fileSystem.MoveDirectoryShouldNotBeFound(nonExistentItem, true)
            fileSystem.DeleteDirectory_DirectoryShouldNotBeFound(virtualPathToNonExistentItem);

            // TODO #457434
            // fileSystem.ReplaceDirectoryShouldNotBeFound(nonExistentItem, true)
        }

        [TestCaseSource(typeof(FileRunnersAndFolders), FileRunnersAndFolders.TestRunners)]
        public void RenameEmptyVirtualNTFSFolder(FileSystemRunner fileSystem, string parentFolder)
        {
            string testFolderName = Path.Combine(parentFolder, "test_folder");
            string testFolderVirtualPath = this.Enlistment.GetVirtualPathTo(testFolderName);
            testFolderVirtualPath.ShouldNotExistOnDisk(fileSystem);

            fileSystem.CreateDirectory(testFolderVirtualPath);
            testFolderVirtualPath.ShouldBeADirectory(fileSystem);

            string newFolderName = Path.Combine(parentFolder, "test_folder_renamed");
            string newFolderVirtualPath = this.Enlistment.GetVirtualPathTo(newFolderName);
            newFolderVirtualPath.ShouldNotExistOnDisk(fileSystem);

            fileSystem.MoveDirectory(testFolderVirtualPath, newFolderVirtualPath);
            testFolderVirtualPath.ShouldNotExistOnDisk(fileSystem);
            newFolderVirtualPath.ShouldBeADirectory(fileSystem);

            fileSystem.DeleteDirectory(newFolderVirtualPath);
            newFolderVirtualPath.ShouldNotExistOnDisk(fileSystem);
        }

        [TestCaseSource(typeof(FileRunnersAndFolders), FileRunnersAndFolders.TestRunners)]
        public void CaseOnlyRenameEmptyVirtualNTFSFolder(FileSystemRunner fileSystem, string parentFolder)
        {
            string testFolderName = Path.Combine(parentFolder, "test_folder");
            string testFolderVirtualPath = this.Enlistment.GetVirtualPathTo(testFolderName);
            testFolderVirtualPath.ShouldNotExistOnDisk(fileSystem);

            fileSystem.CreateDirectory(testFolderVirtualPath);
            testFolderVirtualPath.ShouldBeADirectory(fileSystem);

            string newFolderName = Path.Combine(parentFolder, "test_FOLDER");
            string newFolderVirtualPath = this.Enlistment.GetVirtualPathTo(newFolderName);

            // Use NativeMethods.MoveFile instead of the runner because it supports case only rename
            NativeMethods.MoveFile(testFolderVirtualPath, newFolderVirtualPath);

            newFolderVirtualPath.ShouldBeADirectory(fileSystem).WithCaseMatchingName(Path.GetFileName(newFolderName));

            fileSystem.DeleteDirectory(newFolderVirtualPath);
            newFolderVirtualPath.ShouldNotExistOnDisk(fileSystem);
        }

        [TestCaseSource(typeof(FileSystemRunner), FileSystemRunner.TestRunners)]
        public void CaseOnlyRenameToAllCapsEmptyVirtualNTFSFolder(FileSystemRunner fileSystem)
        {
            string testFolderName = Path.Combine("test_folder");
            string testFolderVirtualPath = this.Enlistment.GetVirtualPathTo(testFolderName);
            testFolderVirtualPath.ShouldNotExistOnDisk(fileSystem);

            fileSystem.CreateDirectory(testFolderVirtualPath);
            testFolderVirtualPath.ShouldBeADirectory(fileSystem);

            string newFolderName = Path.Combine("TEST_FOLDER");
            string newFolderVirtualPath = this.Enlistment.GetVirtualPathTo(newFolderName);

            // Use NativeMethods.MoveFile instead of the runner because it supports case only rename
            NativeMethods.MoveFile(testFolderVirtualPath, newFolderVirtualPath);

            newFolderVirtualPath.ShouldBeADirectory(fileSystem).WithCaseMatchingName(Path.GetFileName(newFolderName));

            fileSystem.DeleteDirectory(newFolderVirtualPath);
            newFolderVirtualPath.ShouldNotExistOnDisk(fileSystem);
        }

        [TestCaseSource(typeof(FileSystemRunner), FileSystemRunner.TestRunners)]
        public void CaseOnlyRenameTopOfVirtualNTFSFolderTree(FileSystemRunner fileSystem)
        {
            string testFolderParent = "test_folder_parent";
            string testFolderChild = "test_folder_child";
            string testFolderGrandChild = "test_folder_grandchild";
            string testFile = "test.txt";
            this.Enlistment.GetVirtualPathTo(testFolderParent).ShouldNotExistOnDisk(fileSystem);

            // Create the folder tree
            fileSystem.CreateDirectory(this.Enlistment.GetVirtualPathTo(testFolderParent));
            this.Enlistment.GetVirtualPathTo(testFolderParent).ShouldBeADirectory(fileSystem);

            string realtiveChildFolderPath = Path.Combine(testFolderParent, testFolderChild);
            fileSystem.CreateDirectory(this.Enlistment.GetVirtualPathTo(realtiveChildFolderPath));
            this.Enlistment.GetVirtualPathTo(realtiveChildFolderPath).ShouldBeADirectory(fileSystem);

            string realtiveGrandChildFolderPath = Path.Combine(realtiveChildFolderPath, testFolderGrandChild);
            fileSystem.CreateDirectory(this.Enlistment.GetVirtualPathTo(realtiveGrandChildFolderPath));
            this.Enlistment.GetVirtualPathTo(realtiveGrandChildFolderPath).ShouldBeADirectory(fileSystem);

            string relativeTestFilePath = Path.Combine(realtiveGrandChildFolderPath, testFile);
            string testFileContents = "This is the contents of a test file";
            fileSystem.WriteAllText(this.Enlistment.GetVirtualPathTo(relativeTestFilePath), testFileContents);
            this.Enlistment.GetVirtualPathTo(relativeTestFilePath).ShouldBeAFile(fileSystem).WithContents(testFileContents);

            string newFolderParentName = "test_FOLDER_PARENT";

            // Use NativeMethods.MoveFile instead of the runner because it supports case only rename
            NativeMethods.MoveFile(this.Enlistment.GetVirtualPathTo(testFolderParent), this.Enlistment.GetVirtualPathTo(newFolderParentName));

            this.Enlistment.GetVirtualPathTo(newFolderParentName).ShouldBeADirectory(fileSystem).WithCaseMatchingName(newFolderParentName);
            this.Enlistment.GetVirtualPathTo(realtiveChildFolderPath).ShouldBeADirectory(fileSystem);
            this.Enlistment.GetVirtualPathTo(realtiveGrandChildFolderPath).ShouldBeADirectory(fileSystem);
            this.Enlistment.GetVirtualPathTo(relativeTestFilePath).ShouldBeAFile(fileSystem).WithContents(testFileContents);

            // Cleanup
            fileSystem.DeleteDirectory(this.Enlistment.GetVirtualPathTo(testFolderParent));

            this.Enlistment.GetVirtualPathTo(testFolderParent).ShouldNotExistOnDisk(fileSystem);
            this.Enlistment.GetVirtualPathTo(realtiveChildFolderPath).ShouldNotExistOnDisk(fileSystem);
            this.Enlistment.GetVirtualPathTo(realtiveGrandChildFolderPath).ShouldNotExistOnDisk(fileSystem);
            this.Enlistment.GetVirtualPathTo(relativeTestFilePath).ShouldNotExistOnDisk(fileSystem);
        }

        [TestCaseSource(typeof(FileSystemRunner), FileSystemRunner.TestRunners)]
        public void CaseOnlyRenameFullDotGitFolder(FileSystemRunner fileSystem)
        {
            string testFolderName = ".git\\test_folder";
            string testFolderVirtualPath = this.Enlistment.GetVirtualPathTo(testFolderName);
            testFolderVirtualPath.ShouldNotExistOnDisk(fileSystem);

            fileSystem.CreateDirectory(testFolderVirtualPath);
            testFolderVirtualPath.ShouldBeADirectory(fileSystem);

            string newFolderName = "test_FOLDER";
            string newFolderVirtualPath = this.Enlistment.GetVirtualPathTo(Path.Combine(".git", newFolderName));

            // Use NativeMethods.MoveFile instead of the runner because it supports case only rename
            NativeMethods.MoveFile(testFolderVirtualPath, newFolderVirtualPath);

            newFolderVirtualPath.ShouldBeADirectory(fileSystem).WithCaseMatchingName(newFolderName);

            fileSystem.DeleteDirectory(newFolderVirtualPath);
            newFolderVirtualPath.ShouldNotExistOnDisk(fileSystem);
        }

        [TestCaseSource(typeof(FileSystemRunner), FileSystemRunner.TestRunners)]
        public void CaseOnlyRenameTopOfDotGitFullFolderTree(FileSystemRunner fileSystem)
        {
            string testFolderParent = ".git\\test_folder_parent";
            string testFolderChild = "test_folder_child";
            string testFolderGrandChild = "test_folder_grandchild";
            string testFile = "test.txt";
            this.Enlistment.GetVirtualPathTo(testFolderParent).ShouldNotExistOnDisk(fileSystem);

            // Create the folder tree
            fileSystem.CreateDirectory(this.Enlistment.GetVirtualPathTo(testFolderParent));
            this.Enlistment.GetVirtualPathTo(testFolderParent).ShouldBeADirectory(fileSystem);

            string realtiveChildFolderPath = Path.Combine(testFolderParent, testFolderChild);
            fileSystem.CreateDirectory(this.Enlistment.GetVirtualPathTo(realtiveChildFolderPath));
            this.Enlistment.GetVirtualPathTo(realtiveChildFolderPath).ShouldBeADirectory(fileSystem);

            string realtiveGrandChildFolderPath = Path.Combine(realtiveChildFolderPath, testFolderGrandChild);
            fileSystem.CreateDirectory(this.Enlistment.GetVirtualPathTo(realtiveGrandChildFolderPath));
            this.Enlistment.GetVirtualPathTo(realtiveGrandChildFolderPath).ShouldBeADirectory(fileSystem);

            string relativeTestFilePath = Path.Combine(realtiveGrandChildFolderPath, testFile);
            string testFileContents = "This is the contents of a test file";
            fileSystem.WriteAllText(this.Enlistment.GetVirtualPathTo(relativeTestFilePath), testFileContents);
            this.Enlistment.GetVirtualPathTo(relativeTestFilePath).ShouldBeAFile(fileSystem).WithContents(testFileContents);

            string newFolderParentName = "test_FOLDER_PARENT";

            // Use NativeMethods.MoveFile instead of the runner because it supports case only rename
            NativeMethods.MoveFile(this.Enlistment.GetVirtualPathTo(testFolderParent), this.Enlistment.GetVirtualPathTo(Path.Combine(".git", newFolderParentName)));

            this.Enlistment.GetVirtualPathTo(Path.Combine(".git", newFolderParentName)).ShouldBeADirectory(fileSystem).WithCaseMatchingName(newFolderParentName);
            this.Enlistment.GetVirtualPathTo(realtiveChildFolderPath).ShouldBeADirectory(fileSystem);
            this.Enlistment.GetVirtualPathTo(realtiveGrandChildFolderPath).ShouldBeADirectory(fileSystem);
            this.Enlistment.GetVirtualPathTo(relativeTestFilePath).ShouldBeAFile(fileSystem).WithContents(testFileContents);

            // Cleanup
            fileSystem.DeleteDirectory(this.Enlistment.GetVirtualPathTo(Path.Combine(".git", newFolderParentName)));

            this.Enlistment.GetVirtualPathTo(Path.Combine(".git", newFolderParentName)).ShouldNotExistOnDisk(fileSystem);
        }

        [TestCaseSource(typeof(FileRunnersAndFolders), FileRunnersAndFolders.TestRunners)]
        public void MoveVirtualNTFSFolderIntoVirtualNTFSFolder(FileSystemRunner fileSystem, string parentFolder)
        {
            string testFolderName = Path.Combine(parentFolder, "test_folder");
            string testFolderVirtualPath = this.Enlistment.GetVirtualPathTo(testFolderName);
            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, testFolderName, parentFolder);

            fileSystem.CreateDirectory(testFolderVirtualPath);
            testFolderVirtualPath.ShouldBeADirectory(fileSystem);

            string targetFolderName = Path.Combine(parentFolder, "target_folder");
            string targetFolderVirtualPath = this.Enlistment.GetVirtualPathTo(targetFolderName);
            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, targetFolderName, parentFolder);

            fileSystem.CreateDirectory(targetFolderVirtualPath);
            targetFolderVirtualPath.ShouldBeADirectory(fileSystem);

            string testFileName = Path.Combine(testFolderName, "test.txt");
            string testFileContents = "This is the contents of a test file";
            fileSystem.WriteAllText(this.Enlistment.GetVirtualPathTo(testFileName), testFileContents);
            this.Enlistment.GetVirtualPathTo(testFileName).ShouldBeAFile(fileSystem).WithContents(testFileContents);

            string newTestFolder = Path.Combine(targetFolderName, Path.GetFileName(testFolderName));
            string newFolderVirtualPath = this.Enlistment.GetVirtualPathTo(newTestFolder);

            fileSystem.MoveDirectory(testFolderVirtualPath, newFolderVirtualPath);
            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, testFolderName, parentFolder);
            newFolderVirtualPath.ShouldBeADirectory(fileSystem);

            string newTestFileName = Path.Combine(newTestFolder, Path.GetFileName(testFileName));
            this.Enlistment.GetVirtualPathTo(newTestFileName).ShouldBeAFile(fileSystem).WithContents(testFileContents);

            fileSystem.DeleteDirectory(targetFolderVirtualPath);
            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, targetFolderName, parentFolder);
        }

        [TestCaseSource(typeof(FileRunnersAndFolders), FileRunnersAndFolders.TestRunners)]
        public void RenameAndMoveVirtualNTFSFolderIntoVirtualNTFSFolder(FileSystemRunner fileSystem, string parentFolder)
        {
            string testFolderName = Path.Combine(parentFolder, "test_folder");
            string testFolderVirtualPath = this.Enlistment.GetVirtualPathTo(testFolderName);
            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, testFolderName, parentFolder);

            fileSystem.CreateDirectory(testFolderVirtualPath);
            testFolderVirtualPath.ShouldBeADirectory(fileSystem);

            string targetFolderName = Path.Combine(parentFolder, "target_folder");
            string targetFolderVirtualPath = this.Enlistment.GetVirtualPathTo(targetFolderName);
            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, targetFolderName, parentFolder);

            fileSystem.CreateDirectory(targetFolderVirtualPath);
            targetFolderVirtualPath.ShouldBeADirectory(fileSystem);

            string testFileName = "test.txt";
            string testFilePartialPath = Path.Combine(testFolderName, testFileName);
            string testFileContents = "This is the contents of a test file";
            fileSystem.WriteAllText(this.Enlistment.GetVirtualPathTo(testFilePartialPath), testFileContents);
            this.Enlistment.GetVirtualPathTo(testFilePartialPath).ShouldBeAFile(fileSystem).WithContents(testFileContents);

            string newTestFolder = Path.Combine(targetFolderName, "test_folder_renamed");
            string newFolderVirtualPath = this.Enlistment.GetVirtualPathTo(newTestFolder);

            fileSystem.MoveDirectory(testFolderVirtualPath, newFolderVirtualPath);
            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, testFolderName, parentFolder);
            newFolderVirtualPath.ShouldBeADirectory(fileSystem);

            string newTestFileName = Path.Combine(newTestFolder, testFileName);
            this.Enlistment.GetVirtualPathTo(newTestFileName).ShouldBeAFile(fileSystem).WithContents(testFileContents);

            fileSystem.DeleteDirectory(targetFolderVirtualPath);
            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, targetFolderName, parentFolder);
        }

        [TestCaseSource(typeof(FileSystemRunner), FileSystemRunner.TestRunners)]
        public void MoveVirtualNTFSFolderTreeIntoVirtualNTFSFolder(FileSystemRunner fileSystem)
        {
            string testFolderParent = "test_folder_parent";
            string testFolderChild = "test_folder_child";
            string testFolderGrandChild = "test_folder_grandchild";
            string testFile = "test.txt";
            this.Enlistment.GetVirtualPathTo(testFolderParent).ShouldNotExistOnDisk(fileSystem);

            // Create the folder tree (to move)
            fileSystem.CreateDirectory(this.Enlistment.GetVirtualPathTo(testFolderParent));
            this.Enlistment.GetVirtualPathTo(testFolderParent).ShouldBeADirectory(fileSystem);

            string realtiveChildFolderPath = Path.Combine(testFolderParent, testFolderChild);
            fileSystem.CreateDirectory(this.Enlistment.GetVirtualPathTo(realtiveChildFolderPath));
            this.Enlistment.GetVirtualPathTo(realtiveChildFolderPath).ShouldBeADirectory(fileSystem);

            string realtiveGrandChildFolderPath = Path.Combine(realtiveChildFolderPath, testFolderGrandChild);
            fileSystem.CreateDirectory(this.Enlistment.GetVirtualPathTo(realtiveGrandChildFolderPath));
            this.Enlistment.GetVirtualPathTo(realtiveGrandChildFolderPath).ShouldBeADirectory(fileSystem);

            string relativeTestFilePath = Path.Combine(realtiveGrandChildFolderPath, testFile);
            string testFileContents = "This is the contents of a test file";
            fileSystem.WriteAllText(this.Enlistment.GetVirtualPathTo(relativeTestFilePath), testFileContents);
            this.Enlistment.GetVirtualPathTo(relativeTestFilePath).ShouldBeAFile(fileSystem).WithContents(testFileContents);

            // Create the target
            string targetFolder = "target_folder";
            this.Enlistment.GetVirtualPathTo(targetFolder).ShouldNotExistOnDisk(fileSystem);

            fileSystem.CreateDirectory(this.Enlistment.GetVirtualPathTo(targetFolder));
            this.Enlistment.GetVirtualPathTo(targetFolder).ShouldBeADirectory(fileSystem);

            fileSystem.MoveDirectory(
                this.Enlistment.GetVirtualPathTo(testFolderParent),
                this.Enlistment.GetVirtualPathTo(Path.Combine(targetFolder, testFolderParent)));

            // The old tree structure should be gone
            this.Enlistment.GetVirtualPathTo(testFolderParent).ShouldNotExistOnDisk(fileSystem);
            this.Enlistment.GetVirtualPathTo(realtiveChildFolderPath).ShouldNotExistOnDisk(fileSystem);
            this.Enlistment.GetVirtualPathTo(realtiveGrandChildFolderPath).ShouldNotExistOnDisk(fileSystem);
            this.Enlistment.GetVirtualPathTo(relativeTestFilePath).ShouldNotExistOnDisk(fileSystem);

            // The tree should have been moved under the target folder
            testFolderParent = Path.Combine(targetFolder, testFolderParent);
            realtiveChildFolderPath = Path.Combine(testFolderParent, testFolderChild);
            realtiveGrandChildFolderPath = Path.Combine(realtiveChildFolderPath, testFolderGrandChild);
            relativeTestFilePath = Path.Combine(realtiveGrandChildFolderPath, testFile);

            this.Enlistment.GetVirtualPathTo(testFolderParent).ShouldBeADirectory(fileSystem);
            this.Enlistment.GetVirtualPathTo(realtiveChildFolderPath).ShouldBeADirectory(fileSystem);
            this.Enlistment.GetVirtualPathTo(realtiveGrandChildFolderPath).ShouldBeADirectory(fileSystem);
            this.Enlistment.GetVirtualPathTo(relativeTestFilePath).ShouldBeAFile(fileSystem).WithContents(testFileContents);

            // Cleanup
            fileSystem.DeleteDirectory(this.Enlistment.GetVirtualPathTo(targetFolder));

            this.Enlistment.GetVirtualPathTo(targetFolder).ShouldNotExistOnDisk(fileSystem);
            this.Enlistment.GetVirtualPathTo(testFolderParent).ShouldNotExistOnDisk(fileSystem);
            this.Enlistment.GetVirtualPathTo(realtiveChildFolderPath).ShouldNotExistOnDisk(fileSystem);
            this.Enlistment.GetVirtualPathTo(realtiveGrandChildFolderPath).ShouldNotExistOnDisk(fileSystem);
            this.Enlistment.GetVirtualPathTo(relativeTestFilePath).ShouldNotExistOnDisk(fileSystem);
        }

        [TestCaseSource(typeof(FileSystemRunner), FileSystemRunner.TestRunners)]
        public void MoveDotGitFullFolderTreeToDotGitFullFolder(FileSystemRunner fileSystem)
        {
            string testFolderRoot = ".git";
            string testFolderParent = "test_folder_parent";
            string testFolderChild = "test_folder_child";
            string testFolderGrandChild = "test_folder_grandchild";
            string testFile = "test.txt";
            this.Enlistment.GetVirtualPathTo(Path.Combine(testFolderRoot, testFolderParent)).ShouldNotExistOnDisk(fileSystem);

            // Create the folder tree (to move)
            fileSystem.CreateDirectory(this.Enlistment.GetVirtualPathTo(Path.Combine(testFolderRoot, testFolderParent)));
            this.Enlistment.GetVirtualPathTo(Path.Combine(testFolderRoot, testFolderParent)).ShouldBeADirectory(fileSystem);

            string realtiveChildFolderPath = Path.Combine(testFolderRoot, testFolderParent, testFolderChild);
            fileSystem.CreateDirectory(this.Enlistment.GetVirtualPathTo(realtiveChildFolderPath));
            this.Enlistment.GetVirtualPathTo(realtiveChildFolderPath).ShouldBeADirectory(fileSystem);

            string realtiveGrandChildFolderPath = Path.Combine(realtiveChildFolderPath, testFolderGrandChild);
            fileSystem.CreateDirectory(this.Enlistment.GetVirtualPathTo(realtiveGrandChildFolderPath));
            this.Enlistment.GetVirtualPathTo(realtiveGrandChildFolderPath).ShouldBeADirectory(fileSystem);

            string relativeTestFilePath = Path.Combine(realtiveGrandChildFolderPath, testFile);
            string testFileContents = "This is the contents of a test file";
            fileSystem.WriteAllText(this.Enlistment.GetVirtualPathTo(relativeTestFilePath), testFileContents);
            this.Enlistment.GetVirtualPathTo(relativeTestFilePath).ShouldBeAFile(fileSystem).WithContents(testFileContents);

            // Create the target
            string targetFolder = ".git\\target_folder";
            this.Enlistment.GetVirtualPathTo(targetFolder).ShouldNotExistOnDisk(fileSystem);

            fileSystem.CreateDirectory(this.Enlistment.GetVirtualPathTo(targetFolder));
            this.Enlistment.GetVirtualPathTo(targetFolder).ShouldBeADirectory(fileSystem);

            fileSystem.MoveDirectory(
                this.Enlistment.GetVirtualPathTo(Path.Combine(testFolderRoot, testFolderParent)),
                this.Enlistment.GetVirtualPathTo(Path.Combine(targetFolder, testFolderParent)));

            // The old tree structure should be gone
            this.Enlistment.GetVirtualPathTo(Path.Combine(testFolderRoot, testFolderParent)).ShouldNotExistOnDisk(fileSystem);
            this.Enlistment.GetVirtualPathTo(realtiveChildFolderPath).ShouldNotExistOnDisk(fileSystem);
            this.Enlistment.GetVirtualPathTo(realtiveGrandChildFolderPath).ShouldNotExistOnDisk(fileSystem);
            this.Enlistment.GetVirtualPathTo(relativeTestFilePath).ShouldNotExistOnDisk(fileSystem);

            // The tree should have been moved under the target folder
            testFolderParent = Path.Combine(targetFolder, testFolderParent);
            realtiveChildFolderPath = Path.Combine(testFolderParent, testFolderChild);
            realtiveGrandChildFolderPath = Path.Combine(realtiveChildFolderPath, testFolderGrandChild);
            relativeTestFilePath = Path.Combine(realtiveGrandChildFolderPath, testFile);

            this.Enlistment.GetVirtualPathTo(testFolderParent).ShouldBeADirectory(fileSystem);
            this.Enlistment.GetVirtualPathTo(realtiveChildFolderPath).ShouldBeADirectory(fileSystem);
            this.Enlistment.GetVirtualPathTo(realtiveGrandChildFolderPath).ShouldBeADirectory(fileSystem);
            this.Enlistment.GetVirtualPathTo(relativeTestFilePath).ShouldBeAFile(fileSystem).WithContents(testFileContents);

            // Cleanup
            fileSystem.DeleteDirectory(this.Enlistment.GetVirtualPathTo(targetFolder));

            this.Enlistment.GetVirtualPathTo(targetFolder).ShouldNotExistOnDisk(fileSystem);
            this.Enlistment.GetVirtualPathTo(testFolderParent).ShouldNotExistOnDisk(fileSystem);
            this.Enlistment.GetVirtualPathTo(realtiveChildFolderPath).ShouldNotExistOnDisk(fileSystem);
            this.Enlistment.GetVirtualPathTo(realtiveGrandChildFolderPath).ShouldNotExistOnDisk(fileSystem);
            this.Enlistment.GetVirtualPathTo(relativeTestFilePath).ShouldNotExistOnDisk(fileSystem);
        }

        [TestCaseSource(typeof(FileSystemRunner), FileSystemRunner.TestRunners)]
        public void DeleteIndexFileFails(FileSystemRunner fileSystem)
        {
            string indexFilePath = this.Enlistment.GetVirtualPathTo(@".git\index");
            indexFilePath.ShouldBeAFile(fileSystem);
            fileSystem.DeleteFile_AccessShouldBeDenied(indexFilePath);
            indexFilePath.ShouldBeAFile(fileSystem);
        }

        [TestCaseSource(typeof(FileRunnersAndFolders), FileRunnersAndFolders.TestRunners)]
        public void MoveVirtualNTFSFolderIntoInvalidFolder(FileSystemRunner fileSystem, string parentFolder)
        {
            string testFolderParent = Path.Combine(parentFolder, "test_folder_parent");
            string testFolderChild = "test_folder_child";
            string testFolderGrandChild = "test_folder_grandchild";
            string testFile = "test.txt";
            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, testFolderParent, parentFolder);

            // Create the folder tree (to move)
            fileSystem.CreateDirectory(this.Enlistment.GetVirtualPathTo(testFolderParent));
            this.Enlistment.GetVirtualPathTo(testFolderParent).ShouldBeADirectory(fileSystem);

            string realtiveChildFolderPath = Path.Combine(testFolderParent, testFolderChild);
            fileSystem.CreateDirectory(this.Enlistment.GetVirtualPathTo(realtiveChildFolderPath));
            this.Enlistment.GetVirtualPathTo(realtiveChildFolderPath).ShouldBeADirectory(fileSystem);

            string realtiveGrandChildFolderPath = Path.Combine(realtiveChildFolderPath, testFolderGrandChild);
            fileSystem.CreateDirectory(this.Enlistment.GetVirtualPathTo(realtiveGrandChildFolderPath));
            this.Enlistment.GetVirtualPathTo(realtiveGrandChildFolderPath).ShouldBeADirectory(fileSystem);

            string relativeTestFilePath = Path.Combine(realtiveGrandChildFolderPath, testFile);
            string testFileContents = "This is the contents of a test file";
            fileSystem.WriteAllText(this.Enlistment.GetVirtualPathTo(relativeTestFilePath), testFileContents);
            this.Enlistment.GetVirtualPathTo(relativeTestFilePath).ShouldBeAFile(fileSystem).WithContents(testFileContents);

            string targetFolder = Path.Combine(parentFolder, "target_folder_does_not_exists");
            this.Enlistment.GetVirtualPathTo(targetFolder).ShouldNotExistOnDisk(fileSystem);

            // This move should fail
            fileSystem.MoveDirectory_TargetShouldBeInvalid(
                this.Enlistment.GetVirtualPathTo(testFolderParent),
                this.Enlistment.GetVirtualPathTo(Path.Combine(targetFolder, Path.GetFileName(testFolderParent))));

            // The old tree structure should still be there
            this.Enlistment.GetVirtualPathTo(testFolderParent).ShouldBeADirectory(fileSystem);
            this.Enlistment.GetVirtualPathTo(realtiveChildFolderPath).ShouldBeADirectory(fileSystem);
            this.Enlistment.GetVirtualPathTo(realtiveGrandChildFolderPath).ShouldBeADirectory(fileSystem);
            this.Enlistment.GetVirtualPathTo(relativeTestFilePath).ShouldBeAFile(fileSystem).WithContents(testFileContents);

            // Cleanup
            fileSystem.DeleteDirectory(this.Enlistment.GetVirtualPathTo(testFolderParent));
            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, testFolderParent, parentFolder);
            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, realtiveChildFolderPath, parentFolder);
            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, realtiveGrandChildFolderPath, parentFolder);
            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, relativeTestFilePath, parentFolder);
        }

        [TestCaseSource(typeof(FileRunnersAndFolders), FileRunnersAndFolders.TestFolders)]
        public void CreateFileInheritsParentDirectoryAttributes(string parentFolder)
        {
            string parentDirectoryPath = this.Enlistment.GetVirtualPathTo(Path.Combine(parentFolder, "CreateFileInheritsParentDirectoryAttributes"));
            FileSystemRunner.DefaultRunner.CreateDirectory(parentDirectoryPath);
            DirectoryInfo parentDirInfo = new DirectoryInfo(parentDirectoryPath);
            parentDirInfo.Attributes |= FileAttributes.NoScrubData;
            parentDirInfo.Attributes.HasFlag(FileAttributes.NoScrubData).ShouldEqual(true);

            string targetFilePath = Path.Combine(parentDirectoryPath, "TargetFile");
            FileSystemRunner.DefaultRunner.WriteAllText(targetFilePath, "Some contents that don't matter");
            targetFilePath.ShouldBeAFile(FileSystemRunner.DefaultRunner).WithAttribute(FileAttributes.NoScrubData);

            FileSystemRunner.DefaultRunner.DeleteDirectory(parentDirectoryPath);
        }

        [TestCaseSource(typeof(FileRunnersAndFolders), FileRunnersAndFolders.TestFolders)]
        public void CreateDirectoryInheritsParentDirectoryAttributes(string parentFolder)
        {
            string parentDirectoryPath = this.Enlistment.GetVirtualPathTo(Path.Combine(parentFolder, "CreateDirectoryInheritsParentDirectoryAttributes"));
            FileSystemRunner.DefaultRunner.CreateDirectory(parentDirectoryPath);
            DirectoryInfo parentDirInfo = new DirectoryInfo(parentDirectoryPath);
            parentDirInfo.Attributes |= FileAttributes.NoScrubData;
            parentDirInfo.Attributes.HasFlag(FileAttributes.NoScrubData).ShouldEqual(true);

            string targetDirPath = Path.Combine(parentDirectoryPath, "TargetDir");
            FileSystemRunner.DefaultRunner.CreateDirectory(targetDirPath);
            targetDirPath.ShouldBeADirectory(FileSystemRunner.DefaultRunner).WithAttribute(FileAttributes.NoScrubData);

            FileSystemRunner.DefaultRunner.DeleteDirectory(parentDirectoryPath);
        }

        [TestCaseSource(typeof(FileRunnersAndFolders), FileRunnersAndFolders.TestFolders)]
        public void StreamAccessReadFromMemoryMappedVirtualNTFSFile(string parentFolder)
        {
            // Use SystemIORunner as the text we are writing is too long to pass to the command line
            FileSystemRunner fileSystem = new SystemIORunner();

            string filename = Path.Combine(parentFolder, "StreamAccessReadFromMemoryMappedVirtualNTFSFile");
            string fileVirtualPath = this.Enlistment.GetVirtualPathTo(filename);
            fileVirtualPath.ShouldNotExistOnDisk(fileSystem);

            StringBuilder contentsBuilder = new StringBuilder();
            while (contentsBuilder.Length < 4096 * 2)
            {
                contentsBuilder.Append(Guid.NewGuid().ToString());
            }

            string contents = contentsBuilder.ToString();

            fileSystem.WriteAllText(fileVirtualPath, contents);
            fileVirtualPath.ShouldBeAFile(fileSystem).WithContents(contents);

            using (MemoryMappedFile mmf = MemoryMappedFile.CreateFromFile(fileVirtualPath))
            {
                int offset = 0;
                int size = contents.Length;
                using (MemoryMappedViewStream streamAccessor = mmf.CreateViewStream(offset, size))
                {
                    streamAccessor.CanRead.ShouldEqual(true);

                    for (int i = 0; i < size; ++i)
                    {
                        streamAccessor.ReadByte().ShouldEqual(contents[i]);
                    }
                }
            }

            fileSystem.DeleteFile(fileVirtualPath);
            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, filename, parentFolder);
        }

        [TestCaseSource(typeof(FileRunnersAndFolders), FileRunnersAndFolders.TestFolders)]
        public void RandomAccessReadFromMemoryMappedVirtualNTFSFile(string parentFolder)
        {
            // Use SystemIORunner as the text we are writing is too long to pass to the command line
            FileSystemRunner fileSystem = new SystemIORunner();

            string filename = Path.Combine(parentFolder, "RandomAccessReadFromMemoryMappedVirtualNTFSFile");
            string fileVirtualPath = this.Enlistment.GetVirtualPathTo(filename);
            fileVirtualPath.ShouldNotExistOnDisk(fileSystem);

            StringBuilder contentsBuilder = new StringBuilder();
            while (contentsBuilder.Length < 4096 * 2)
            {
                contentsBuilder.Append(Guid.NewGuid().ToString());
            }

            string contents = contentsBuilder.ToString();

            fileSystem.WriteAllText(fileVirtualPath, contents);
            fileVirtualPath.ShouldBeAFile(fileSystem).WithContents(contents);

            using (MemoryMappedFile mmf = MemoryMappedFile.CreateFromFile(fileVirtualPath))
            {
                int offset = 0;
                int size = contents.Length;
                using (MemoryMappedViewAccessor randAccessor = mmf.CreateViewAccessor(offset, size))
                {
                    randAccessor.CanRead.ShouldEqual(true);

                    for (int i = 0; i < size; ++i)

                    {
                        ((char)randAccessor.ReadByte(i)).ShouldEqual(contents[i]);
                    }

                    for (int i = size - 1; i >= 0; --i)
                    {
                        ((char)randAccessor.ReadByte(i)).ShouldEqual(contents[i]);
                    }
                }
            }

            fileSystem.DeleteFile(fileVirtualPath);
            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, filename, parentFolder);
        }

        [TestCaseSource(typeof(FileRunnersAndFolders), FileRunnersAndFolders.TestFolders)]
        public void StreamAccessReadWriteFromMemoryMappedVirtualNTFSFile(string parentFolder)
        {
            // Use SystemIORunner as the text we are writing is too long to pass to the command line
            FileSystemRunner fileSystem = new SystemIORunner();

            string filename = Path.Combine(parentFolder, "StreamAccessReadWriteFromMemoryMappedVirtualNTFSFile");
            string fileVirtualPath = this.Enlistment.GetVirtualPathTo(filename);
            fileVirtualPath.ShouldNotExistOnDisk(fileSystem);

            StringBuilder contentsBuilder = new StringBuilder();
            while (contentsBuilder.Length < 4096 * 2)
            {
                contentsBuilder.Append(Guid.NewGuid().ToString());
            }

            string contents = contentsBuilder.ToString();

            fileSystem.WriteAllText(fileVirtualPath, contents);
            fileVirtualPath.ShouldBeAFile(fileSystem).WithContents(contents);

            using (MemoryMappedFile mmf = MemoryMappedFile.CreateFromFile(fileVirtualPath))
            {
                int offset = 64;
                int size = contents.Length;
                string newContent = "**NEWCONTENT**";

                using (MemoryMappedViewStream streamAccessor = mmf.CreateViewStream(offset, size - offset))
                {
                    streamAccessor.CanRead.ShouldEqual(true);
                    streamAccessor.CanWrite.ShouldEqual(true);

                    for (int i = offset; i < size - offset; ++i)
                    {
                        streamAccessor.ReadByte().ShouldEqual(contents[i]);
                    }

                    // Reset to the start of the stream (which will place the streamAccessor at offset in the memory file)
                    streamAccessor.Seek(0, SeekOrigin.Begin);
                    byte[] newContentBuffer = Encoding.ASCII.GetBytes(newContent);

                    streamAccessor.Write(newContentBuffer, 0, newContent.Length);

                    for (int i = 0; i < newContent.Length; ++i)
                    {
                        contentsBuilder[offset + i] = newContent[i];
                    }

                    contents = contentsBuilder.ToString();
                }

                // Verify the file has the new contents inserted into it
                using (MemoryMappedViewStream streamAccessor = mmf.CreateViewStream(offset: 0, size: size))
                {
                    for (int i = 0; i < size; ++i)
                    {
                        streamAccessor.ReadByte().ShouldEqual(contents[i]);
                    }
                }
            }

            // Confirm the new contents was written to disk
            fileVirtualPath.ShouldBeAFile(fileSystem).WithContents(contents);

            fileSystem.DeleteFile(fileVirtualPath);
            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, filename, parentFolder);
        }

        [TestCaseSource(typeof(FileRunnersAndFolders), FileRunnersAndFolders.TestFolders)]
        public void RandomAccessReadWriteFromMemoryMappedVirtualNTFSFile(string parentFolder)
        {
            // Use SystemIORunner as the text we are writing is too long to pass to the command line
            FileSystemRunner fileSystem = new SystemIORunner();

            string filename = Path.Combine(parentFolder, "RandomAccessReadWriteFromMemoryMappedVirtualNTFSFile");
            string fileVirtualPath = this.Enlistment.GetVirtualPathTo(filename);
            fileVirtualPath.ShouldNotExistOnDisk(fileSystem);

            StringBuilder contentsBuilder = new StringBuilder();
            while (contentsBuilder.Length < 4096 * 2)
            {
                contentsBuilder.Append(Guid.NewGuid().ToString());
            }

            string contents = contentsBuilder.ToString();

            fileSystem.WriteAllText(fileVirtualPath, contents);
            fileVirtualPath.ShouldBeAFile(fileSystem).WithContents(contents);

            using (MemoryMappedFile mmf = MemoryMappedFile.CreateFromFile(fileVirtualPath))
            {
                int offset = 64;
                int size = contents.Length;
                string newContent = "**NEWCONTENT**";

                using (MemoryMappedViewAccessor randomAccessor = mmf.CreateViewAccessor(offset, size - offset))
                {
                    randomAccessor.CanRead.ShouldEqual(true);
                    randomAccessor.CanWrite.ShouldEqual(true);

                    for (int i = 0; i < size - offset; ++i)
                    {
                        ((char)randomAccessor.ReadByte(i)).ShouldEqual(contents[i + offset]);
                    }

                    for (int i = 0; i < newContent.Length; ++i)
                    {
                        // Convert to byte before writing rather than writing as char, because char version will write a 16-bit
                        // unicode char
                        randomAccessor.Write(i, Convert.ToByte(newContent[i]));
                        ((char)randomAccessor.ReadByte(i)).ShouldEqual(newContent[i]);
                    }

                    for (int i = 0; i < newContent.Length; ++i)
                    {
                        contentsBuilder[offset + i] = newContent[i];
                    }

                    contents = contentsBuilder.ToString();
                }

                // Verify the file has the new contents inserted into it
                using (MemoryMappedViewAccessor randomAccessor = mmf.CreateViewAccessor(offset: 0, size: size))
                {
                    for (int i = 0; i < size; ++i)
                    {
                        ((char)randomAccessor.ReadByte(i)).ShouldEqual(contents[i]);
                    }
                }
            }

            // Confirm the new contents was written to disk
            fileVirtualPath.ShouldBeAFile(fileSystem).WithContents(contents);

            fileSystem.DeleteFile(fileVirtualPath);
            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, filename, parentFolder);
        }

        [TestCaseSource(typeof(FileRunnersAndFolders), FileRunnersAndFolders.TestFolders)]
        public void StreamAccessToExistingMemoryMappedFile(string parentFolder)
        {
            // Use SystemIORunner as the text we are writing is too long to pass to the command line
            FileSystemRunner fileSystem = new SystemIORunner();

            string filename = Path.Combine(parentFolder, "StreamAccessToExistingMemoryMappedFile");
            string fileVirtualPath = this.Enlistment.GetVirtualPathTo(filename);
            fileVirtualPath.ShouldNotExistOnDisk(fileSystem);

            StringBuilder contentsBuilder = new StringBuilder();
            while (contentsBuilder.Length < 4096 * 2)
            {
                contentsBuilder.Append(Guid.NewGuid().ToString());
            }

            string contents = contentsBuilder.ToString();
            int size = contents.Length;

            fileSystem.WriteAllText(fileVirtualPath, contents);
            fileVirtualPath.ShouldBeAFile(fileSystem).WithContents(contents);

            string memoryMapFileName = "StreamAccessFile";
            using (MemoryMappedFile mmf = MemoryMappedFile.CreateFromFile(fileVirtualPath, FileMode.Open, memoryMapFileName))
            {
                Thread[] threads = new Thread[4];
                bool keepRunning = true;
                for (int i = 0; i < threads.Length; ++i)
                {
                    int myIndex = i;
                    threads[i] = new Thread(() =>
                    {
                        // Create random seeks (seeded for repeatability)
                        Random randNum = new Random(myIndex);

                        using (MemoryMappedFile threadFile = MemoryMappedFile.OpenExisting(memoryMapFileName))
                        {
                            while (keepRunning)
                            {
                                // Pick an offset somewhere in the first half of the file
                                int offset = randNum.Next(size / 2);

                                using (MemoryMappedViewStream streamAccessor = threadFile.CreateViewStream(offset, size - offset))
                                {
                                    for (int j = 0; j < size - offset; ++j)
                                    {
                                        streamAccessor.ReadByte().ShouldEqual(contents[j + offset]);
                                    }
                                }
                            }
                        }
                    });

                    threads[i].Start();
                }

                Thread.Sleep(500);
                keepRunning = false;

                for (int i = 0; i < threads.Length; ++i)
                {
                    threads[i].Join();
                }
            }

            fileSystem.DeleteFile(fileVirtualPath);
            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, filename, parentFolder);
        }

        [TestCaseSource(typeof(FileRunnersAndFolders), FileRunnersAndFolders.TestFolders)]
        public void RandomAccessToExistingMemoryMappedFile(string parentFolder)
        {
            // Use SystemIORunner as the text we are writing is too long to pass to the command line
            FileSystemRunner fileSystem = new SystemIORunner();

            string filename = Path.Combine(parentFolder, "RandomAccessToExistingMemoryMappedFile");
            string fileVirtualPath = this.Enlistment.GetVirtualPathTo(filename);
            fileVirtualPath.ShouldNotExistOnDisk(fileSystem);

            StringBuilder contentsBuilder = new StringBuilder();
            while (contentsBuilder.Length < 4096 * 2)
            {
                contentsBuilder.Append(Guid.NewGuid().ToString());
            }

            string contents = contentsBuilder.ToString();
            int size = contents.Length;

            fileSystem.WriteAllText(fileVirtualPath, contents);
            fileVirtualPath.ShouldBeAFile(fileSystem).WithContents(contents);

            string memoryMapFileName = "RandomAccessFile";
            using (MemoryMappedFile mmf = MemoryMappedFile.CreateFromFile(fileVirtualPath, FileMode.Open, memoryMapFileName))
            {
                Thread[] threads = new Thread[4];
                bool keepRunning = true;
                for (int i = 0; i < threads.Length; ++i)
                {
                    int myIndex = i;
                    threads[i] = new Thread(() =>
                    {
                        // Create random seeks (seeded for repeatability)
                        Random randNum = new Random(myIndex);

                        using (MemoryMappedFile threadFile = MemoryMappedFile.OpenExisting(memoryMapFileName))
                        {
                            while (keepRunning)
                            {
                                // Pick an offset somewhere in the first half of the file
                                int offset = randNum.Next(size / 2);

                                using (MemoryMappedViewAccessor randomAccessor = threadFile.CreateViewAccessor(offset, size - offset))
                                {
                                    for (int j = 0; j < size - offset; ++j)
                                    {
                                        ((char)randomAccessor.ReadByte(j)).ShouldEqual(contents[j + offset]);
                                    }
                                }
                            }
                        }
                    });

                    threads[i].Start();
                }

                Thread.Sleep(500);
                keepRunning = false;

                for (int i = 0; i < threads.Length; ++i)
                {
                    threads[i].Join();
                }
            }

            fileSystem.DeleteFile(fileVirtualPath);
            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, filename, parentFolder);
        }

        [TestCaseSource(typeof(FileRunnersAndFolders), FileRunnersAndFolders.TestFolders)]
        public void NativeReadAndWriteSeparateHandles(string parentFolder)
        {
            FileSystemRunner fileSystem = FileSystemRunner.DefaultRunner;

            string filename = Path.Combine(parentFolder, "NativeReadAndWriteSeparateHandles");
            string fileVirtualPath = this.Enlistment.GetVirtualPathTo(filename);
            fileVirtualPath.ShouldNotExistOnDisk(fileSystem);

            NativeTests.ReadAndWriteSeparateHandles(fileVirtualPath).ShouldEqual(true);

            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, filename, parentFolder);
        }

        [TestCaseSource(typeof(FileRunnersAndFolders), FileRunnersAndFolders.TestFolders)]
        public void NativeReadAndWriteSameHandle(string parentFolder)
        {
            FileSystemRunner fileSystem = FileSystemRunner.DefaultRunner;

            string filename = Path.Combine(parentFolder, "NativeReadAndWriteSameHandle");
            string fileVirtualPath = this.Enlistment.GetVirtualPathTo(filename);
            fileVirtualPath.ShouldNotExistOnDisk(fileSystem);

            NativeTests.ReadAndWriteSameHandle(fileVirtualPath, synchronousIO: false).ShouldEqual(true);

            fileVirtualPath.ShouldNotExistOnDisk(fileSystem);

            NativeTests.ReadAndWriteSameHandle(fileVirtualPath, synchronousIO: true).ShouldEqual(true);

            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, filename, parentFolder);
        }

        [TestCaseSource(typeof(FileRunnersAndFolders), FileRunnersAndFolders.TestFolders)]
        public void NativeReadAndWriteRepeatedly(string parentFolder)
        {
            FileSystemRunner fileSystem = FileSystemRunner.DefaultRunner;

            string filename = Path.Combine(parentFolder, "NativeReadAndWriteRepeatedly");
            string fileVirtualPath = this.Enlistment.GetVirtualPathTo(filename);
            fileVirtualPath.ShouldNotExistOnDisk(fileSystem);

            NativeTests.ReadAndWriteRepeatedly(fileVirtualPath, synchronousIO: false).ShouldEqual(true);

            fileVirtualPath.ShouldNotExistOnDisk(fileSystem);

            NativeTests.ReadAndWriteRepeatedly(fileVirtualPath, synchronousIO: true).ShouldEqual(true);

            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, filename, parentFolder);
        }

        [TestCaseSource(typeof(FileRunnersAndFolders), FileRunnersAndFolders.TestFolders)]
        public void NativeRemoveReadOnlyAttribute(string parentFolder)
        {
            FileSystemRunner fileSystem = FileSystemRunner.DefaultRunner;

            string filename = Path.Combine(parentFolder, "NativeRemoveReadOnlyAttribute");
            string fileVirtualPath = this.Enlistment.GetVirtualPathTo(filename);
            fileVirtualPath.ShouldNotExistOnDisk(fileSystem);

            NativeTests.RemoveReadOnlyAttribute(fileVirtualPath).ShouldEqual(true);

            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, filename, parentFolder);
        }

        [TestCaseSource(typeof(FileRunnersAndFolders), FileRunnersAndFolders.TestFolders)]
        public void NativeCannotWriteToReadOnlyFile(string parentFolder)
        {
            FileSystemRunner fileSystem = FileSystemRunner.DefaultRunner;

            string filename = Path.Combine(parentFolder, "NativeCannotWriteToReadOnlyFile");
            string fileVirtualPath = this.Enlistment.GetVirtualPathTo(filename);
            fileVirtualPath.ShouldNotExistOnDisk(fileSystem);

            NativeTests.CannotWriteToReadOnlyFile(fileVirtualPath).ShouldEqual(true);

            FileRunnersAndFolders.ShouldNotExistOnDisk(this.Enlistment, fileSystem, filename, parentFolder);
        }

        [TestCase]
        public void NativeEnumerationErrorsMatchNTFS()
        {
            FileSystemRunner fileSystem = FileSystemRunner.DefaultRunner;
            string nonExistentVirtualPath = this.Enlistment.GetVirtualPathTo("this_does_not_exist");
            nonExistentVirtualPath.ShouldNotExistOnDisk(fileSystem);
            string nonExistentPhysicalPath = Path.Combine(this.Enlistment.DotRGFSRoot, "this_does_not_exist");
            nonExistentPhysicalPath.ShouldNotExistOnDisk(fileSystem);

            NativeTests.EnumerationErrorsMatchNTFSForNonExistentFolder(nonExistentVirtualPath, nonExistentPhysicalPath).ShouldEqual(true);
        }

        [TestCase]
        public void NativeEnumerationErrorsMatchNTFSForNestedFolder()
        {
            FileSystemRunner fileSystem = FileSystemRunner.DefaultRunner;

            this.Enlistment.GetVirtualPathTo("RGFS").ShouldBeADirectory(fileSystem);
            string nonExistentVirtualPath = this.Enlistment.GetVirtualPathTo("RGFS\\this_does_not_exist");
            nonExistentVirtualPath.ShouldNotExistOnDisk(fileSystem);

            this.Enlistment.DotRGFSRoot.ShouldBeADirectory(fileSystem);
            string nonExistentPhysicalPath = Path.Combine(this.Enlistment.DotRGFSRoot, "this_does_not_exist");
            nonExistentPhysicalPath.ShouldNotExistOnDisk(fileSystem);

            NativeTests.EnumerationErrorsMatchNTFSForNonExistentFolder(nonExistentVirtualPath, nonExistentPhysicalPath).ShouldEqual(true);
        }

        [TestCase]
        public void NativeEnumerationDotGitFolderErrorsMatchNTFS()
        {
            FileSystemRunner fileSystem = FileSystemRunner.DefaultRunner;
            string nonExistentVirtualPath = this.Enlistment.GetVirtualPathTo(".git\\this_does_not_exist");
            nonExistentVirtualPath.ShouldNotExistOnDisk(fileSystem);
            string nonExistentPhysicalPath = Path.Combine(this.Enlistment.DotRGFSRoot, "this_does_not_exist");
            nonExistentPhysicalPath.ShouldNotExistOnDisk(fileSystem);

            NativeTests.EnumerationErrorsMatchNTFSForNonExistentFolder(nonExistentVirtualPath, nonExistentPhysicalPath).ShouldEqual(true);
        }

        [TestCase]
        public void NativeEnumerationErrorsMatchNTFSForEmptyNewFolder()
        {
            FileSystemRunner fileSystem = FileSystemRunner.DefaultRunner;
            string newVirtualFolderPath = this.Enlistment.GetVirtualPathTo("new_folder");
            newVirtualFolderPath.ShouldNotExistOnDisk(fileSystem);
            fileSystem.CreateDirectory(newVirtualFolderPath);
            newVirtualFolderPath.ShouldBeADirectory(fileSystem);

            string newPhysicalFolderPath = Path.Combine(this.Enlistment.DotRGFSRoot, "new_folder");
            newPhysicalFolderPath.ShouldNotExistOnDisk(fileSystem);
            fileSystem.CreateDirectory(newPhysicalFolderPath);
            newPhysicalFolderPath.ShouldBeADirectory(fileSystem);

            NativeTests.EnumerationErrorsMatchNTFSForEmptyFolder(newVirtualFolderPath, newPhysicalFolderPath).ShouldEqual(true);

            fileSystem.DeleteDirectory(newVirtualFolderPath);
            newVirtualFolderPath.ShouldNotExistOnDisk(fileSystem);
            fileSystem.DeleteDirectory(newPhysicalFolderPath);
            newPhysicalFolderPath.ShouldNotExistOnDisk(fileSystem);
        }

        [TestCase]
        public void NativeDeleteEmptyFolderWithFileDispositionOnClose()
        {
            FileSystemRunner fileSystem = FileSystemRunner.DefaultRunner;
            string newVirtualFolderPath = this.Enlistment.GetVirtualPathTo("new_folder");
            newVirtualFolderPath.ShouldNotExistOnDisk(fileSystem);
            fileSystem.CreateDirectory(newVirtualFolderPath);
            newVirtualFolderPath.ShouldBeADirectory(fileSystem);

            NativeTests.CanDeleteEmptyFolderWithFileDispositionOnClose(newVirtualFolderPath).ShouldEqual(true);

            newVirtualFolderPath.ShouldNotExistOnDisk(fileSystem);
        }

        [TestCase]
        public void NativeQueryDirectoryFileRestartScanResetsFilter()
        {
            FileSystemRunner fileSystem = FileSystemRunner.DefaultRunner;
            string folderPath = this.Enlistment.GetVirtualPathTo("EnumerateAndReadTestFiles");
            folderPath.ShouldBeADirectory(fileSystem);

            NativeTests.QueryDirectoryFileRestartScanResetsFilter(folderPath).ShouldEqual(true);
        }

        [TestCase]
        public void ErrorWhenPathTreatsFileAsFolderMatchesNTFS_VirtualRGFltPath()
        {
            FileSystemRunner fileSystem = FileSystemRunner.DefaultRunner;
            string existingFileVirtualPath = this.Enlistment.GetVirtualPathTo("ErrorWhenPathTreatsFileAsFolderMatchesNTFS\\virtual");
            string existingFilePhysicalPath = this.CreateFileInPhysicalPath(fileSystem);

            foreach (CreationDisposition creationDispostion in Enum.GetValues(typeof(CreationDisposition)))
            {
                NativeTests.ErrorWhenPathTreatsFileAsFolderMatchesNTFS(existingFileVirtualPath, existingFilePhysicalPath, (int)creationDispostion).ShouldEqual(true);
            }
        }

        [TestCase]
        public void ErrorWhenPathTreatsFileAsFolderMatchesNTFS_PartialRGFltPath()
        {
            FileSystemRunner fileSystem = FileSystemRunner.DefaultRunner;

            string existingFileVirtualPath = this.Enlistment.GetVirtualPathTo("ErrorWhenPathTreatsFileAsFolderMatchesNTFS\\partial");
            existingFileVirtualPath.ShouldBeAFile(fileSystem);
            fileSystem.ReadAllText(existingFileVirtualPath);
            string existingFilePhysicalPath = this.CreateFileInPhysicalPath(fileSystem);

            foreach (CreationDisposition creationDispostion in Enum.GetValues(typeof(CreationDisposition)))
            {
                NativeTests.ErrorWhenPathTreatsFileAsFolderMatchesNTFS(existingFileVirtualPath, existingFilePhysicalPath, (int)creationDispostion).ShouldEqual(true);
            }
        }

        [TestCase]
        public void ErrorWhenPathTreatsFileAsFolderMatchesNTFS_FullRGFltPath()
        {
            FileSystemRunner fileSystem = FileSystemRunner.DefaultRunner;

            string existingFileVirtualPath = this.Enlistment.GetVirtualPathTo("ErrorWhenPathTreatsFileAsFolderMatchesNTFS\\full");
            existingFileVirtualPath.ShouldBeAFile(fileSystem);
            fileSystem.AppendAllText(existingFileVirtualPath, "extra text");
            string existingFilePhysicalPath = this.CreateFileInPhysicalPath(fileSystem);

            foreach (CreationDisposition creationDispostion in Enum.GetValues(typeof(CreationDisposition)))
            {
                NativeTests.ErrorWhenPathTreatsFileAsFolderMatchesNTFS(existingFileVirtualPath, existingFilePhysicalPath, (int)creationDispostion).ShouldEqual(true);
            }
        }

        [TestCase]
        public void EnumerateWithTrailingSlashMatchesWithoutSlashAfterDelete()
        {
            NativeTrailingSlashTests.EnumerateWithTrailingSlashMatchesWithoutSlashAfterDelete(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_ModifyFileInScratchAndDir()
        {
            RGFlt_BugRegressionTest.RGFlt_ModifyFileInScratchAndDir(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_RMDIRTest1()
        {
            RGFlt_BugRegressionTest.RGFlt_RMDIRTest1(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_RMDIRTest2()
        {
            RGFlt_BugRegressionTest.RGFlt_RMDIRTest2(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_RMDIRTest3()
        {
            RGFlt_BugRegressionTest.RGFlt_RMDIRTest3(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_RMDIRTest4()
        {
            RGFlt_BugRegressionTest.RGFlt_RMDIRTest4(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_RMDIRTest5()
        {
            RGFlt_BugRegressionTest.RGFlt_RMDIRTest5(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_DeepNonExistFileUnderPartial()
        {
            RGFlt_BugRegressionTest.RGFlt_DeepNonExistFileUnderPartial(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_SupersededReparsePoint()
        {
            RGFlt_BugRegressionTest.RGFlt_SupersededReparsePoint(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_DeleteVirtualFile_SetDisposition()
        {
            RGFlt_DeleteFileTest.RGFlt_DeleteVirtualFile_SetDisposition(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_DeleteVirtualFile_DeleteOnClose()
        {
            RGFlt_DeleteFileTest.RGFlt_DeleteVirtualFile_DeleteOnClose(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_DeletePlaceholder_SetDisposition()
        {
            RGFlt_DeleteFileTest.RGFlt_DeletePlaceholder_SetDisposition(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_DeletePlaceholder_DeleteOnClose()
        {
            RGFlt_DeleteFileTest.RGFlt_DeletePlaceholder_DeleteOnClose(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_DeleteFullFile_SetDisposition()
        {
            RGFlt_DeleteFileTest.RGFlt_DeleteFullFile_SetDisposition(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_DeleteFullFile_DeleteOnClose()
        {
            RGFlt_DeleteFileTest.RGFlt_DeleteFullFile_DeleteOnClose(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_DeleteLocalFile_SetDisposition()
        {
            RGFlt_DeleteFileTest.RGFlt_DeleteLocalFile_SetDisposition(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_DeleteLocalFile_DeleteOnClose()
        {
            RGFlt_DeleteFileTest.RGFlt_DeleteLocalFile_DeleteOnClose(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_DeleteNotExistFile_SetDisposition()
        {
            RGFlt_DeleteFileTest.RGFlt_DeleteNotExistFile_SetDisposition(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_DeleteNotExistFile_DeleteOnClose()
        {
            RGFlt_DeleteFileTest.RGFlt_DeleteNotExistFile_DeleteOnClose(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_DeleteNonRootVirtualFile_SetDisposition()
        {
            RGFlt_DeleteFileTest.RGFlt_DeleteNonRootVirtualFile_SetDisposition(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_DeleteNonRootVirtualFile_DeleteOnClose()
        {
            RGFlt_DeleteFileTest.RGFlt_DeleteNonRootVirtualFile_DeleteOnClose(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_DeleteFileOutsideVRoot_SetDisposition()
        {
            RGFlt_DeleteFileTest.RGFlt_DeleteFileOutsideVRoot_SetDisposition(Path.GetDirectoryName(this.Enlistment.RepoRoot)).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_DeleteFileOutsideVRoot_DeleteOnClose()
        {
            RGFlt_DeleteFileTest.RGFlt_DeleteFileOutsideVRoot_DeleteOnClose(Path.GetDirectoryName(this.Enlistment.RepoRoot)).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_DeleteVirtualNonEmptyFolder_SetDisposition()
        {
            RGFlt_DeleteFolderTest.RGFlt_DeleteVirtualNonEmptyFolder_SetDisposition(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_DeleteVirtualNonEmptyFolder_DeleteOnClose()
        {
            RGFlt_DeleteFolderTest.RGFlt_DeleteVirtualNonEmptyFolder_DeleteOnClose(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_DeletePlaceholderNonEmptyFolder_SetDisposition()
        {
            RGFlt_DeleteFolderTest.RGFlt_DeletePlaceholderNonEmptyFolder_SetDisposition(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_DeletePlaceholderNonEmptyFolder_DeleteOnClose()
        {
            RGFlt_DeleteFolderTest.RGFlt_DeletePlaceholderNonEmptyFolder_DeleteOnClose(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_DeleteLocalEmptyFolder_SetDisposition()
        {
            RGFlt_DeleteFolderTest.RGFlt_DeleteLocalEmptyFolder_SetDisposition(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_DeleteLocalEmptyFolder_DeleteOnClose()
        {
            RGFlt_DeleteFolderTest.RGFlt_DeleteLocalEmptyFolder_DeleteOnClose(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_DeleteNonRootVirtualFolder_SetDisposition()
        {
            RGFlt_DeleteFolderTest.RGFlt_DeleteNonRootVirtualFolder_SetDisposition(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_DeleteNonRootVirtualFolder_DeleteOnClose()
        {
            RGFlt_DeleteFolderTest.RGFlt_DeleteNonRootVirtualFolder_DeleteOnClose(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_EnumEmptyFolder()
        {
            RGFlt_DirEnumTest.RGFlt_EnumEmptyFolder(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_EnumFolderWithOneFileInRepo()
        {
            RGFlt_DirEnumTest.RGFlt_EnumFolderWithOneFileInPackage(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_EnumFolderWithOneFileInRepoBeforeScratchFile()
        {
            RGFlt_DirEnumTest.RGFlt_EnumFolderWithOneFileInBoth(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_EnumFolderWithOneFileInRepoAfterScratchFile()
        {
            RGFlt_DirEnumTest.RGFlt_EnumFolderWithOneFileInBoth1(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_EnumFolderDeleteExistingFile()
        {
            RGFlt_DirEnumTest.RGFlt_EnumFolderDeleteExistingFile(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_EnumFolderSmallBuffer()
        {
            RGFlt_DirEnumTest.RGFlt_EnumFolderSmallBuffer(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_ModifyFileInScratchAndCheckLastWriteTime()
        {
            RGFlt_FileAttributeTest.RGFlt_ModifyFileInScratchAndCheckLastWriteTime(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_FileSize()
        {
            RGFlt_FileAttributeTest.RGFlt_FileSize(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_ModifyFileInScratchAndCheckFileSize()
        {
            RGFlt_FileAttributeTest.RGFlt_ModifyFileInScratchAndCheckFileSize(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_FileAttributes()
        {
            RGFlt_FileAttributeTest.RGFlt_FileAttributes(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_OneEAAttributeWillPass()
        {
            RGFlt_FileEATest.RGFlt_OneEAAttributeWillPass(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_OpenRootFolder()
        {
            RGFlt_FileOperationTest.RGFlt_OpenRootFolder(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_WriteAndVerify()
        {
            RGFlt_FileOperationTest.RGFlt_WriteAndVerify(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_DeleteExistingFile()
        {
            RGFlt_FileOperationTest.RGFlt_DeleteExistingFile(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_OpenNonExistingFile()
        {
            RGFlt_FileOperationTest.RGFlt_OpenNonExistingFile(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_MoveFile_NoneToNone()
        {
            RGFlt_MoveFileTest.RGFlt_MoveFile_NoneToNone(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_MoveFile_VirtualToNone()
        {
            RGFlt_MoveFileTest.RGFlt_MoveFile_VirtualToNone(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_MoveFile_PartialToNone()
        {
            RGFlt_MoveFileTest.RGFlt_MoveFile_PartialToNone(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_MoveFile_FullToNone()
        {
            RGFlt_MoveFileTest.RGFlt_MoveFile_FullToNone(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_MoveFile_LocalToNone()
        {
            RGFlt_MoveFileTest.RGFlt_MoveFile_LocalToNone(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_MoveFile_VirtualToVirtual()
        {
            RGFlt_MoveFileTest.RGFlt_MoveFile_VirtualToVirtual(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_MoveFile_VirtualToVirtualFileNameChanged()
        {
            RGFlt_MoveFileTest.RGFlt_MoveFile_VirtualToVirtualFileNameChanged(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_MoveFile_VirtualToPartial()
        {
            RGFlt_MoveFileTest.RGFlt_MoveFile_VirtualToPartial(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_MoveFile_PartialToPartial()
        {
            RGFlt_MoveFileTest.RGFlt_MoveFile_PartialToPartial(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_MoveFile_LocalToVirtual()
        {
            RGFlt_MoveFileTest.RGFlt_MoveFile_LocalToVirtual(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_MoveFile_VirtualToVirtualIntermidiateDirNotExist()
        {
            RGFlt_MoveFileTest.RGFlt_MoveFile_VirtualToVirtualIntermidiateDirNotExist(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_MoveFile_VirtualToNoneIntermidiateDirNotExist()
        {
            RGFlt_MoveFileTest.RGFlt_MoveFile_VirtualToNoneIntermidiateDirNotExist(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_MoveFile_OutsideToNone()
        {
            RGFlt_MoveFileTest.RGFlt_MoveFile_OutsideToNone(Path.GetDirectoryName(this.Enlistment.RepoRoot), this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_MoveFile_OutsideToVirtual()
        {
            RGFlt_MoveFileTest.RGFlt_MoveFile_OutsideToVirtual(Path.GetDirectoryName(this.Enlistment.RepoRoot), this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_MoveFile_OutsideToPartial()
        {
            RGFlt_MoveFileTest.RGFlt_MoveFile_OutsideToPartial(Path.GetDirectoryName(this.Enlistment.RepoRoot), this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_MoveFile_NoneToOutside()
        {
            RGFlt_MoveFileTest.RGFlt_MoveFile_NoneToOutside(Path.GetDirectoryName(this.Enlistment.RepoRoot), this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_MoveFile_VirtualToOutside()
        {
            RGFlt_MoveFileTest.RGFlt_MoveFile_VirtualToOutside(Path.GetDirectoryName(this.Enlistment.RepoRoot), this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_MoveFile_PartialToOutside()
        {
            RGFlt_MoveFileTest.RGFlt_MoveFile_PartialToOutside(Path.GetDirectoryName(this.Enlistment.RepoRoot), this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_MoveFile_OutsideToOutside()
        {
            RGFlt_MoveFileTest.RGFlt_MoveFile_OutsideToOutside(Path.GetDirectoryName(this.Enlistment.RepoRoot), this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_MoveFile_LongFileName()
        {
            RGFlt_MoveFileTest.RGFlt_MoveFile_LongFileName(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_MoveFolder_NoneToNone()
        {
            RGFlt_MoveFolderTest.RGFlt_MoveFolder_NoneToNone(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_MoveFolder_VirtualToNone()
        {
            RGFlt_MoveFolderTest.RGFlt_MoveFolder_VirtualToNone(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_MoveFolder_PartialToNone()
        {
            RGFlt_MoveFolderTest.RGFlt_MoveFolder_PartialToNone(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_MoveFolder_VirtualToVirtual()
        {
            RGFlt_MoveFolderTest.RGFlt_MoveFolder_VirtualToVirtual(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_MoveFolder_VirtualToPartial()
        {
            RGFlt_MoveFolderTest.RGFlt_MoveFolder_VirtualToPartial(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_MoveFolder_OutsideToNone()
        {
            RGFlt_MoveFolderTest.RGFlt_MoveFolder_OutsideToNone(Path.GetDirectoryName(this.Enlistment.RepoRoot), this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_MoveFolder_OutsideToVirtual()
        {
            RGFlt_MoveFolderTest.RGFlt_MoveFolder_OutsideToVirtual(Path.GetDirectoryName(this.Enlistment.RepoRoot), this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_MoveFolder_NoneToOutside()
        {
            RGFlt_MoveFolderTest.RGFlt_MoveFolder_NoneToOutside(Path.GetDirectoryName(this.Enlistment.RepoRoot), this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_MoveFolder_VirtualToOutside()
        {
            RGFlt_MoveFolderTest.RGFlt_MoveFolder_VirtualToOutside(Path.GetDirectoryName(this.Enlistment.RepoRoot), this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_MoveFolder_OutsideToOutside()
        {
            RGFlt_MoveFolderTest.RGFlt_MoveFolder_OutsideToOutside(Path.GetDirectoryName(this.Enlistment.RepoRoot), this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_OpenForReadsSameTime()
        {
            RGFlt_MultiThreadTest.RGFlt_OpenForReadsSameTime(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_OpenMultipleFilesForReadsSameTime()
        {
            RGFlt_MultiThreadTest.RGFlt_OpenMultipleFilesForReadsSameTime(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_OpenForWritesSameTime()
        {
            RGFlt_MultiThreadTest.RGFlt_OpenForWritesSameTime(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_SetLink_ToVirtualFile()
        {
            RGFlt_SetLinkTest.RGFlt_SetLink_ToVirtualFile(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_SetLink_ToPlaceHolder()
        {
            RGFlt_SetLinkTest.RGFlt_SetLink_ToPlaceHolder(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_SetLink_ToFullFile()
        {
            RGFlt_SetLinkTest.RGFlt_SetLink_ToFullFile(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_SetLink_ToNonExistFileWillFail()
        {
            RGFlt_SetLinkTest.RGFlt_SetLink_ToNonExistFileWillFail(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_SetLink_NameAlreadyExistWillFail()
        {
            RGFlt_SetLinkTest.RGFlt_SetLink_NameAlreadyExistWillFail(this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_SetLink_FromOutside()
        {
            RGFlt_SetLinkTest.RGFlt_SetLink_FromOutside(Path.GetDirectoryName(this.Enlistment.RepoRoot), this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        [TestCase]
        public void Native_RGFlt_SetLink_ToOutside()
        {
            RGFlt_SetLinkTest.RGFlt_SetLink_ToOutside(Path.GetDirectoryName(this.Enlistment.RepoRoot), this.Enlistment.RepoRoot).ShouldEqual(true);
        }

        private string CreateFileInPhysicalPath(FileSystemRunner fileSystem)
        {
            string existingFilePhysicalPath = Path.Combine(this.Enlistment.DotRGFSRoot, "existingFileTest.txt");
            fileSystem.WriteAllText(existingFilePhysicalPath, "File for testing");
            existingFilePhysicalPath.ShouldBeAFile(fileSystem);
            return existingFilePhysicalPath;
        }

        private class NativeTests
        {
            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool ReadAndWriteSeparateHandles(string fileVirtualPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool ReadAndWriteSameHandle(string fileVirtualPath, bool synchronousIO);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool ReadAndWriteRepeatedly(string fileVirtualPath, bool synchronousIO);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RemoveReadOnlyAttribute(string fileVirtualPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool CannotWriteToReadOnlyFile(string fileVirtualPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool EnumerationErrorsMatchNTFSForNonExistentFolder(string nonExistentVirtualPath, string nonExistentPhysicalPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool EnumerationErrorsMatchNTFSForEmptyFolder(string emptyFolderVirtualPath, string emptyFolderPhysicalPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool CanDeleteEmptyFolderWithFileDispositionOnClose(string emptyFolderPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool QueryDirectoryFileRestartScanResetsFilter(string folderPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool ErrorWhenPathTreatsFileAsFolderMatchesNTFS(string filePath, string fileNTFSPath, int creationDisposition);
        }

        private class NativeTrailingSlashTests
        {
            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool EnumerateWithTrailingSlashMatchesWithoutSlashAfterDelete(string virtualRootPath);
        }

        private class RGFlt_BugRegressionTest
        {
            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_ModifyFileInScratchAndDir(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_RMDIRTest1(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_RMDIRTest2(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_RMDIRTest3(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_RMDIRTest4(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_RMDIRTest5(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_DeepNonExistFileUnderPartial(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_SupersededReparsePoint(string virtualRootPath);
        }

        private class RGFlt_DeleteFileTest
        {
            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_DeleteVirtualFile_SetDisposition(string enumFolderSmallBufferPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_DeleteVirtualFile_DeleteOnClose(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_DeletePlaceholder_SetDisposition(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_DeletePlaceholder_DeleteOnClose(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_DeleteFullFile_SetDisposition(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_DeleteFullFile_DeleteOnClose(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_DeleteLocalFile_SetDisposition(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_DeleteLocalFile_DeleteOnClose(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_DeleteNotExistFile_SetDisposition(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_DeleteNotExistFile_DeleteOnClose(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_DeleteNonRootVirtualFile_SetDisposition(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_DeleteNonRootVirtualFile_DeleteOnClose(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_DeleteFileOutsideVRoot_SetDisposition(string pathOutsideRepo);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_DeleteFileOutsideVRoot_DeleteOnClose(string pathOutsideRepo);
        }

        private class RGFlt_DeleteFolderTest
        {
            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_DeleteVirtualNonEmptyFolder_SetDisposition(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_DeleteVirtualNonEmptyFolder_DeleteOnClose(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_DeletePlaceholderNonEmptyFolder_SetDisposition(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_DeletePlaceholderNonEmptyFolder_DeleteOnClose(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_DeleteLocalEmptyFolder_SetDisposition(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_DeleteLocalEmptyFolder_DeleteOnClose(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_DeleteNonRootVirtualFolder_SetDisposition(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_DeleteNonRootVirtualFolder_DeleteOnClose(string virtualRootPath);
        }

        private class RGFlt_DirEnumTest
        {
            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_EnumEmptyFolder(string emptyFolderPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_EnumFolderWithOneFileInPackage(string enumFolderWithOneFileInRepoPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_EnumFolderWithOneFileInBoth(string enumFolderWithOneFileInRepoBeforeScratchPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_EnumFolderWithOneFileInBoth1(string enumFolderWithOneFileInRepoAfterScratchPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_EnumFolderDeleteExistingFile(string enumFolderDeleteExistingFilePath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_EnumFolderSmallBuffer(string enumFolderSmallBufferPath);
        }

        private class RGFlt_FileAttributeTest
        {
            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_ModifyFileInScratchAndCheckLastWriteTime(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_FileSize(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_ModifyFileInScratchAndCheckFileSize(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_FileAttributes(string virtualRootPath);
        }

        private class RGFlt_FileEATest
        {
            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_OneEAAttributeWillPass(string virtualRootPath);
        }

        private class RGFlt_FileOperationTest
        {
            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_OpenRootFolder(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_WriteAndVerify(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_DeleteExistingFile(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_OpenNonExistingFile(string virtualRootPath);
        }

        private class RGFlt_MoveFileTest
        {
            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_MoveFile_NoneToNone(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_MoveFile_VirtualToNone(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_MoveFile_PartialToNone(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_MoveFile_FullToNone(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_MoveFile_LocalToNone(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_MoveFile_VirtualToVirtual(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_MoveFile_VirtualToVirtualFileNameChanged(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_MoveFile_VirtualToPartial(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_MoveFile_PartialToPartial(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_MoveFile_LocalToVirtual(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_MoveFile_VirtualToVirtualIntermidiateDirNotExist(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_MoveFile_VirtualToNoneIntermidiateDirNotExist(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_MoveFile_OutsideToNone(string pathOutsideRepo, string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_MoveFile_OutsideToVirtual(string pathOutsideRepo, string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_MoveFile_OutsideToPartial(string pathOutsideRepo, string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_MoveFile_NoneToOutside(string pathOutsideRepo, string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_MoveFile_VirtualToOutside(string pathOutsideRepo, string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_MoveFile_PartialToOutside(string pathOutsideRepo, string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_MoveFile_OutsideToOutside(string pathOutsideRepo, string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_MoveFile_LongFileName(string virtualRootPath);
        }

        private class RGFlt_MoveFolderTest
        {
            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_MoveFolder_NoneToNone(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_MoveFolder_VirtualToNone(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_MoveFolder_PartialToNone(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_MoveFolder_VirtualToVirtual(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_MoveFolder_VirtualToPartial(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_MoveFolder_OutsideToNone(string pathOutsideRepo, string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_MoveFolder_OutsideToVirtual(string pathOutsideRepo, string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_MoveFolder_NoneToOutside(string pathOutsideRepo, string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_MoveFolder_VirtualToOutside(string pathOutsideRepo, string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_MoveFolder_OutsideToOutside(string pathOutsideRepo, string virtualRootPath);
        }

        private class RGFlt_MultiThreadTest
        {
            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_OpenForReadsSameTime(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_OpenForWritesSameTime(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_OpenMultipleFilesForReadsSameTime(string virtualRootPath);
        }

        private class RGFlt_SetLinkTest
        {
            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_SetLink_ToVirtualFile(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_SetLink_ToPlaceHolder(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_SetLink_ToFullFile(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_SetLink_ToNonExistFileWillFail(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_SetLink_NameAlreadyExistWillFail(string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_SetLink_FromOutside(string pathOutsideRepo, string virtualRootPath);

            [DllImport("RGFS.NativeTests.dll")]
            public static extern bool RGFlt_SetLink_ToOutside(string pathOutsideRepo, string virtualRootPath);
        }

        private class FileRunnersAndFolders
        {
            public const string TestFolders = "Folders";
            public const string TestRunners = "Runners";
            public const string TestCanDeleteFilesWhileTheyAreOpenRunners = "CanDeleteFilesWhileTheyAreOpenRunners";
            public const string DotGitFolder = ".git";

            private static object[] allFolders =
            {
                new object[] { string.Empty },
                new object[] { DotGitFolder },
            };

            public static object[] Runners
            {
                get
                {
                    List<object[]> runnersAndParentFolders = new List<object[]>();
                    foreach (object[] runner in FileSystemRunner.Runners.ToList())
                    {
                        runnersAndParentFolders.Add(new object[] { runner.ToList().First(), string.Empty });
                        runnersAndParentFolders.Add(new object[] { runner.ToList().First(), DotGitFolder });
                    }

                    return runnersAndParentFolders.ToArray();
                }
            }

            public static object[] CanDeleteFilesWhileTheyAreOpenRunners
            {
                get
                {
                    // Don't use the BashRunner for the CanDeleteFilesWhileTheyAreOpen test as bash.exe (rm command) moves
                    // the file to the recycle bin rather than deleting it if the file that is getting removed is currently open.
                    List<object[]> runnersAndParentFolders = new List<object[]>();
                    foreach (object[] runner in FileSystemRunner.Runners.ToList())
                    {
                        if (!(runner.ToList().First() is BashRunner))
                        {
                            runnersAndParentFolders.Add(new object[] { runner.ToList().First(), string.Empty });
                            runnersAndParentFolders.Add(new object[] { runner.ToList().First(), DotGitFolder });
                        }
                    }

                    return runnersAndParentFolders.ToArray();
                }
            }

            public static object[] Folders
            {
                get
                {
                    return allFolders;
                }
            }

            public static void ShouldNotExistOnDisk(RGFSFunctionalTestEnlistment enlistment, FileSystemRunner fileSystem, string filename, string parentFolder)
            {
                enlistment.GetVirtualPathTo(filename).ShouldNotExistOnDisk(fileSystem);
            }
        }

        private class DeleteDotGitTestsRunners
        {
            public const string TestRunners = "Runners";

            public static object[] Runners
            {
                get
                {
                    // Don't use the BashRunner or SystemIORunner for the CanDeleteFilesWhileTheyAreOpen test as they start
                    // recursively deleting inside of the directory junction (before attempting to delete the junction itself)
                    List<object[]> runners = new List<object[]>();
                    foreach (object[] runner in FileSystemRunner.Runners.ToList())
                    {
                        if (!(runner.ToList().First() is BashRunner) && !(runner.ToList().First() is SystemIORunner))
                        {
                            runners.Add(new object[] { runner.ToList().First() });
                        }
                    }

                    return runners.ToArray();
                }
            }
        }
    }
}
