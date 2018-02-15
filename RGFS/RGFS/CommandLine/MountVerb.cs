﻿using CommandLine;
using RGFS.CommandLine.DiskLayoutUpgrades;
using RGFS.Common;
using RGFS.Common.FileSystem;
using RGFS.Common.Git;
using RGFS.Common.Http;
using RGFS.Common.NamedPipes;
using RGFS.Common.Tracing;
using RGFS.RGFlt.DotGit;
using Microsoft.Diagnostics.Tracing;
using System;
using System.IO;
using System.Security.Principal;

namespace RGFS.CommandLine
{
    [Verb(MountVerb.MountVerbName, HelpText = "Mount a RGFS virtual repo")]
    public class MountVerb : RGFSVerb.ForExistingEnlistment
    {
        private const string MountVerbName = "mount";

        [Option(
            'v',
            RGFSConstants.VerbParameters.Mount.Verbosity,
            Default = RGFSConstants.VerbParameters.Mount.DefaultVerbosity,
            Required = false,
            HelpText = "Sets the verbosity of console logging. Accepts: Verbose, Informational, Warning, Error")]
        public string Verbosity { get; set; }

        [Option(
            'k',
            RGFSConstants.VerbParameters.Mount.Keywords,
            Default = RGFSConstants.VerbParameters.Mount.DefaultKeywords,
            Required = false,
            HelpText = "A CSV list of logging filter keywords. Accepts: Any, Network")]
        public string KeywordsCsv { get; set; }

        [Option(
            'd',
            RGFSConstants.VerbParameters.Mount.DebugWindow,
            Default = false,
            Required = false,
            HelpText = "Show the debug window.  By default, all output is written to a log file and no debug window is shown.")]
        public bool ShowDebugWindow { get; set; }

        public bool SkipMountedCheck { get; set; }
        public bool SkipVersionCheck { get; set; }

        protected override string VerbName
        {
            get { return MountVerbName; }
        }

        public override void InitializeDefaultParameterValues()
        {
            this.Verbosity = RGFSConstants.VerbParameters.Mount.DefaultVerbosity;
            this.KeywordsCsv = RGFSConstants.VerbParameters.Mount.DefaultKeywords;
        }

        protected override void PreCreateEnlistment()
        {
            this.CheckRGFltHealthy();

            string enlistmentRoot = Paths.GetRGFSEnlistmentRoot(this.EnlistmentRootPath);
            if (enlistmentRoot == null)
            {
                this.ReportErrorAndExit("Error: '{0}' is not a valid RGFS enlistment", this.EnlistmentRootPath);
            }

            if (!this.SkipMountedCheck)
            {
                using (NamedPipeClient pipeClient = new NamedPipeClient(Paths.GetNamedPipeName(enlistmentRoot)))
                {
                    if (pipeClient.Connect(500))
                    {
                        this.ReportErrorAndExit(tracer: null, exitCode: ReturnCode.Success, error: "This repo is already mounted.");
                    }
                }
            }
            
            if (!DiskLayoutUpgrade.TryRunAllUpgrades(enlistmentRoot))
            {
                this.ReportErrorAndExit("Failed to upgrade repo disk layout. " + ConsoleHelper.GetRGFSLogMessage(enlistmentRoot));
            }

            string error;
            if (!DiskLayoutUpgrade.TryCheckDiskLayoutVersion(tracer: null, enlistmentRoot: enlistmentRoot, error: out error))
            {
                this.ReportErrorAndExit("Error: " + error);
            }
        }

