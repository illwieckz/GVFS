using RGFS.Common;
using RGFS.RGFlt;
using RGFS.Tests.Should;
using RGFS.UnitTests.Category;
using RGFS.UnitTests.Mock.Common;
using RGFS.UnitTests.Mock.Git;
using RGFS.UnitTests.Mock.RgFlt;
using RGFS.UnitTests.Mock.RGFS.RgFlt;
using RGFS.UnitTests.Mock.RGFS.RgFlt.DotGit;
using RGFS.UnitTests.Virtual;
using RgLib;
using NUnit.Framework;
using System;
using System.Threading.Tasks;

namespace RGFS.UnitTests.RGFlt.DotGit
{
    public class RGFltCallbacksTests : TestsWithCommonRepo
    {
        [TestCase]
        public void CannotDeleteIndexOrPacks()
        {
            RGFltCallbacks.DoesPathAllowDelete(string.Empty).ShouldEqual(true);

            RGFltCallbacks.DoesPathAllowDelete(@".git\index").ShouldEqual(false);
            RGFltCallbacks.DoesPathAllowDelete(@".git\INDEX").ShouldEqual(false);

            RGFltCallbacks.DoesPathAllowDelete(@".git\index.lock").ShouldEqual(true);
            RGFltCallbacks.DoesPathAllowDelete(@".git\INDEX.lock").ShouldEqual(true);
            RGFltCallbacks.DoesPathAllowDelete(@".git\objects\pack").ShouldEqual(true);
            RGFltCallbacks.DoesPathAllowDelete(@".git\objects\pack-temp").ShouldEqual(true);
            RGFltCallbacks.DoesPathAllowDelete(@".git\objects\pack\pack-1e88df2a4e234c82858cfe182070645fb96d6131.pack").ShouldEqual(true);
            RGFltCallbacks.DoesPathAllowDelete(@".git\objects\pack\pack-1e88df2a4e234c82858cfe182070645fb96d6131.idx").ShouldEqual(true);
        }

        [TestCase]
        public void OnStartDirectoryEnumerationReturnsPendingWhenResultsNotInMemory()
        {
            using (MockVirtualizationInstance mockRgFlt = new MockVirtualizationInstance())
            using (MockGitIndexProjection gitIndexProjection = new MockGitIndexProjection(new[] { "test.txt" }))
            {
                RGFltCallbacks callbacks = new RGFltCallbacks(
                    this.Repo.Context,
                    this.Repo.GitObjects,
                    RepoMetadata.Instance,
                    blobSizes: null,
                    rgflt: mockRgFlt,
                    gitIndexProjection: gitIndexProjection,
                    reliableBackgroundOperations: new MockReliableBackgroundOperations());

                string error;
                callbacks.TryStart(out error).ShouldEqual(true);

                Guid enumerationGuid = Guid.NewGuid();
                gitIndexProjection.EnumerationInMemory = false;
                mockRgFlt.OnStartDirectoryEnumeration(1, enumerationGuid, "test").ShouldEqual(NtStatus.Pending);
                mockRgFlt.WaitForCompletionStatus().ShouldEqual(NtStatus.Success);
                mockRgFlt.OnEndDirectoryEnumeration(enumerationGuid).ShouldEqual(NtStatus.Success);
            }
        }

        [TestCase]
        public void OnStartDirectoryEnumerationReturnsSuccessWhenResultsInMemory()
        {
            using (MockVirtualizationInstance mockRgFlt = new MockVirtualizationInstance())
            using (MockGitIndexProjection gitIndexProjection = new MockGitIndexProjection(new[] { "test" }))
            {
                RGFltCallbacks callbacks = new RGFltCallbacks(
                    this.Repo.Context,
                    this.Repo.GitObjects,
                    RepoMetadata.Instance,
                    blobSizes: null,
                    rgflt: mockRgFlt,
                    gitIndexProjection: gitIndexProjection,
                    reliableBackgroundOperations: new MockReliableBackgroundOperations());

                string error;
                callbacks.TryStart(out error).ShouldEqual(true);

                Guid enumerationGuid = Guid.NewGuid();
                gitIndexProjection.EnumerationInMemory = true;
                mockRgFlt.OnStartDirectoryEnumeration(1, enumerationGuid, "test").ShouldEqual(NtStatus.Success);
                mockRgFlt.OnEndDirectoryEnumeration(enumerationGuid).ShouldEqual(NtStatus.Success);
            }
        }

