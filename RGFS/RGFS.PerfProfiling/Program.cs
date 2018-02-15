﻿using RGFS.Common;
using RGFS.RGFlt.DotGit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace RGFS.PerfProfiling
{
    class Program
    {
        static void Main(string[] args)
        {
            ProfilingEnvironment environment = new ProfilingEnvironment(@"M:\OS");
            TimeIt(
                "Validate Index",
                () => GitIndexProjection.ReadIndex(Path.Combine(environment.Enlistment.WorkingDirectoryRoot, RGFSConstants.DotGit.Index)));
            TimeIt(
                "Index Parse (new projection)", 
                () => environment.RGFltCallbacks.GitIndexProjectionProfiler.ForceRebuildProjection());
            TimeIt(
                "Index Parse (update offsets and validate)", 
                () => environment.RGFltCallbacks.GitIndexProjectionProfiler.ForceUpdateOffsetsAndValidateSparseCheckout());
            TimeIt(
                "Index Parse (validate sparse checkout)", 
                () => environment.RGFltCallbacks.GitIndexProjectionProfiler.ForceValidateSparseCheckout());
            Console.WriteLine("Press Enter to exit");
        }

        private static void TimeIt(string name, Action action)
        {
            List<TimeSpan> times = new List<TimeSpan>();

            for (int i = 0; i < 10; i++)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                action();
                stopwatch.Stop();

                times.Add(stopwatch.Elapsed);
                Console.WriteLine(stopwatch.Elapsed.TotalMilliseconds);
            }

            Console.WriteLine("Average Time - " + name + times.Select(timespan => timespan.TotalMilliseconds).Average());
            Console.WriteLine();
        }
    }
}