        protected override void Execute(RGFSEnlistment enlistment)
        {
            string errorMessage = null;
            if (!HooksInstaller.InstallHooks(enlistment, out errorMessage))
            {
                this.ReportErrorAndExit("Error installing hooks: " + errorMessage);
            }

            if (!enlistment.TryConfigureAlternate(out errorMessage))
            {
                this.ReportErrorAndExit("Error configuring alternate: " + errorMessage);
            }

            CacheServerInfo cacheServer = CacheServerResolver.GetCacheServerFromConfig(enlistment);

            string mountExeLocation = null;
            using (JsonEtwTracer tracer = new JsonEtwTracer(RGFSConstants.RGFSEtwProviderName, "PreMount"))
            {
                tracer.AddLogFileEventListener(
                    RGFSEnlistment.GetNewRGFSLogFileName(enlistment.RGFSLogsRoot, RGFSConstants.LogFileTypes.MountVerb),
                    EventLevel.Verbose,
                    Keywords.Any);
                tracer.WriteStartEvent(
                    enlistment.EnlistmentRoot,
                    enlistment.RepoUrl,
                    cacheServer.Url,
                    enlistment.GitObjectsRoot,
                    new EventMetadata
                    {
                        { "Unattended", this.Unattended },
                        { "IsElevated", ProcessHelper.IsAdminElevated() },
                    });

                // TODO 1050199: Once the service is an optional component, RGFS should only attempt to attach
                // RgFlt via the service if the service is present\enabled
                if (!RgFltFilter.TryAttach(tracer, enlistment.EnlistmentRoot, out errorMessage))
                {
                    if (!this.ShowStatusWhileRunning(
                        () => { return this.AttachRgFltThroughService(enlistment, out errorMessage); },
                        "Attaching RgFlt to volume"))
                    {
                        this.ReportErrorAndExit(tracer, errorMessage);
                    }
                }

                this.CheckAntiVirusExclusion(tracer, enlistment.EnlistmentRoot);

                if (!this.SkipVersionCheck)
                {
                    string authErrorMessage = null;
                    if (!this.ShowStatusWhileRunning(
                        () => enlistment.Authentication.TryRefreshCredentials(tracer, out authErrorMessage),
                        "Authenticating"))
                    {
                        this.Output.WriteLine("    WARNING: " + authErrorMessage);
                        this.Output.WriteLine("    Mount will proceed, but new files cannot be accessed until RGFS can authenticate.");
                    }

                    RetryConfig retryConfig = this.GetRetryConfig(tracer, enlistment);
                    RGFSConfig rgfsConfig = this.QueryRGFSConfig(tracer, enlistment, retryConfig);

                    this.ValidateClientVersions(tracer, enlistment, rgfsConfig, showWarnings: true);

                    CacheServerResolver cacheServerResolver = new CacheServerResolver(tracer, enlistment);
                    cacheServer = cacheServerResolver.ResolveNameFromRemote(cacheServer.Url, rgfsConfig);
                    this.Output.WriteLine("Configured cache server: " + cacheServer);
                }

                if (!this.ShowStatusWhileRunning(
                    () => { return this.PerformPreMountValidation(tracer, enlistment, out mountExeLocation, out errorMessage); },
                    "Validating repo"))
                {
                    this.ReportErrorAndExit(tracer, errorMessage);
                }
            }

            if (!this.ShowStatusWhileRunning(
                () => { return this.TryMount(enlistment, mountExeLocation, out errorMessage); },
                "Mounting"))
            {
                this.ReportErrorAndExit(errorMessage);
            }

            if (!this.Unattended)
            {
                if (!this.ShowStatusWhileRunning(
                    () => { return this.RegisterMount(enlistment, out errorMessage); },
                    "Registering for automount"))
                {
                    this.Output.WriteLine("    WARNING: " + errorMessage);
                }
            }
        }

        private bool PerformPreMountValidation(ITracer tracer, RGFSEnlistment enlistment, out string mountExeLocation, out string errorMessage)
        {
            errorMessage = string.Empty;
            mountExeLocation = string.Empty;

            // We have to parse these parameters here to make sure they are valid before 
            // handing them to the background process which cannot tell the user when they are bad
            EventLevel verbosity;
            Keywords keywords;
            this.ParseEnumArgs(out verbosity, out keywords);

            mountExeLocation = Path.Combine(ProcessHelper.GetCurrentProcessLocation(), RGFSConstants.MountExecutableName);
            if (!File.Exists(mountExeLocation))
            {
                errorMessage = "Could not find RGFS.Mount.exe. You may need to reinstall RGFS.";
                return false;
            }

            GitProcess git = new GitProcess(enlistment);
            if (!git.IsValidRepo())
            {
                errorMessage = "The .git folder is missing or has invalid contents";
                return false;
            }

            try
            {
                GitIndexProjection.ReadIndex(Path.Combine(enlistment.WorkingDirectoryRoot, RGFSConstants.DotGit.Index));
            }
            catch (Exception e)
            {
                EventMetadata metadata = new EventMetadata();
                metadata.Add("Exception", e.ToString());
                tracer.RelatedError(metadata, "Index validation failed");
                errorMessage = "Index validation failed, run 'rgfs repair' to repair index.";

                return false;
            }

            return true;
        }

