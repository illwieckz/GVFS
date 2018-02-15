﻿using RGFS.Common;
using RGFS.Common.Git;
using RGFS.Common.Tracing;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;

namespace RGFS.RGFlt.DotGit
{
    public class AlwaysExcludeFile
    {
        private const string DefaultEntry = "*";
        private HashSet<string> entries;
        private HashSet<string> entriesToRemove;
        private FileSerializer fileSerializer;
        private RGFSContext context;

        public AlwaysExcludeFile(RGFSContext context, string virtualAlwaysExcludeFilePath)
        {
            this.entries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            this.entriesToRemove = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            this.fileSerializer = new FileSerializer(context, virtualAlwaysExcludeFilePath);
            this.context = context;
        }

        public void LoadOrCreate()
        {
            foreach (string line in this.fileSerializer.ReadAll())
            {
                string sanitizedFileLine;
                if (GitConfigHelper.TrySanitizeConfigFileLine(line, out sanitizedFileLine))
                {
                    this.entries.Add(sanitizedFileLine);
                }
            }

            // Ensure the default entry is always in the always_exclude file
            if (this.entries.Add(DefaultEntry))
            {
                this.fileSerializer.AppendLine(DefaultEntry);
                this.fileSerializer.Close();
            }
        }

        public CallbackResult FlushAndClose()
        {
            if (this.entriesToRemove.Count > 0)
            {
                foreach (string entry in this.entriesToRemove)
                {
                    this.entries.Remove(entry);
                }

                try
                {
                    this.fileSerializer.ReplaceFile(this.entries);
                }
                catch (IOException e)
                {
                    return this.ReportException(e, null, isRetryable: true);
                }
                catch (Win32Exception e)
                {
                    return this.ReportException(e, null, isRetryable: true);
                }
                catch (Exception e)
                {
                    return this.ReportException(e, null, isRetryable: false);
                }

                this.entriesToRemove.Clear();
            }

            this.fileSerializer.Close();
            return CallbackResult.Success;
        }

        public CallbackResult AddEntriesForFile(string virtualPath)
        {
            try
            {
                string[] pathParts = virtualPath.Split(new char[] { RGFSConstants.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);
                StringBuilder path = new StringBuilder("!" + RGFSConstants.GitPathSeparatorString, virtualPath.Length + 2);
                for (int i = 0; i < pathParts.Length; i++)
                {
                    path.Append(pathParts[i]);
                    if (i < pathParts.Length - 1)
                    {
                        path.Append(RGFSConstants.GitPathSeparator);
                    }

                    string entry = path.ToString();
                    if (this.entries.Add(entry))
                    {
                        this.fileSerializer.AppendLine(entry);
                    }

                    this.entriesToRemove.Remove(entry);
                }
            }
            catch (IOException e)
            {
                return this.ReportException(e, virtualPath, isRetryable: true);
            }
            catch (Exception e)
            {
                return this.ReportException(e, virtualPath, isRetryable: false);
            }

            return CallbackResult.Success;
        }

        public CallbackResult RemoveEntriesForFiles(List<string> virtualPaths)
        {
            foreach (string virtualPath in virtualPaths)
            {
                string entry = virtualPath.Replace(RGFSConstants.PathSeparator, RGFSConstants.GitPathSeparator);
                entry = "!" + RGFSConstants.GitPathSeparatorString + entry;
                this.entriesToRemove.Add(entry);
            }

            return CallbackResult.Success;
        }

        private CallbackResult ReportException(
            Exception e,
            string virtualPath,
            bool isRetryable,
            [System.Runtime.CompilerServices.CallerMemberName] string functionName = "")
        {
            EventMetadata metadata = new EventMetadata();
            metadata.Add("Area", "AlwaysExcludeFile");
            if (virtualPath != null)
            {
                metadata.Add("virtualPath", virtualPath);
            }

            metadata.Add("Exception", e.ToString());
            if (isRetryable)
            {
                this.context.Tracer.RelatedWarning(metadata, e.GetType().ToString() + " caught while processing " + functionName);
                return CallbackResult.RetryableError;
            }
            else
            {
                this.context.Tracer.RelatedError(metadata, e.GetType().ToString() + " caught while processing " + functionName);
                return CallbackResult.FatalError;
            }
        }
    }
}
