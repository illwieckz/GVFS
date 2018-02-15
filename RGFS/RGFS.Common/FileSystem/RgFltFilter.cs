﻿using RGFS.Common.Tracing;
using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;
using System.Security;
using System.ServiceProcess;
using System.Text;

namespace RGFS.Common.FileSystem
{
    public class RgFltFilter
    {
        public const RegistryHive RgFltParametersHive = RegistryHive.LocalMachine;
        public const string RgFltParametersKey = "SYSTEM\\CurrentControlSet\\Services\\Rgflt\\Parameters";
        public const string RgFltTimeoutValue = "CommandTimeoutInMs";
        private const string EtwArea = nameof(RgFltFilter);

        private const string RgFltName = "rgflt";

        private const uint OkResult = 0;
        private const uint NameCollisionErrorResult = 0x801F0012;

        public static bool TryAttach(ITracer tracer, string root, out string errorMessage)
        {
            errorMessage = null;
            try
            {
                StringBuilder volumePathName = new StringBuilder(RGFSConstants.MaxPath);
                if (!NativeMethods.GetVolumePathName(root, volumePathName, RGFSConstants.MaxPath))
                {
                    errorMessage = "Could not get volume path name";
                    tracer.RelatedError(errorMessage);
                    return false;
                }

                uint result = NativeMethods.FilterAttach(RgFltName, volumePathName.ToString(), null);
                if (result != OkResult && result != NameCollisionErrorResult)
                {
                    errorMessage = string.Format("Attaching the filter driver resulted in: {0}", result);
                    tracer.RelatedError(errorMessage);
                    return false;
                }
            }
            catch (Exception e)
            {
                errorMessage = string.Format("Attaching the filter driver resulted in: {0}", e.Message);
                tracer.RelatedError(errorMessage);
                return false;
            }

            return true;
        }

        public static bool IsHealthy(out string error, ITracer tracer)
        {
            return IsServiceRunning(out error, tracer);
        }
        
        private static bool IsServiceRunning(out string error, ITracer tracer)
        {
            error = string.Empty;

            bool rgfltServiceRunning = false;
            try
            {
                ServiceController controller = new ServiceController("rgflt");
                rgfltServiceRunning = controller.Status.Equals(ServiceControllerStatus.Running);
            }
            catch (InvalidOperationException e)
            {
                if (tracer != null)
                {
                    EventMetadata metadata = new EventMetadata();
                    metadata.Add("Area", EtwArea);
                    metadata.Add("Exception", e.ToString());
                    tracer.RelatedError(metadata, "InvalidOperationException: RgFlt Service was not found");
                }

                error = "Error: RgFlt Service was not found. To resolve, re-install RGFS";
                return false;
            }

            if (!rgfltServiceRunning)
            {
                if (tracer != null)
                {
                    EventMetadata metadata = new EventMetadata();
                    metadata.Add("Area", EtwArea);
                    tracer.RelatedError(metadata, "RgFlt Service is not running");
                }

                error = "Error: RgFlt Service is not running. To resolve, run \"sc start rgflt\" from an elevated command prompt";
                return false;
            }

            return true;
        }        

        private static class NativeMethods
        {
            [DllImport("fltlib.dll", CharSet = CharSet.Unicode)]
            public static extern uint FilterAttach(
                string filterName,
                string volumeName,
                string instanceName,
                uint createdInstanceNameLength = 0,
                string createdInstanceName = null);

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool GetVolumePathName(
                string volumeName,
                StringBuilder volumePathName,
                uint bufferLength);
        }
    }
}
