﻿using RGFS.Common;
using RGFS.Common.FileSystem;
using RGFS.Common.Git;
using RGFS.Common.Http;
using RGFS.Common.NamedPipes;
using RGFS.Common.Tracing;
using RGFS.RGFlt;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

namespace RGFS.Mount
{
    public class InProcessMount
    {
        // Tests show that 250 is the max supported pipe name length
        private const int MaxPipeNameLength = 250;
        private const int MutexMaxWaitTimeMS = 500;

        private readonly bool showDebugWindow;

        private RGFltCallbacks rgfltCallbacks;
        private RGFSEnlistment enlistment;
        private ITracer tracer;

        private CacheServerInfo cacheServer;
        private RetryConfig retryConfig;

        private RGFSGitObjects gitObjects;
        private RGFSLock rgfsLock;

        private MountState currentState;
        private HeartbeatThread heartbeat;
        private ManualResetEvent unmountEvent;

        private List<SafeFileHandle> folderLockHandles;
        
        public InProcessMount(ITracer tracer, RGFSEnlistment enlistment, CacheServerInfo cacheServer, RetryConfig retryConfig, bool showDebugWindow)
        {
            this.tracer = tracer;
            this.retryConfig = retryConfig;
            this.cacheServer = cacheServer;
            this.enlistment = enlistment;
            this.showDebugWindow = showDebugWindow;
            this.unmountEvent = new ManualResetEvent(false);
        }

        private enum MountState
        {
            Invalid = 0,

            Mounting,
            Ready,
            Unmounting,
            MountFailed
        }

        public void Mount(EventLevel verbosity, Keywords keywords)
        {
            this.currentState = MountState.Mounting;

            // We must initialize repo metadata before starting the pipe server so it 
            // can immediately handle status requests
            string error;
            if (!RepoMetadata.TryInitialize(this.tracer, this.enlistment.DotRGFSRoot, out error))
            {
                this.FailMountAndExit("Failed to load repo metadata: {0}", error);
            }

            using (NamedPipeServer pipeServer = this.StartNamedPipe())
            {
                RGFSContext context = this.CreateContext();

                if (context.Unattended)
                {
                    this.tracer.RelatedEvent(EventLevel.Critical, RGFSConstants.UnattendedEnvironmentVariable, null);
                }

                this.ValidateMountPoints();
                this.UpdateHooks();
                this.SetVisualStudioRegistryKey();

                this.rgfsLock = context.Repository.RGFSLock;
                this.MountAndStartWorkingDirectoryCallbacks(context, this.cacheServer);

                Console.Title = "RGFS " + ProcessHelper.GetCurrentProcessVersion() + " - " + this.enlistment.EnlistmentRoot;

                this.tracer.RelatedEvent(
                    EventLevel.Critical,
                    "Mount",
                    new EventMetadata
                    {
                        // Use TracingConstants.MessageKey.InfoMessage rather than TracingConstants.MessageKey.CriticalMessage
                        // as this message should not appear as an error
                        { TracingConstants.MessageKey.InfoMessage, "Virtual repo is ready" },
                    });

                this.currentState = MountState.Ready;

                this.unmountEvent.WaitOne();
            }
        }

        private RGFSContext CreateContext()
        {
            PhysicalFileSystem fileSystem = new PhysicalFileSystem();
            GitRepo gitRepo = this.CreateOrReportAndExit(
                () => new GitRepo(
                    this.tracer,
                    this.enlistment,
                    fileSystem),
                "Failed to read git repo");
            return new RGFSContext(this.tracer, fileSystem, gitRepo, this.enlistment);
        }