        [TestCase]
        public void GetPlaceholderInformationHandlerPathNotProjected()
        {
            using (MockVirtualizationInstance mockRgFlt = new MockVirtualizationInstance())
            using (MockGitIndexProjection gitIndexProjection = new MockGitIndexProjection(new[] { "test.txt" }))
            {
                RGFltCallbacks callbacks = new RGFltCallbacks(
                    this.Repo.Context,
                    this.Repo.GitObjects,
                    RepoMetadata.Instance,
                    blobSizes: null,
                    rgflt: mockRgFlt,
                    gitIndexProjection: gitIndexProjection,
                    reliableBackgroundOperations: new MockReliableBackgroundOperations());

                string error;
                callbacks.TryStart(out error).ShouldEqual(true);

                mockRgFlt.OnGetPlaceholderInformation(1, "doesNotExist", 0, 0, 0, 0, 1, "UnitTests").ShouldEqual(NtStatus.ObjectNameNotFound);
            }
        }

        [TestCase]
        public void GetPlaceholderInformationHandlerPathProjected()
        {
            using (MockVirtualizationInstance mockRgFlt = new MockVirtualizationInstance())
            using (MockGitIndexProjection gitIndexProjection = new MockGitIndexProjection(new[] { "test.txt" }))
            {
                RGFltCallbacks callbacks = new RGFltCallbacks(
                    this.Repo.Context,
                    this.Repo.GitObjects,
                    RepoMetadata.Instance,
                    blobSizes: null,
                    rgflt: mockRgFlt,
                    gitIndexProjection: gitIndexProjection,
                    reliableBackgroundOperations: new MockReliableBackgroundOperations());

                string error;
                callbacks.TryStart(out error).ShouldEqual(true);

                mockRgFlt.OnGetPlaceholderInformation(1, "test.txt", 0, 0, 0, 0, 1, "UnitTests").ShouldEqual(NtStatus.Pending);
                mockRgFlt.WaitForCompletionStatus().ShouldEqual(NtStatus.Success);
                mockRgFlt.CreatedPlaceholders.ShouldContain(entry => entry == "test.txt");
                gitIndexProjection.PlaceholdersCreated.ShouldContain(entry => entry == "test.txt");
            }
        }

        [TestCase]
        public void GetPlaceholderInformationHandlerCancelledBeforeSchedulingAsync()
        {
            using (MockVirtualizationInstance mockRgFlt = new MockVirtualizationInstance())
            using (MockGitIndexProjection gitIndexProjection = new MockGitIndexProjection(new[] { "test.txt" }))
            {
                RGFltCallbacks callbacks = new RGFltCallbacks(
                    this.Repo.Context,
                    this.Repo.GitObjects,
                    RepoMetadata.Instance,
                    blobSizes: null,
                    rgflt: mockRgFlt,
                    gitIndexProjection: gitIndexProjection,
                    reliableBackgroundOperations: new MockReliableBackgroundOperations());

                string error;
                callbacks.TryStart(out error).ShouldEqual(true);

                gitIndexProjection.BlockIsPathProjected(willWaitForRequest: true);

                Task.Run(() =>
                {
                    // Wait for OnGetPlaceholderInformation to call IsPathProjected and then while it's blocked there
                    // call OnCancelCommand
                    gitIndexProjection.WaitForIsPathProjected();
                    mockRgFlt.OnCancelCommand(1);
                    gitIndexProjection.UnblockIsPathProjected();
                });

                mockRgFlt.OnGetPlaceholderInformation(1, "test.txt", 0, 0, 0, 0, 1, "UnitTests").ShouldEqual(NtStatus.Pending);

                // Cancelling before GetPlaceholderInformation has registered the command results in placeholders being created
                mockRgFlt.WaitForPlaceholderCreate();
                gitIndexProjection.WaitForPlaceholderCreate();
                mockRgFlt.CreatedPlaceholders.ShouldContain(entry => entry == "test.txt");
                gitIndexProjection.PlaceholdersCreated.ShouldContain(entry => entry == "test.txt");
            }
        }