        private bool AttachRgFltThroughService(RGFSEnlistment enlistment, out string errorMessage)
        {
            errorMessage = string.Empty;

            NamedPipeMessages.AttachRgFltRequest request = new NamedPipeMessages.AttachRgFltRequest();
            request.EnlistmentRoot = enlistment.EnlistmentRoot;

            using (NamedPipeClient client = new NamedPipeClient(this.ServicePipeName))
            {
                if (!client.Connect())
                {
                    errorMessage = "Unable to mount because RGFS.Service is not responding. " + RGFSVerb.StartServiceInstructions;
                    return false;
                }

                try
                {
                    client.SendRequest(request.ToMessage());
                    NamedPipeMessages.Message response = client.ReadResponse();
                    if (response.Header == NamedPipeMessages.AttachRgFltRequest.Response.Header)
                    {
                        NamedPipeMessages.AttachRgFltRequest.Response message = NamedPipeMessages.AttachRgFltRequest.Response.FromMessage(response);

                        if (!string.IsNullOrEmpty(message.ErrorMessage))
                        {
                            errorMessage = message.ErrorMessage;
                            return false;
                        }

                        if (message.State != NamedPipeMessages.CompletionState.Success)
                        {
                            errorMessage = "Failed to attach RgFlt to volume.";
                            return false;
                        }
                        else
                        {
                            return true;
                        }
                    }
                    else
                    {
                        errorMessage = string.Format("RGFS.Service responded with unexpected message: {0}", response);
                        return false;
                    }
                }
                catch (BrokenPipeException e)
                {
                    errorMessage = "Unable to communicate with RGFS.Service: " + e.ToString();
                    return false;
                }
            }
        }

        private bool ExcludeFromAntiVirusThroughService(string path, out string errorMessage)
        {
            errorMessage = string.Empty;

            NamedPipeMessages.ExcludeFromAntiVirusRequest request = new NamedPipeMessages.ExcludeFromAntiVirusRequest();
            request.ExclusionPath = path;

            using (NamedPipeClient client = new NamedPipeClient(this.ServicePipeName))
            {
                if (!client.Connect())
                {
                    errorMessage = "Unable to exclude from antivirus because RGFS.Service is not responding. " + RGFSVerb.StartServiceInstructions;
                    return false;
                }

                try
                {
                    client.SendRequest(request.ToMessage());
                    NamedPipeMessages.Message response = client.ReadResponse();
                    if (response.Header == NamedPipeMessages.ExcludeFromAntiVirusRequest.Response.Header)
                    {
                        NamedPipeMessages.ExcludeFromAntiVirusRequest.Response message = NamedPipeMessages.ExcludeFromAntiVirusRequest.Response.FromMessage(response);

                        if (!string.IsNullOrEmpty(message.ErrorMessage))
                        {
                            errorMessage = message.ErrorMessage;
                            return false;
                        }

                        return message.State == NamedPipeMessages.CompletionState.Success;
                    }
                    else
                    {
                        errorMessage = string.Format("RGFS.Service responded with unexpected message: {0}", response);
                        return false;
                    }
                }
                catch (BrokenPipeException e)
                {
                    errorMessage = "Unable to communicate with RGFS.Service: " + e.ToString();
                    return false;
                }
            }
        }

        private void CheckAntiVirusExclusion(ITracer tracer, string path)
        {
            bool isExcluded;
            string getError;
            if (AntiVirusExclusions.TryGetIsPathExcluded(path, out isExcluded, out getError))
            {
                if (!isExcluded)
                {
                    if (ProcessHelper.IsAdminElevated())
                    {
                        string addError;
                        if (AntiVirusExclusions.AddAntiVirusExclusion(path, out addError))
                        {
                            addError = string.Empty;
                            if (!AntiVirusExclusions.TryGetIsPathExcluded(path, out isExcluded, out getError))
                            {
                                EventMetadata metadata = new EventMetadata();
                                metadata.Add("getError", getError);
                                metadata.Add("path", path);
                                tracer.RelatedWarning(metadata, "CheckAntiVirusExclusion: Failed to determine if path excluded after adding it");
                            }
                        }
                        else
                        {
                            EventMetadata metadata = new EventMetadata();
                            metadata.Add("addError", addError);
                            metadata.Add("path", path);
                            tracer.RelatedWarning(metadata, "CheckAntiVirusExclusion: AddAntiVirusExclusion failed");
                        }
                    }
                    else
                    {
                        EventMetadata metadata = new EventMetadata();
                        metadata.Add("path", path);
                        metadata.Add(TracingConstants.MessageKey.InfoMessage, "CheckAntiVirusExclusion: Skipping call to AddAntiVirusExclusion, RGFS is not running with elevation");
                        tracer.RelatedEvent(EventLevel.Informational, "CheckAntiVirusExclusion_SkipLocalAdd", metadata);
                    }
                }                
            }
            else
            {
                EventMetadata metadata = new EventMetadata();
                metadata.Add("getError", getError);
                metadata.Add("path", path);
                tracer.RelatedWarning(metadata, "CheckAntiVirusExclusion: Failed to determine if path excluded");
            }

            string errorMessage = null;
            if (!isExcluded && !this.Unattended)
            {
                if (this.ShowStatusWhileRunning(
                    () => { return this.ExcludeFromAntiVirusThroughService(path, out errorMessage); },
                    string.Format("Excluding '{0}' from antivirus", path)))
                {
                    isExcluded = true;
                }
                else
                {
                    EventMetadata metadata = new EventMetadata();
                    metadata.Add("errorMessage", errorMessage);
                    metadata.Add("path", path);
                    tracer.RelatedWarning(metadata, "CheckAntiVirusExclusion: Failed to exclude path through service");
                }
            }

            if (!isExcluded)
            {
                this.Output.WriteLine();
                this.Output.WriteLine("WARNING: Unable to ensure that '{0}' is excluded from antivirus", path);
                if (!string.IsNullOrEmpty(errorMessage))
                {
                    this.Output.WriteLine(errorMessage);
                }
                
                this.Output.WriteLine();
            }
        }