        private void ValidateMountPoints()
        {
            DirectoryInfo workingDirectoryRootInfo = new DirectoryInfo(this.enlistment.WorkingDirectoryRoot);
            if (!workingDirectoryRootInfo.Exists)
            {
                this.FailMountAndExit("Failed to initialize file system callbacks. Directory \"{0}\" must exist.", this.enlistment.WorkingDirectoryRoot);
            }

            string dotGitPath = Path.Combine(this.enlistment.WorkingDirectoryRoot, RGFSConstants.DotGit.Root);
            DirectoryInfo dotGitPathInfo = new DirectoryInfo(dotGitPath);
            if (!dotGitPathInfo.Exists)
            {
                this.FailMountAndExit("Failed to mount. Directory \"{0}\" must exist.", dotGitPathInfo);
            }
        }

        private void UpdateHooks()
        {
            bool copyReadObjectHook = false;
            string enlistmentReadObjectHookPath = Path.Combine(this.enlistment.WorkingDirectoryRoot, RGFSConstants.DotGit.Hooks.ReadObjectPath + ".exe");
            string installedReadObjectHookPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), RGFSConstants.RGFSReadObjectHookExecutableName);

            if (!File.Exists(installedReadObjectHookPath))
            {
                this.FailMountAndExit(RGFSConstants.RGFSReadObjectHookExecutableName + " cannot be found at {0}", installedReadObjectHookPath);
            }

            if (!File.Exists(enlistmentReadObjectHookPath))
            {
                copyReadObjectHook = true;

                EventMetadata metadata = new EventMetadata();
                metadata.Add("Area", "Mount");
                metadata.Add("enlistmentReadObjectHookPath", enlistmentReadObjectHookPath);
                metadata.Add("installedReadObjectHookPath", installedReadObjectHookPath);
                metadata.Add(TracingConstants.MessageKey.WarningMessage, RGFSConstants.DotGit.Hooks.ReadObjectName + " not found in enlistment, copying from installation folder");
                this.tracer.RelatedEvent(EventLevel.Warning, "ReadObjectMissingFromEnlistment", metadata);
            }
            else
            {
                try
                {
                    FileVersionInfo enlistmentVersion = FileVersionInfo.GetVersionInfo(enlistmentReadObjectHookPath);
                    FileVersionInfo installedVersion = FileVersionInfo.GetVersionInfo(installedReadObjectHookPath);
                    copyReadObjectHook = enlistmentVersion.FileVersion != installedVersion.FileVersion;
                }
                catch (Exception e)
                {
                    EventMetadata metadata = new EventMetadata();
                    metadata.Add("Area", "Mount");
                    metadata.Add("enlistmentReadObjectHookPath", enlistmentReadObjectHookPath);
                    metadata.Add("installedReadObjectHookPath", installedReadObjectHookPath);
                    metadata.Add("Exception", e.ToString());
                    this.tracer.RelatedError(metadata, "Failed to compare " + RGFSConstants.DotGit.Hooks.ReadObjectName + " version");
                    this.FailMountAndExit("Error comparing " + RGFSConstants.DotGit.Hooks.ReadObjectName + " versions. " + ConsoleHelper.GetRGFSLogMessage(this.enlistment.EnlistmentRoot));
                }
            }