        [TestCase]
        public void GetPlaceholderInformationHandlerCancelledDuringAsyncCallback()
        {
            using (MockVirtualizationInstance mockRgFlt = new MockVirtualizationInstance())
            using (MockGitIndexProjection gitIndexProjection = new MockGitIndexProjection(new[] { "test.txt" }))
            {
                RGFltCallbacks callbacks = new RGFltCallbacks(
                    this.Repo.Context,
                    this.Repo.GitObjects,
                    RepoMetadata.Instance,
                    blobSizes: null,
                    rgflt: mockRgFlt,
                    gitIndexProjection: gitIndexProjection,
                    reliableBackgroundOperations: new MockReliableBackgroundOperations());

                string error;
                callbacks.TryStart(out error).ShouldEqual(true);

                gitIndexProjection.BlockGetProjectedFileInfo(willWaitForRequest: true);
                mockRgFlt.OnGetPlaceholderInformation(1, "test.txt", 0, 0, 0, 0, 1, "UnitTests").ShouldEqual(NtStatus.Pending);
                gitIndexProjection.WaitForGetProjectedFileInfo();
                mockRgFlt.OnCancelCommand(1);
                gitIndexProjection.UnblockGetProjectedFileInfo();

                // Cancelling in the middle of GetPlaceholderInformation still allows it to create placeholders when the cancellation does not
                // interrupt network requests                
                mockRgFlt.WaitForPlaceholderCreate();
                gitIndexProjection.WaitForPlaceholderCreate();
                mockRgFlt.CreatedPlaceholders.ShouldContain(entry => entry == "test.txt");
                gitIndexProjection.PlaceholdersCreated.ShouldContain(entry => entry == "test.txt");
            }
        }

        [TestCase]
        [Category(CategoryConstants.ExceptionExpected)]
        public void GetPlaceholderInformationHandlerCancelledDuringNetworkRequest()
        {
            using (MockVirtualizationInstance mockRgFlt = new MockVirtualizationInstance())
            using (MockGitIndexProjection gitIndexProjection = new MockGitIndexProjection(new[] { "test.txt" }))
            {
                RGFltCallbacks callbacks = new RGFltCallbacks(
                    this.Repo.Context,
                    this.Repo.GitObjects,
                    RepoMetadata.Instance,
                    blobSizes: null,
                    rgflt: mockRgFlt,
                    gitIndexProjection: gitIndexProjection,
                    reliableBackgroundOperations: new MockReliableBackgroundOperations());

                string error;
                callbacks.TryStart(out error).ShouldEqual(true);

                MockTracer mockTracker = this.Repo.Context.Tracer as MockTracer;
                mockTracker.WaitRelatedEventName = "RGFltGetPlaceholderInformationAsyncHandler_GetProjectedRGFltFileInfoAndShaCancelled";
                gitIndexProjection.ThrowOperationCanceledExceptionOnProjectionRequest = true;
                mockRgFlt.OnGetPlaceholderInformation(1, "test.txt", 0, 0, 0, 0, 1, "UnitTests").ShouldEqual(NtStatus.Pending);

                // Cancelling in the middle of GetPlaceholderInformation in the middle of a network request should not result in placeholder
                // getting created
                mockTracker.WaitForRelatedEvent();
                mockRgFlt.CreatedPlaceholders.ShouldNotContain(entry => entry == "test.txt");
                gitIndexProjection.PlaceholdersCreated.ShouldNotContain(entry => entry == "test.txt");
            }
        }

        [TestCase]
        public void OnGetFileStreamReturnsInvalidParameterWhenOffsetNonZero()
        {
            using (MockVirtualizationInstance mockRgFlt = new MockVirtualizationInstance())
            using (MockGitIndexProjection gitIndexProjection = new MockGitIndexProjection(new[] { "test.txt" }))
            {
                RGFltCallbacks callbacks = new RGFltCallbacks(
                    this.Repo.Context,
                    this.Repo.GitObjects,
                    RepoMetadata.Instance,
                    blobSizes: null,
                    rgflt: mockRgFlt,
                    gitIndexProjection: gitIndexProjection,
                    reliableBackgroundOperations: new MockReliableBackgroundOperations());

                string error;
                callbacks.TryStart(out error).ShouldEqual(true);

                Guid enumerationGuid = Guid.NewGuid();

                byte[] contentId = RGFltCallbacks.ConvertShaToContentId("0123456789012345678901234567890123456789");
                byte[] epochId = RGFltCallbacks.GetEpochId();

                mockRgFlt.OnGetFileStream(
                    commandId: 1,
                    relativePath: "test.txt",
                    byteOffset: 10,
                    length: 100,
                    streamGuid: Guid.NewGuid(),
                    contentId: contentId,
                    epochId: epochId,
                    triggeringProcessId: 2,
                    triggeringProcessImageFileName: "UnitTest").ShouldEqual(NtStatus.InvalidParameter);
            }
        }

