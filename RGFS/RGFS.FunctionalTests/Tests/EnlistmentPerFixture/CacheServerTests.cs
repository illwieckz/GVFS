﻿using RGFS.FunctionalTests.Tools;
using RGFS.Tests.Should;
using NUnit.Framework;

namespace RGFS.FunctionalTests.Tests.EnlistmentPerFixture
{
    [TestFixture]
    public class CacheServerTests : TestsWithEnlistmentPerFixture
    {
        private const string CustomUrl = "https://myCache";

        [TestCase]
        public void SettingGitConfigChangesCacheServer()
        {
            ProcessResult result = GitProcess.InvokeProcess(this.Enlistment.RepoRoot, "config rgfs.cache-server " + CustomUrl);
            result.ExitCode.ShouldEqual(0, result.Errors);

            this.Enlistment.GetCacheServer().ShouldContain("Using cache server: User Defined (" + CustomUrl + ")");
        }

        [TestCase]
        public void SetAndGetTests()
        {
            this.Enlistment.SetCacheServer("\"\"").ShouldContain("You must specify a value for the cache server");

            string noneMessage = "Using cache server: None (" + this.Enlistment.RepoUrl + ")";

            this.Enlistment.SetCacheServer("None").ShouldContain(noneMessage);
            this.Enlistment.GetCacheServer().ShouldContain(noneMessage);

            this.Enlistment.SetCacheServer(this.Enlistment.RepoUrl).ShouldContain(noneMessage);
            this.Enlistment.GetCacheServer().ShouldContain(noneMessage);

            this.Enlistment.SetCacheServer(CustomUrl).ShouldContain("Using cache server: " + CustomUrl);
            this.Enlistment.GetCacheServer().ShouldContain("Using cache server: User Defined (" + CustomUrl + ")");
        }
    }
}