            if (copyReadObjectHook)
            {
                try
                {
                    File.Copy(installedReadObjectHookPath, enlistmentReadObjectHookPath, overwrite: true);
                }
                catch (Exception e)
                {
                    EventMetadata metadata = new EventMetadata();
                    metadata.Add("Area", "Mount");
                    metadata.Add("enlistmentReadObjectHookPath", enlistmentReadObjectHookPath);
                    metadata.Add("installedReadObjectHookPath", installedReadObjectHookPath);
                    metadata.Add("Exception", e.ToString());
                    this.tracer.RelatedError(metadata, "Failed to copy " + RGFSConstants.DotGit.Hooks.ReadObjectName + " to enlistment");
                    this.FailMountAndExit("Error copying " + RGFSConstants.DotGit.Hooks.ReadObjectName + " to enlistment. " + ConsoleHelper.GetRGFSLogMessage(this.enlistment.EnlistmentRoot));
                }
            }
        }

        private void SetVisualStudioRegistryKey()
        {
            const string GitBinPathEnd = "\\cmd\\git.exe";
            const string GitVSRegistryKeyName = "HKEY_CURRENT_USER\\Software\\Microsoft\\VSCommon\\15.0\\TeamFoundation\\GitSourceControl";
            const string GitVSRegistryValueName = "GitPath";

            if (!this.enlistment.GitBinPath.EndsWith(GitBinPathEnd))
            {
                this.tracer.RelatedWarning(
                    "Unable to configure Visual Studio’s GitSourceControl regkey because invalid git.exe path found: " + this.enlistment.GitBinPath, 
                    Keywords.Telemetry);

                return;
            }

            string regKeyValue = this.enlistment.GitBinPath.Substring(0, this.enlistment.GitBinPath.Length - GitBinPathEnd.Length);
            Registry.SetValue(GitVSRegistryKeyName, GitVSRegistryValueName, regKeyValue);
        }

        private NamedPipeServer StartNamedPipe()
        {
            try
            {
                return NamedPipeServer.StartNewServer(this.enlistment.NamedPipeName, this.tracer, this.HandleRequest);
            }
            catch (PipeNameLengthException)
            {
                this.FailMountAndExit("Failed to create mount point. Mount path exceeds the maximum number of allowed characters");
                return null;
            }
        }

        private void FailMountAndExit(string error, params object[] args)
        {
            this.currentState = MountState.MountFailed;

            this.tracer.RelatedError(error, args);
            if (this.showDebugWindow)
            {
                Console.WriteLine("\nPress Enter to Exit");
                Console.ReadLine();
            }

            if (this.rgfltCallbacks != null)
            {
                this.rgfltCallbacks.Dispose();
                this.rgfltCallbacks = null;
            }
            
            Environment.Exit((int)ReturnCode.GenericError);
        }

        private T CreateOrReportAndExit<T>(Func<T> factory, string reportMessage)
        {
            try
            {
                return factory();
            }
            catch (Exception e)
            {
                this.FailMountAndExit(reportMessage + " " + e.ToString());
                throw;
            }
        }

        private void HandleRequest(ITracer tracer, string request, NamedPipeServer.Connection connection)
        {
            NamedPipeMessages.Message message = NamedPipeMessages.Message.FromString(request);

            switch (message.Header)
            {
                case NamedPipeMessages.GetStatus.Request:
                    this.HandleGetStatusRequest(connection);
                    break;

                case NamedPipeMessages.Unmount.Request:
                    this.HandleUnmountRequest(connection);
                    break;

                case NamedPipeMessages.AcquireLock.AcquireRequest:
                    this.HandleLockRequest(message.Body, connection);
                    break;

                case NamedPipeMessages.ReleaseLock.Request:
                    this.HandleReleaseLockRequest(message.Body, connection);
                    break;

                case NamedPipeMessages.DownloadObject.DownloadRequest:
                    this.HandleDownloadObjectRequest(message, connection);
                    break;

                default:
                    EventMetadata metadata = new EventMetadata();
                    metadata.Add("Area", "Mount");
                    metadata.Add("Header", message.Header);
                    this.tracer.RelatedError(metadata, "HandleRequest: Unknown request");

                    connection.TrySendResponse(NamedPipeMessages.UnknownRequest);
                    break;
            }
        }

        private void HandleLockRequest(string messageBody, NamedPipeServer.Connection connection)
        {
            NamedPipeMessages.AcquireLock.Response response;
            NamedPipeMessages.LockData externalHolder;

            NamedPipeMessages.LockRequest request = new NamedPipeMessages.LockRequest(messageBody);
            NamedPipeMessages.LockData requester = request.RequestData;
            if (request == null)
            {
                response = new NamedPipeMessages.AcquireLock.Response(NamedPipeMessages.UnknownRequest, requester);
            }
            else if (this.currentState != MountState.Ready)
            {
                response = new NamedPipeMessages.AcquireLock.Response(NamedPipeMessages.AcquireLock.MountNotReadyResult);
            }
            else
            {
                bool lockAcquired = false;
                if (this.rgfltCallbacks.IsReadyForExternalAcquireLockRequests())
                {
                    lockAcquired = this.rgfsLock.TryAcquireLock(requester, out externalHolder);                   
                }
                else
                {
                    // There might be an external lock holder, and it should be reported to the user
                    externalHolder = this.rgfsLock.GetExternalLockHolder();
                }

                if (lockAcquired)
                {
                    response = new NamedPipeMessages.AcquireLock.Response(NamedPipeMessages.AcquireLock.AcceptResult);
                }
                else if (externalHolder == null)
                {
                    response = new NamedPipeMessages.AcquireLock.Response(NamedPipeMessages.AcquireLock.DenyRGFSResult);
                }
                else
                {
                    response = new NamedPipeMessages.AcquireLock.Response(NamedPipeMessages.AcquireLock.DenyGitResult, externalHolder);
                }
            }

            connection.TrySendResponse(response.CreateMessage());
        }

        private void HandleReleaseLockRequest(string messageBody, NamedPipeServer.Connection connection)
        {
            NamedPipeMessages.LockRequest request = new NamedPipeMessages.LockRequest(messageBody);
            NamedPipeMessages.ReleaseLock.Response response = this.rgfltCallbacks.TryReleaseExternalLock(request.RequestData.PID);
            connection.TrySendResponse(response.CreateMessage());
        }

        private void HandleDownloadObjectRequest(NamedPipeMessages.Message message, NamedPipeServer.Connection connection)
        {
            NamedPipeMessages.DownloadObject.Response response;

            NamedPipeMessages.DownloadObject.Request request = new NamedPipeMessages.DownloadObject.Request(message);
            string objectSha = request.RequestSha;
            if (request == null)
            {
                response = new NamedPipeMessages.DownloadObject.Response(NamedPipeMessages.UnknownRequest);
            }
            else if (this.currentState != MountState.Ready)
            {
                response = new NamedPipeMessages.DownloadObject.Response(NamedPipeMessages.DownloadObject.MountNotReadyResult);
            }
            else
            {
                if (!SHA1Util.IsValidShaFormat(objectSha))
                {
                    response = new NamedPipeMessages.DownloadObject.Response(NamedPipeMessages.DownloadObject.InvalidSHAResult);
                }
                else
                {
                    if (this.gitObjects.TryDownloadAndSaveObject(objectSha) == GitObjects.DownloadAndSaveObjectResult.Success)
                    {
                        response = new NamedPipeMessages.DownloadObject.Response(NamedPipeMessages.DownloadObject.SuccessResult);
                    }
                    else
                    {
                        response = new NamedPipeMessages.DownloadObject.Response(NamedPipeMessages.DownloadObject.DownloadFailed);
                    }
                }
            }

            connection.TrySendResponse(response.CreateMessage());
        }

        private void HandleGetStatusRequest(NamedPipeServer.Connection connection)
        {
            NamedPipeMessages.GetStatus.Response response = new NamedPipeMessages.GetStatus.Response();
            response.EnlistmentRoot = this.enlistment.EnlistmentRoot;
            response.RepoUrl = this.enlistment.RepoUrl;
            response.CacheServer = this.cacheServer.ToString();
            response.LockStatus = this.rgfsLock != null ? this.rgfsLock.GetStatus() : "Unavailable";
            response.DiskLayoutVersion = RepoMetadata.Instance.GetCurrentDiskLayoutVersion();

            switch (this.currentState)
            {
                case MountState.Mounting:
                    response.MountStatus = NamedPipeMessages.GetStatus.Mounting;
                    break;

                case MountState.Ready:
                    response.MountStatus = NamedPipeMessages.GetStatus.Ready;
                    response.BackgroundOperationCount = this.rgfltCallbacks.GetBackgroundOperationCount();
                    break;

                case MountState.Unmounting:
                    response.MountStatus = NamedPipeMessages.GetStatus.Unmounting;
                    break;

                case MountState.MountFailed:
                    response.MountStatus = NamedPipeMessages.GetStatus.MountFailed;
                    break;

                default:
                    response.MountStatus = NamedPipeMessages.UnknownRGFSState;
                    break;
            }

            connection.TrySendResponse(response.ToJson());
        }

        private void HandleUnmountRequest(NamedPipeServer.Connection connection)
        {
            switch (this.currentState)
            {
                case MountState.Mounting:
                    connection.TrySendResponse(NamedPipeMessages.Unmount.NotMounted);
                    break;

                // Even if the previous mount failed, attempt to unmount anyway.  Otherwise the user has no
                // recourse but to kill the process.
                case MountState.MountFailed:
                    goto case MountState.Ready;

                case MountState.Ready:
                    this.currentState = MountState.Unmounting;

                    connection.TrySendResponse(NamedPipeMessages.Unmount.Acknowledged);
                    this.UnmountAndStopWorkingDirectoryCallbacks();
                    connection.TrySendResponse(NamedPipeMessages.Unmount.Completed);

                    this.unmountEvent.Set();
                    Environment.Exit((int)ReturnCode.Success);
                    break;

                case MountState.Unmounting:
                    connection.TrySendResponse(NamedPipeMessages.Unmount.AlreadyUnmounting);
                    break;

                default:
                    connection.TrySendResponse(NamedPipeMessages.UnknownRGFSState);
                    break;
            }
        }

        private void AcquireFolderLocks(RGFSContext context)
        {
            this.folderLockHandles = new List<SafeFileHandle>();
            this.folderLockHandles.Add(context.FileSystem.LockDirectory(context.Enlistment.DotRGFSRoot));
        }

        private void ReleaseFolderLocks()
        {
            foreach (SafeFileHandle folderHandle in this.folderLockHandles)
            {
                folderHandle.Dispose();
            }
        }

        private void MountAndStartWorkingDirectoryCallbacks(RGFSContext context, CacheServerInfo cache)
        {
            string error;
            if (!context.Enlistment.Authentication.TryRefreshCredentials(context.Tracer, out error))
            {
                this.FailMountAndExit("Failed to obtain git credentials: " + error);
            }
            
            GitObjectsHttpRequestor objectRequestor = new GitObjectsHttpRequestor(context.Tracer, context.Enlistment, cache, this.retryConfig);
            this.gitObjects = new RGFSGitObjects(context, objectRequestor);
            this.rgfltCallbacks = this.CreateOrReportAndExit(() => new RGFltCallbacks(context, this.gitObjects, RepoMetadata.Instance), "Failed to create src folder callbacks");

            int persistedVersion;
            if (!RepoMetadata.Instance.TryGetOnDiskLayoutVersion(out persistedVersion, out error))
            {
                this.FailMountAndExit("Error: {0}", error);
            }

            if (persistedVersion != RepoMetadata.DiskLayoutVersion.CurrentDiskLayoutVersion)
            {
                this.FailMountAndExit(
                    "Error: On disk version ({0}) does not match current version ({1})",
                    persistedVersion,
                    RepoMetadata.DiskLayoutVersion.CurrentDiskLayoutVersion);
            }

            try
            {
                if (!this.rgfltCallbacks.TryStart(out error))
                {
                    this.FailMountAndExit("Error: {0}. \r\nPlease confirm that rgfs clone completed without error.", error);
                }
            }
            catch (Exception e)
            {
                this.FailMountAndExit("Failed to initialize src folder callbacks. {0}", e.ToString());
            }

            this.AcquireFolderLocks(context);

            this.heartbeat = new HeartbeatThread(this.tracer, this.rgfltCallbacks);
            this.heartbeat.Start();
        }

        private void UnmountAndStopWorkingDirectoryCallbacks()
        {
            this.ReleaseFolderLocks();

            if (this.heartbeat != null)
            {
                this.heartbeat.Stop();
                this.heartbeat = null;
            }

            if (this.rgfltCallbacks != null)
            {
                this.rgfltCallbacks.Stop();
                this.rgfltCallbacks.Dispose();
                this.rgfltCallbacks = null;
            }
        }
    }
}