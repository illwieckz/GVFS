﻿using RGFS.FunctionalTests.Category;
using NUnit.Framework;

namespace RGFS.FunctionalTests.Tests.GitCommands
{
    [TestFixture]
    [Category(CategoryConstants.GitCommands)]
    public class EnumerationMergeTest : GitRepoTests
    {
        // Commit that found RgFlt Bug 12258777: Entries are sometimes skipped during 
        // enumeration when they don't fit in a user's buffer
        private const string EnumerationReproCommitish = "FunctionalTests/20170602";

        public EnumerationMergeTest() : base(enlistmentPerTest: true)
        {
        }

        [TestCase]
        public void ConfirmEnumerationMatches()
        {
            this.ControlGitRepo.Fetch(GitRepoTests.ConflictSourceBranch);
            this.ValidateGitCommand("checkout " + GitRepoTests.ConflictSourceBranch);

            // Failure for RgFlt Bug 12258777 occurs during teardown, the calls above are to set up
            // the conditions to reproduce the bug
        }

        protected override void CreateEnlistment()
        {
            this.CreateEnlistment(EnumerationReproCommitish);
        }
    }
}