        [TestCase]
        public void OnGetFileStreamReturnsInternalErrorWhenPlaceholderVersionDoesNotMatchExpected()
        {
            using (MockVirtualizationInstance mockRgFlt = new MockVirtualizationInstance())
            using (MockGitIndexProjection gitIndexProjection = new MockGitIndexProjection(new[] { "test.txt" }))
            {
                RGFltCallbacks callbacks = new RGFltCallbacks(
                    this.Repo.Context,
                    this.Repo.GitObjects,
                    RepoMetadata.Instance,
                    blobSizes: null,
                    rgflt: mockRgFlt,
                    gitIndexProjection: gitIndexProjection,
                    reliableBackgroundOperations: new MockReliableBackgroundOperations());

                string error;
                callbacks.TryStart(out error).ShouldEqual(true);

                Guid enumerationGuid = Guid.NewGuid();

                byte[] contentId = RGFltCallbacks.ConvertShaToContentId("0123456789012345678901234567890123456789");
                byte[] epochId = new byte[] { RGFltCallbacks.PlaceholderVersion + 1 };

                mockRgFlt.OnGetFileStream(
                    commandId: 1,
                    relativePath: "test.txt",
                    byteOffset: 0,
                    length: 100,
                    streamGuid: Guid.NewGuid(),
                    contentId: contentId,
                    epochId: epochId,
                    triggeringProcessId: 2,
                    triggeringProcessImageFileName: "UnitTest").ShouldEqual(NtStatus.InternalError);
            }
        }

        [TestCase]
        public void OnGetFileStreamReturnsPendingAndCompletesWithSuccessWhenNoFailures()
        {
            using (MockVirtualizationInstance mockRgFlt = new MockVirtualizationInstance())
            using (MockGitIndexProjection gitIndexProjection = new MockGitIndexProjection(new[] { "test.txt" }))
            {
                RGFltCallbacks callbacks = new RGFltCallbacks(
                    this.Repo.Context,
                    this.Repo.GitObjects,
                    RepoMetadata.Instance,
                    blobSizes: null,
                    rgflt: mockRgFlt,
                    gitIndexProjection: gitIndexProjection,
                    reliableBackgroundOperations: new MockReliableBackgroundOperations());

                string error;
                callbacks.TryStart(out error).ShouldEqual(true);

                Guid enumerationGuid = Guid.NewGuid();

                byte[] contentId = RGFltCallbacks.ConvertShaToContentId("0123456789012345678901234567890123456789");
                byte[] epochId = RGFltCallbacks.GetEpochId();

                uint fileLength = 100;
                MockRGFSGitObjects mockRGFSGitObjects = this.Repo.GitObjects as MockRGFSGitObjects;
                mockRGFSGitObjects.FileLength = fileLength;
                mockRgFlt.WriteFileReturnStatus = NtStatus.Success;

                mockRgFlt.OnGetFileStream(
                    commandId: 1,
                    relativePath: "test.txt",
                    byteOffset: 0,
                    length: fileLength,
                    streamGuid: Guid.NewGuid(),
                    contentId: contentId,
                    epochId: epochId,
                    triggeringProcessId: 2,
                    triggeringProcessImageFileName: "UnitTest").ShouldEqual(NtStatus.Pending);

                mockRgFlt.WaitForCompletionStatus().ShouldEqual(NtStatus.Success);
            }
        }

        [TestCase]
        [Category(CategoryConstants.ExceptionExpected)]
        public void OnGetFileStreamHandlesTryCopyBlobContentStreamThrowingOperationCanceled()
        {
            using (MockVirtualizationInstance mockRgFlt = new MockVirtualizationInstance())
            using (MockGitIndexProjection gitIndexProjection = new MockGitIndexProjection(new[] { "test.txt" }))
            {
                RGFltCallbacks callbacks = new RGFltCallbacks(
                    this.Repo.Context,
                    this.Repo.GitObjects,
                    RepoMetadata.Instance,
                    blobSizes: null,
                    rgflt: mockRgFlt,
                    gitIndexProjection: gitIndexProjection,
                    reliableBackgroundOperations: new MockReliableBackgroundOperations());

                string error;
                callbacks.TryStart(out error).ShouldEqual(true);

                Guid enumerationGuid = Guid.NewGuid();

                byte[] contentId = RGFltCallbacks.ConvertShaToContentId("0123456789012345678901234567890123456789");
                byte[] epochId = RGFltCallbacks.GetEpochId();

                MockRGFSGitObjects mockRGFSGitObjects = this.Repo.GitObjects as MockRGFSGitObjects;

                MockTracer mockTracker = this.Repo.Context.Tracer as MockTracer;
                mockTracker.WaitRelatedEventName = "RGFltGetFileStreamHandlerAsyncHandler_OperationCancelled";
                mockRGFSGitObjects.CancelTryCopyBlobContentStream = true;

                mockRgFlt.OnGetFileStream(
                    commandId: 1,
                    relativePath: "test.txt",
                    byteOffset: 0,
                    length: 100,
                    streamGuid: Guid.NewGuid(),
                    contentId: contentId,
                    epochId: epochId,
                    triggeringProcessId: 2,
                    triggeringProcessImageFileName: "UnitTest").ShouldEqual(NtStatus.Pending);

                mockTracker.WaitForRelatedEvent();
            }
        }

