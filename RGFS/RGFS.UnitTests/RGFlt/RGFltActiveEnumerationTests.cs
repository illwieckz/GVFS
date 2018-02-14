using RGFS.RGFlt;
using RGFS.Tests.Should;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RGFS.UnitTests.RGFlt
{
    [TestFixture]
    public class RGFltActiveEnumerationTests
    {
        [TestCase]
        public void EnumerationHandlesEmptyList()
        {
            using (RGFltActiveEnumeration activeEnumeration = new RGFltActiveEnumeration(new List<RGFltFileInfo>()))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(false);
                activeEnumeration.MoveNext().ShouldEqual(false);
                activeEnumeration.RestartEnumeration(string.Empty);
                activeEnumeration.IsCurrentValid.ShouldEqual(false);
            }
        }

        [TestCase]
        public void EnumerateSingleEntryList()
        {
            List<RGFltFileInfo> entries = new List<RGFltFileInfo>()
            {
                new RGFltFileInfo("a", size: 0, isFolder:false)
            };

            using (RGFltActiveEnumeration activeEnumeration = new RGFltActiveEnumeration(entries))
            {
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries);
            }
        }

        [TestCase]
        public void EnumerateMultipleEntries()
        {
            List<RGFltFileInfo> entries = new List<RGFltFileInfo>()
            {
                new RGFltFileInfo("a", size: 0, isFolder:false),
                new RGFltFileInfo("B", size: 0, isFolder:true),
                new RGFltFileInfo("c", size: 0, isFolder:false),
                new RGFltFileInfo("D.txt", size: 0, isFolder:false),
                new RGFltFileInfo("E.txt", size: 0, isFolder:false),
                new RGFltFileInfo("E.bat", size: 0, isFolder:false),
            };

            using (RGFltActiveEnumeration activeEnumeration = new RGFltActiveEnumeration(entries))
            {
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries);
            }
        }

        [TestCase]
        public void EnumerateSingleEntryListWithEmptyFilter()
        {
            List<RGFltFileInfo> entries = new List<RGFltFileInfo>()
            {
                new RGFltFileInfo("a", size: 0, isFolder:false)
            };

            // Test empty string ("") filter
            using (RGFltActiveEnumeration activeEnumeration = new RGFltActiveEnumeration(entries))
            {
                activeEnumeration.TrySaveFilterString(string.Empty).ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries);
            }

            // Test null filter
            using (RGFltActiveEnumeration activeEnumeration = new RGFltActiveEnumeration(entries))
            {
                activeEnumeration.TrySaveFilterString(null).ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries);
            }
        }

        [TestCase]
        public void EnumerateSingleEntryListWithWildcardFilter()
        {
            List<RGFltFileInfo> entries = new List<RGFltFileInfo>()
            {
                new RGFltFileInfo("a", size: 0, isFolder:false)
            };

            using (RGFltActiveEnumeration activeEnumeration = new RGFltActiveEnumeration(entries))
            {
                activeEnumeration.TrySaveFilterString("*").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries);
            }

            using (RGFltActiveEnumeration activeEnumeration = new RGFltActiveEnumeration(entries))
            {
                activeEnumeration.TrySaveFilterString("?").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries);
            }

            using (RGFltActiveEnumeration activeEnumeration = new RGFltActiveEnumeration(entries))
            {
                activeEnumeration.TrySaveFilterString("*.*").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries);
            }
        }

        [TestCase]
        public void EnumerateSingleEntryListWithMatchingFilter()
        {
            List<RGFltFileInfo> entries = new List<RGFltFileInfo>()
            {
                new RGFltFileInfo("a", size: 0, isFolder:false)
            };

            using (RGFltActiveEnumeration activeEnumeration = new RGFltActiveEnumeration(entries))
            {
                activeEnumeration.TrySaveFilterString("a").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries);
            }

            using (RGFltActiveEnumeration activeEnumeration = new RGFltActiveEnumeration(entries))
            {
                activeEnumeration.TrySaveFilterString("A").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries);
            }
        }

        [TestCase]
        public void EnumerateSingleEntryListWithNonMatchingFilter()
        {
            List<RGFltFileInfo> entries = new List<RGFltFileInfo>()
            {
                new RGFltFileInfo("a", size: 0, isFolder:false)
            };

            using (RGFltActiveEnumeration activeEnumeration = new RGFltActiveEnumeration(entries))
            {
                string filter = "b";
                activeEnumeration.TrySaveFilterString(filter).ShouldEqual(true);
                activeEnumeration.IsCurrentValid.ShouldEqual(false);
                activeEnumeration.MoveNext().ShouldEqual(false);
                activeEnumeration.RestartEnumeration(filter);
                activeEnumeration.IsCurrentValid.ShouldEqual(false);
            }
        }

        [TestCase]
        public void CannotSetMoreThanOneFilter()
        {
            string filterString = "*.*";

            using (RGFltActiveEnumeration activeEnumeration = new RGFltActiveEnumeration(new List<RGFltFileInfo>()))
            {
                activeEnumeration.TrySaveFilterString(filterString).ShouldEqual(true);
                activeEnumeration.TrySaveFilterString(null).ShouldEqual(false);
                activeEnumeration.TrySaveFilterString(string.Empty).ShouldEqual(false);
                activeEnumeration.TrySaveFilterString("?").ShouldEqual(false);
                activeEnumeration.GetFilterString().ShouldEqual(filterString);
            }
        }

        [TestCase]
        public void EnumerateMultipleEntryListWithEmptyFilter()
        {
            List<RGFltFileInfo> entries = new List<RGFltFileInfo>()
            {
                new RGFltFileInfo("a", size: 0, isFolder:false),
                new RGFltFileInfo("B", size: 0, isFolder:true),
                new RGFltFileInfo("c", size: 0, isFolder:false),
                new RGFltFileInfo("D.txt", size: 0, isFolder:false),
                new RGFltFileInfo("E.txt", size: 0, isFolder:false),
                new RGFltFileInfo("E.bat", size: 0, isFolder:false),
            };

            // Test empty string ("") filter
            using (RGFltActiveEnumeration activeEnumeration = new RGFltActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.TrySaveFilterString(string.Empty).ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries);
            }

            // Test null filter
            using (RGFltActiveEnumeration activeEnumeration = new RGFltActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.TrySaveFilterString(null).ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries);
            }
        }

        [TestCase]
        public void EnumerateMultipleEntryListWithWildcardFilter()
        {
            List<RGFltFileInfo> entries = new List<RGFltFileInfo>()
            {
                new RGFltFileInfo("a", size: 0, isFolder:false),
                new RGFltFileInfo("B", size: 0, isFolder:true),
                new RGFltFileInfo("c", size: 0, isFolder:false),
                new RGFltFileInfo("D.txt", size: 0, isFolder:false),
                new RGFltFileInfo("E.txt", size: 0, isFolder:false),
                new RGFltFileInfo("E.bat", size: 0, isFolder:false),
            };

            using (RGFltActiveEnumeration activeEnumeration = new RGFltActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.TrySaveFilterString("*.*").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries);
            }

            using (RGFltActiveEnumeration activeEnumeration = new RGFltActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.TrySaveFilterString("*.txt").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries.Where(entry => entry.Name.EndsWith(".txt", System.StringComparison.OrdinalIgnoreCase)));
            }

            // '<' = DOS_STAR, matches 0 or more characters until encountering and matching
            //                 the final . in the name
            using (RGFltActiveEnumeration activeEnumeration = new RGFltActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.TrySaveFilterString("<.txt").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries.Where(entry => entry.Name.EndsWith(".txt", System.StringComparison.OrdinalIgnoreCase)));
            }

            using (RGFltActiveEnumeration activeEnumeration = new RGFltActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.TrySaveFilterString("?").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries.Where(entry => entry.Name.Length == 1));
            }

            using (RGFltActiveEnumeration activeEnumeration = new RGFltActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.TrySaveFilterString("?.txt").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries.Where(entry => entry.Name.Length == 5 && entry.Name.EndsWith(".txt", System.StringComparison.OrdinalIgnoreCase)));
            }

            // '>' = DOS_QM, matches any single character, or upon encountering a period or
            //               end of name string, advances the expression to the end of the
            //               set of contiguous DOS_QMs.
            using (RGFltActiveEnumeration activeEnumeration = new RGFltActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.TrySaveFilterString(">.txt").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries.Where(entry => entry.Name.Length == 5 && entry.Name.EndsWith(".txt", System.StringComparison.OrdinalIgnoreCase)));
            }

            using (RGFltActiveEnumeration activeEnumeration = new RGFltActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.TrySaveFilterString("E.???").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries.Where(entry => entry.Name.Length == 5 && entry.Name.StartsWith("E.", System.StringComparison.OrdinalIgnoreCase)));
            }

            // '"' = DOS_DOT, matches either a . or zero characters beyond name string.
            using (RGFltActiveEnumeration activeEnumeration = new RGFltActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.TrySaveFilterString("E\"*").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries.Where(entry => entry.Name.StartsWith("E.", System.StringComparison.OrdinalIgnoreCase) || entry.Name.Equals("E", System.StringComparison.OrdinalIgnoreCase)));
            }

            using (RGFltActiveEnumeration activeEnumeration = new RGFltActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.TrySaveFilterString("e\"*").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries.Where(entry => entry.Name.StartsWith("E.", System.StringComparison.OrdinalIgnoreCase) || entry.Name.Equals("E", System.StringComparison.OrdinalIgnoreCase)));
            }

            using (RGFltActiveEnumeration activeEnumeration = new RGFltActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.TrySaveFilterString("B\"*").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries.Where(entry => entry.Name.StartsWith("B.", System.StringComparison.OrdinalIgnoreCase) || entry.Name.Equals("B", System.StringComparison.OrdinalIgnoreCase)));
            }

            using (RGFltActiveEnumeration activeEnumeration = new RGFltActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.TrySaveFilterString("e.???").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries.Where(entry => entry.Name.Length == 5 && entry.Name.StartsWith("E.", System.StringComparison.OrdinalIgnoreCase)));
            }
        }

        [TestCase]
        public void EnumerateMultipleEntryListWithMatchingFilter()
        {
            List<RGFltFileInfo> entries = new List<RGFltFileInfo>()
            {
                new RGFltFileInfo("a", size: 0, isFolder:false),
                new RGFltFileInfo("B", size: 0, isFolder:true),
                new RGFltFileInfo("c", size: 0, isFolder:false),
                new RGFltFileInfo("D.txt", size: 0, isFolder:false),
                new RGFltFileInfo("E.txt", size: 0, isFolder:false),
                new RGFltFileInfo("E.bat", size: 0, isFolder:false),
            };

            using (RGFltActiveEnumeration activeEnumeration = new RGFltActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.TrySaveFilterString("E.bat").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries.Where(entry => entry.Name == "E.bat"));
            }

            using (RGFltActiveEnumeration activeEnumeration = new RGFltActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.TrySaveFilterString("e.bat").ShouldEqual(true);
                this.ValidateActiveEnumeratorReturnsAllEntries(activeEnumeration, entries.Where(entry => string.Compare(entry.Name, "e.bat", StringComparison.OrdinalIgnoreCase) == 0));
            }
        }

        [TestCase]
        public void EnumerateMultipleEntryListWithNonMatchingFilter()
        {
            List<RGFltFileInfo> entries = new List<RGFltFileInfo>()
            {
                new RGFltFileInfo("a", size: 0, isFolder:false),
                new RGFltFileInfo("B", size: 0, isFolder:true),
                new RGFltFileInfo("c", size: 0, isFolder:false),
                new RGFltFileInfo("D.txt", size: 0, isFolder:false),
                new RGFltFileInfo("E.txt", size: 0, isFolder:false),
                new RGFltFileInfo("E.bat", size: 0, isFolder:false),
            };

            using (RGFltActiveEnumeration activeEnumeration = new RGFltActiveEnumeration(entries))
            {
                string filter = "g";
                activeEnumeration.TrySaveFilterString(filter).ShouldEqual(true);
                activeEnumeration.IsCurrentValid.ShouldEqual(false);
                activeEnumeration.MoveNext().ShouldEqual(false);
                activeEnumeration.RestartEnumeration(filter);
                activeEnumeration.IsCurrentValid.ShouldEqual(false);
            }
        }

        [TestCase]
        public void SettingFilterAdvancesEnumeratorToMatchingEntry()
        {
            List<RGFltFileInfo> entries = new List<RGFltFileInfo>()
            {
                new RGFltFileInfo("a", size: 0, isFolder:false),
                new RGFltFileInfo("B", size: 0, isFolder:true),
                new RGFltFileInfo("c", size: 0, isFolder:false),
                new RGFltFileInfo("D.txt", size: 0, isFolder:false),
                new RGFltFileInfo("E.txt", size: 0, isFolder:false),
                new RGFltFileInfo("E.bat", size: 0, isFolder:false),
            };

            using (RGFltActiveEnumeration activeEnumeration = new RGFltActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.Current.ShouldBeSameAs(entries[0]);
                activeEnumeration.TrySaveFilterString("D.txt").ShouldEqual(true);
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.Current.Name.ShouldEqual("D.txt");
            }
        }

        [TestCase]
        public void RestartingScanWithFilterAdvancesEnumeratorToNewMatchingEntry()
        {
            List<RGFltFileInfo> entries = new List<RGFltFileInfo>()
            {
                new RGFltFileInfo("a", size: 0, isFolder:false),
                new RGFltFileInfo("B", size: 0, isFolder:true),
                new RGFltFileInfo("c", size: 0, isFolder:false),
                new RGFltFileInfo("D.txt", size: 0, isFolder:false),
                new RGFltFileInfo("E.txt", size: 0, isFolder:false),
                new RGFltFileInfo("E.bat", size: 0, isFolder:false),
            };

            using (RGFltActiveEnumeration activeEnumeration = new RGFltActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.Current.ShouldBeSameAs(entries[0]);
                activeEnumeration.TrySaveFilterString("D.txt").ShouldEqual(true);
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.Current.Name.ShouldEqual("D.txt");

                activeEnumeration.RestartEnumeration("c");
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.Current.Name.ShouldEqual("c");
            }
        }

        [TestCase]
        public void RestartingScanWithFilterAdvancesEnumeratorToFirstMatchingEntry()
        {
            List<RGFltFileInfo> entries = new List<RGFltFileInfo>()
            {
                new RGFltFileInfo("C.TXT", size: 0, isFolder:false),
                new RGFltFileInfo("D.txt", size: 0, isFolder:false),
                new RGFltFileInfo("E.txt", size: 0, isFolder:false),
                new RGFltFileInfo("E.bat", size: 0, isFolder:false),
            };

            using (RGFltActiveEnumeration activeEnumeration = new RGFltActiveEnumeration(entries))
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.Current.ShouldBeSameAs(entries[0]);
                activeEnumeration.TrySaveFilterString("D.txt").ShouldEqual(true);
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.Current.Name.ShouldEqual("D.txt");

                activeEnumeration.RestartEnumeration("c*");
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.Current.Name.ShouldEqual("C.TXT");
            }
        }

        private void ValidateActiveEnumeratorReturnsAllEntries(RGFltActiveEnumeration activeEnumeration, IEnumerable<RGFltFileInfo> entries)
        {
            activeEnumeration.IsCurrentValid.ShouldEqual(true);

            // activeEnumeration should iterate over each entry in entries
            foreach (RGFltFileInfo entry in entries)
            {
                activeEnumeration.IsCurrentValid.ShouldEqual(true);
                activeEnumeration.Current.ShouldBeSameAs(entry);
                activeEnumeration.MoveNext();
            }

            // activeEnumeration should no longer be valid after iterating beyond the end of the list
            activeEnumeration.IsCurrentValid.ShouldEqual(false);

            // attempts to move beyond the end of the list should fail
            activeEnumeration.MoveNext().ShouldEqual(false);
        }
    }
}