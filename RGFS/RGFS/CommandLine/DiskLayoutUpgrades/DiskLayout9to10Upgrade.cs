﻿using RGFS.Common;
using RGFS.Common.FileSystem;
using RGFS.Common.Tracing;
using RGFS.RGFlt;
using Microsoft.Isam.Esent;
using Microsoft.Isam.Esent.Collections.Generic;
using System.Collections.Generic;
using System.IO;

namespace RGFS.CommandLine.DiskLayoutUpgrades
{
    public class DiskLayout9to10Upgrade : DiskLayoutUpgrade
    {
        private const string EsentBackgroundOpsFolder = "BackgroundGitUpdates";
        private const string EsentPlaceholderListFolder = "PlaceholderList";

        protected override int SourceLayoutVersion
        {
            get { return 9; }
        }

        /// <summary>
        /// Rewrites ESENT BackgroundGitUpdates and PlaceholderList DBs to flat formats
        /// </summary>
        public override bool TryUpgrade(ITracer tracer, string enlistmentRoot)
        {
            string dotRGFSRoot = Path.Combine(enlistmentRoot, RGFSConstants.DotRGFS.Root);
            if (!this.UpdateBackgroundOperations(tracer, dotRGFSRoot))
            {
                return false;
            }

            if (!this.UpdatePlaceholderList(tracer, dotRGFSRoot))
            {
                return false;
            }
            
            if (!this.TryIncrementDiskLayoutVersion(tracer, enlistmentRoot, this))
            {
                return false;
            }

            return true;
        }

        private bool UpdatePlaceholderList(ITracer tracer, string dotRGFSRoot)
        {
            string esentPlaceholderFolder = Path.Combine(dotRGFSRoot, EsentPlaceholderListFolder);
            if (Directory.Exists(esentPlaceholderFolder))
            {
                string newPlaceholderFolder = Path.Combine(dotRGFSRoot, RGFSConstants.DotRGFS.Databases.PlaceholderList);
                try
                {
                    using (PersistentDictionary<string, string> oldPlaceholders =
                        new PersistentDictionary<string, string>(esentPlaceholderFolder))
                    {
                        string error;
                        PlaceholderListDatabase newPlaceholders;
                        if (!PlaceholderListDatabase.TryCreate(
                            tracer,
                            newPlaceholderFolder,
                            new PhysicalFileSystem(),
                            out newPlaceholders,
                            out error))
                        {
                            tracer.RelatedError("Failed to create new placeholder database: " + error);
                            return false;
                        }

                        using (newPlaceholders)
                        {
                            List<PlaceholderListDatabase.PlaceholderData> data = new List<PlaceholderListDatabase.PlaceholderData>();
                            foreach (KeyValuePair<string, string> kvp in oldPlaceholders)
                            {
                                tracer.RelatedInfo("Copying ESENT entry: {0} = {1}", kvp.Key, kvp.Value);
                                data.Add(new PlaceholderListDatabase.PlaceholderData(path: kvp.Key, sha: kvp.Value));
                            }

                            newPlaceholders.WriteAllEntriesAndFlush(data);
                        }
                    }
                }
                catch (IOException ex)
                {
                    tracer.RelatedError("Could not write to new placeholder database: " + ex.Message);
                    return false;
                }
                catch (EsentException ex)
                {
                    tracer.RelatedError("Placeholder database appears to be from an older version of RGFS and corrupted: " + ex.Message);
                    return false;
                }
                
                string backupName;
                if (this.TryRenameFolderForDelete(tracer, esentPlaceholderFolder, out backupName))
                {
                    // If this fails, we leave behind cruft, but there's no harm because we renamed.
                    this.TryDeleteFolder(tracer, backupName);
                    return true;
                }
                else
                {
                    // To avoid double upgrading, we should rollback if we can't rename the old data
                    this.TryDeleteFile(tracer, RepoMetadata.Instance.DataFilePath);
                    return false;
                }
            }

            return true;
        }

        private bool UpdateBackgroundOperations(ITracer tracer, string dotRGFSRoot)
        {
            string esentBackgroundOpsFolder = Path.Combine(dotRGFSRoot, EsentBackgroundOpsFolder);
            if (Directory.Exists(esentBackgroundOpsFolder))
            {
                string newBackgroundOpsFolder = Path.Combine(dotRGFSRoot, RGFSConstants.DotRGFS.Databases.BackgroundGitOperations);
                try
                {
                    using (PersistentDictionary<long, RGFltCallbacks.BackgroundGitUpdate> oldBackgroundOps =
                        new PersistentDictionary<long, RGFltCallbacks.BackgroundGitUpdate>(esentBackgroundOpsFolder))
                    {
                        string error;
                        BackgroundGitUpdateQueue newBackgroundOps;
                        if (!BackgroundGitUpdateQueue.TryCreate(
                            tracer,
                            newBackgroundOpsFolder,
                            new PhysicalFileSystem(),
                            out newBackgroundOps,
                            out error))
                        {
                            tracer.RelatedError("Failed to create new background operations folder: " + error);
                            return false;
                        }

                        using (newBackgroundOps)
                        {
                            foreach (KeyValuePair<long, RGFltCallbacks.BackgroundGitUpdate> kvp in oldBackgroundOps)
                            {
                                tracer.RelatedInfo("Copying ESENT entry: {0} = {1}", kvp.Key, kvp.Value);
                                newBackgroundOps.EnqueueAndFlush(kvp.Value);
                            }
                        }
                    }
                }
                catch (IOException ex)
                {
                    tracer.RelatedError("Could not write to new background operations: " + ex.Message);
                    return false;
                }
                catch (EsentException ex)
                {
                    tracer.RelatedError("BackgroundOperations appears to be from an older version of RGFS and corrupted: " + ex.Message);
                    return false;
                }
                
                string backupName;
                if (this.TryRenameFolderForDelete(tracer, esentBackgroundOpsFolder, out backupName))
                {
                    // If this fails, we leave behind cruft, but there's no harm because we renamed.
                    this.TryDeleteFolder(tracer, backupName);
                    return true;
                }
                else
                {
                    // To avoid double upgrading, we should rollback if we can't rename the old data
                    this.TryDeleteFile(tracer, RepoMetadata.Instance.DataFilePath);
                    return false;
                }
            }

            return true;
        }
    }
}