        [TestCase]
        [Category(CategoryConstants.ExceptionExpected)]
        public void OnGetFileStreamHandlesCancellationDuringWriteAction()
        {
            using (MockVirtualizationInstance mockRgFlt = new MockVirtualizationInstance())
            using (MockGitIndexProjection gitIndexProjection = new MockGitIndexProjection(new[] { "test.txt" }))
            {
                RGFltCallbacks callbacks = new RGFltCallbacks(
                    this.Repo.Context,
                    this.Repo.GitObjects,
                    RepoMetadata.Instance,
                    blobSizes: null,
                    rgflt: mockRgFlt,
                    gitIndexProjection: gitIndexProjection,
                    reliableBackgroundOperations: new MockReliableBackgroundOperations());

                string error;
                callbacks.TryStart(out error).ShouldEqual(true);

                Guid enumerationGuid = Guid.NewGuid();

                byte[] contentId = RGFltCallbacks.ConvertShaToContentId("0123456789012345678901234567890123456789");
                byte[] epochId = RGFltCallbacks.GetEpochId();

                uint fileLength = 100;
                MockRGFSGitObjects mockRGFSGitObjects = this.Repo.GitObjects as MockRGFSGitObjects;
                mockRGFSGitObjects.FileLength = fileLength;

                MockTracer mockTracker = this.Repo.Context.Tracer as MockTracer;
                mockTracker.WaitRelatedEventName = "RGFltGetFileStreamHandlerAsyncHandler_OperationCancelled";

                mockRgFlt.BlockCreateWriteBuffer(willWaitForRequest: true);
                mockRgFlt.OnGetFileStream(
                    commandId: 1,
                    relativePath: "test.txt",
                    byteOffset: 0,
                    length: fileLength,
                    streamGuid: Guid.NewGuid(),
                    contentId: contentId,
                    epochId: epochId,
                    triggeringProcessId: 2,
                    triggeringProcessImageFileName: "UnitTest").ShouldEqual(NtStatus.Pending);

                mockRgFlt.WaitForCreateWriteBuffer();
                mockRgFlt.OnCancelCommand(1);
                mockRgFlt.UnblockCreateWriteBuffer();
                mockTracker.WaitForRelatedEvent();
            }
        }

        [TestCase]
        [Category(CategoryConstants.ExceptionExpected)]
        public void OnGetFileStreamHandlesGvWriteFailure()
        {
            using (MockVirtualizationInstance mockRgFlt = new MockVirtualizationInstance())
            using (MockGitIndexProjection gitIndexProjection = new MockGitIndexProjection(new[] { "test.txt" }))
            {
                RGFltCallbacks callbacks = new RGFltCallbacks(
                    this.Repo.Context,
                    this.Repo.GitObjects,
                    RepoMetadata.Instance,
                    blobSizes: null,
                    rgflt: mockRgFlt,
                    gitIndexProjection: gitIndexProjection,
                    reliableBackgroundOperations: new MockReliableBackgroundOperations());

                string error;
                callbacks.TryStart(out error).ShouldEqual(true);

                Guid enumerationGuid = Guid.NewGuid();

                byte[] contentId = RGFltCallbacks.ConvertShaToContentId("0123456789012345678901234567890123456789");
                byte[] epochId = RGFltCallbacks.GetEpochId();

                uint fileLength = 100;
                MockRGFSGitObjects mockRGFSGitObjects = this.Repo.GitObjects as MockRGFSGitObjects;
                mockRGFSGitObjects.FileLength = fileLength;

                MockTracer mockTracker = this.Repo.Context.Tracer as MockTracer;
                mockTracker.WaitRelatedEventName = "RGFltGetFileStreamHandlerAsyncHandler_OperationCancelled";

                mockRgFlt.WriteFileReturnStatus = NtStatus.InternalError;
                mockRgFlt.OnGetFileStream(
                    commandId: 1,
                    relativePath: "test.txt",
                    byteOffset: 0,
                    length: fileLength,
                    streamGuid: Guid.NewGuid(),
                    contentId: contentId,
                    epochId: epochId,
                    triggeringProcessId: 2,
                    triggeringProcessImageFileName: "UnitTest").ShouldEqual(NtStatus.Pending);

                mockRgFlt.WaitForCompletionStatus().ShouldEqual(mockRgFlt.WriteFileReturnStatus);
            }
        }
    }
}