        private bool TryMount(RGFSEnlistment enlistment, string mountExeLocation, out string errorMessage)
        {
            if (!RGFSVerb.TrySetGitConfigSettings(enlistment))
            {
                errorMessage = "Unable to configure git repo";
                return false;
            }

            const string ParamPrefix = "--";
            ProcessHelper.StartBackgroundProcess(
                mountExeLocation,
                string.Join(
                    " ",
                    enlistment.EnlistmentRoot,
                    ParamPrefix + RGFSConstants.VerbParameters.Mount.Verbosity,
                    this.Verbosity,
                    ParamPrefix + RGFSConstants.VerbParameters.Mount.Keywords,
                    this.KeywordsCsv,
                    this.ShowDebugWindow ? ParamPrefix + RGFSConstants.VerbParameters.Mount.DebugWindow : string.Empty),
                createWindow: this.ShowDebugWindow);

            return RGFSEnlistment.WaitUntilMounted(enlistment.EnlistmentRoot, this.Unattended, out errorMessage);
        }

        private bool RegisterMount(RGFSEnlistment enlistment, out string errorMessage)
        {
            errorMessage = string.Empty;

            NamedPipeMessages.RegisterRepoRequest request = new NamedPipeMessages.RegisterRepoRequest();
            request.EnlistmentRoot = enlistment.EnlistmentRoot;

            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);

            request.OwnerSID = identity.User.Value;

            using (NamedPipeClient client = new NamedPipeClient(this.ServicePipeName))
            {
                if (!client.Connect())
                {
                    errorMessage = "Unable to register repo because RGFS.Service is not responding.";
                    return false;
                }

                try
                {
                    client.SendRequest(request.ToMessage());
                    NamedPipeMessages.Message response = client.ReadResponse();
                    if (response.Header == NamedPipeMessages.RegisterRepoRequest.Response.Header)
                    {
                        NamedPipeMessages.RegisterRepoRequest.Response message = NamedPipeMessages.RegisterRepoRequest.Response.FromMessage(response);

                        if (!string.IsNullOrEmpty(message.ErrorMessage))
                        {
                            errorMessage = message.ErrorMessage;
                            return false;
                        }

                        if (message.State != NamedPipeMessages.CompletionState.Success)
                        {
                            errorMessage = "Unable to register repo. " + errorMessage;
                            return false;
                        }
                        else
                        {
                            return true;
                        }
                    }
                    else
                    {
                        errorMessage = string.Format("RGFS.Service responded with unexpected message: {0}", response);
                        return false;
                    }
                }
                catch (BrokenPipeException e)
                {
                    errorMessage = "Unable to communicate with RGFS.Service: " + e.ToString();
                    return false;
                }
            }
        }

        private void ParseEnumArgs(out EventLevel verbosity, out Keywords keywords)
        {
            if (!Enum.TryParse(this.KeywordsCsv, out keywords))
            {
                this.ReportErrorAndExit("Error: Invalid logging filter keywords: " + this.KeywordsCsv);
            }

            if (!Enum.TryParse(this.Verbosity, out verbosity))
            {
                this.ReportErrorAndExit("Error: Invalid logging verbosity: " + this.Verbosity);
            }
        }
    }
}