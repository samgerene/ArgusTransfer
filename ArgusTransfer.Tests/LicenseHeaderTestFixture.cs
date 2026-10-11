// -------------------------------------------------------------------------------------------------
//   <copyright file="LicenseHeaderTestFixture.cs">
//
//     Copyright (c) 2025-2026 Sam Gerené
//
//     Licensed under the Apache License, Version 2.0 (the "License");
//     you may not use this file except in compliance with the License.
//     You may obtain a copy of the License at
//
//         http://www.apache.org/licenses/LICENSE-2.0
//
//     Unless required by applicable law or agreed to in writing, software
//     distributed under the License is distributed on an "AS IS" BASIS,
//     WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//     See the License for the specific language governing permissions and
//     limitations under the License.
//
//   </copyright>
//   ------------------------------------------------------------------------------------------------

namespace ArgusTransfer.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;

    using NUnit.Framework;

    /// <summary>
    /// Suite of tests that verify the license header of every C# source file in the solution
    /// </summary>
    [TestFixture]
    public class LicenseHeaderTestFixture
    {
        /// <summary>
        /// The number of lines at the start of a file in which the license header is expected
        /// </summary>
        private const int HeaderLineCount = 20;

        /// <summary>
        /// The start of the separator line that opens the license header
        /// </summary>
        private const string SeparatorLineStart = "// ---";

        /// <summary>
        /// The index, in the header returned by <see cref="CreateHeader"/>, of the line that names the file
        /// </summary>
        private const int FileNameLineIndex = 1;

        /// <summary>
        /// The index, in the header returned by <see cref="CreateHeader"/>, of the copyright line
        /// </summary>
        private const int CopyrightLineIndex = 3;

        /// <summary>
        /// The index, in the header returned by <see cref="CreateHeader"/>, of the first line of the warranty disclaimer
        /// </summary>
        private const int DisclaimerLineIndex = 11;

        /// <summary>
        /// The directories, relative to the solution directory, of the projects whose source files are checked
        /// </summary>
        private static readonly string[] ProjectDirectories = { "ArgusTransfer", "ArgusTransfer.Tests", "ArgusTransfer.Sample" };

        /// <summary>
        /// The text of the license header lines that are the same in every file, without the comment prefix and indentation
        /// </summary>
        private static readonly string[] LicenseLines =
        {
            "Licensed under the Apache License, Version 2.0 (the \"License\");",
            "you may not use this file except in compliance with the License.",
            "You may obtain a copy of the License at",
            "http://www.apache.org/licenses/LICENSE-2.0",
            "Unless required by applicable law or agreed to in writing, software",
            "distributed under the License is distributed on an \"AS IS\" BASIS,",
            "WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.",
            "See the License for the specific language governing permissions and",
            "limitations under the License.",
            "</copyright>"
        };

        /// <summary>
        /// Matches the line of the header that names the file
        /// </summary>
        private static readonly Regex FileNameLine = new Regex("^<copyright file=\"(?<name>[^\"]*)\">$", RegexOptions.None, TimeSpan.FromSeconds(1));

        /// <summary>
        /// Matches the copyright line of the header
        /// </summary>
        private static readonly Regex CopyrightLine = new Regex(@"^Copyright \(c\) \d{4}(-\d{4})? Sam Gerené$", RegexOptions.None, TimeSpan.FromSeconds(1));

        [Test]
        public void Verify_that_every_source_file_has_a_consistent_license_header()
        {
            var solutionDirectory = FindSolutionDirectory();

            if (solutionDirectory == null)
            {
                Assert.Ignore("The source files are not available next to the test assembly.");
            }

            var sourceFiles = ProjectDirectories
                .SelectMany(project => Directory.EnumerateFiles(Path.Combine(solutionDirectory, project), "*.cs", SearchOption.AllDirectories))
                .Where(file => !IsBuildOutput(solutionDirectory, file))
                .ToList();

            var violations = sourceFiles
                .SelectMany(file => GetViolations(Path.GetFileName(file), File.ReadLines(file).Take(HeaderLineCount).ToList())
                    .Select(violation => $"{Path.GetRelativePath(solutionDirectory, file)}: {violation}"))
                .ToList();

            Assert.That(sourceFiles, Is.Not.Empty);
            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void Verify_that_a_correct_header_has_no_violations()
        {
            var header = CreateHeader();

            Assert.That(GetViolations("Sample.cs", header), Is.Empty);
        }

        [Test]
        public void Verify_that_a_header_naming_another_file_is_a_violation()
        {
            var header = CreateHeader();
            header[FileNameLineIndex] = "//   <copyright file=\"Other.cs\">";

            var violations = GetViolations("Sample.cs", header);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0], Does.Contain("'Other.cs'"));
        }

        [TestCase("//   <copyright file=\"Sample.cs\" >")]
        [TestCase("//   <copyright>")]
        [TestCase("//")]
        public void Verify_that_a_malformed_or_missing_file_name_line_is_a_violation(string fileNameLine)
        {
            var header = CreateHeader();
            header[FileNameLineIndex] = fileNameLine;

            var violations = GetViolations("Sample.cs", header);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0], Does.Contain("expected one '<copyright file="));
        }

        [Test]
        public void Verify_that_a_second_file_name_line_is_a_violation()
        {
            var header = CreateHeader();
            header.Insert(FileNameLineIndex, header[FileNameLineIndex]);

            var violations = GetViolations("Sample.cs", header);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0], Does.Contain("expected one '<copyright file="));
        }

        [TestCase("//     Copyright (c) 2025-2026 Gerené")]
        [TestCase("//     Copyright (c) 2025-2026 Sam Gerene")]
        [TestCase("//     Copyright (C) 2025-2026 Sam Gerené")]
        [TestCase("//     Copyright (c) Sam Gerené")]
        [TestCase("//")]
        public void Verify_that_a_wrong_or_missing_copyright_line_is_a_violation(string copyrightLine)
        {
            var header = CreateHeader();
            header[CopyrightLineIndex] = copyrightLine;

            var violations = GetViolations("Sample.cs", header);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0], Does.Contain("expected a 'Copyright (c)"));
        }

        [Test]
        public void Verify_that_a_changed_license_line_is_a_violation()
        {
            var header = CreateHeader();
            header[DisclaimerLineIndex] = "//     Unless required by applicable law or agreed to in writing, softwareUseCases";

            var violations = GetViolations("Sample.cs", header);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0], Is.EqualTo("missing header line 'Unless required by applicable law or agreed to in writing, software'"));
        }

        [Test]
        public void Verify_that_a_header_that_does_not_start_on_the_first_line_is_a_violation()
        {
            var header = CreateHeader();
            header.Insert(0, "using System;");

            var violations = GetViolations("Sample.cs", header);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0], Does.Contain("first line"));
        }

        [Test]
        public void Verify_that_a_file_without_a_header_reports_every_violation()
        {
            var lines = new List<string> { "namespace Sample", "{", "}" };

            var violations = GetViolations("Sample.cs", lines);

            Assert.That(violations, Has.Count.EqualTo(LicenseLines.Length + 3));
        }

        [Test]
        public void Verify_that_an_empty_file_reports_every_violation()
        {
            var violations = GetViolations("Sample.cs", new List<string>());

            Assert.That(violations, Has.Count.EqualTo(LicenseLines.Length + 3));
        }

        /// <summary>
        /// Creates the lines of a correct license header for a file named <c>Sample.cs</c>
        /// </summary>
        /// <returns>
        /// The lines of the header, which a test can change to create a deviation
        /// </returns>
        private static List<string> CreateHeader()
        {
            return new List<string>
            {
                "// -------------------------------------------------------------------------------------------------",
                "//   <copyright file=\"Sample.cs\">",
                "//",
                "//     Copyright (c) 2025-2026 Sam Gerené",
                "//",
                "//     Licensed under the Apache License, Version 2.0 (the \"License\");",
                "//     you may not use this file except in compliance with the License.",
                "//     You may obtain a copy of the License at",
                "//",
                "//         http://www.apache.org/licenses/LICENSE-2.0",
                "//",
                "//     Unless required by applicable law or agreed to in writing, software",
                "//     distributed under the License is distributed on an \"AS IS\" BASIS,",
                "//     WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.",
                "//     See the License for the specific language governing permissions and",
                "//     limitations under the License.",
                "//",
                "//   </copyright>",
                "//   ------------------------------------------------------------------------------------------------"
            };
        }

        /// <summary>
        /// Finds the directory that contains the solution file, starting at the directory of the test assembly
        /// </summary>
        /// <returns>
        /// The path of the solution directory, or <c>null</c> when it cannot be found
        /// </returns>
        private static string FindSolutionDirectory()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ArgusTransfer.sln")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName;
        }

        /// <summary>
        /// Determines whether a file is generated by the build rather than part of the sources
        /// </summary>
        /// <param name="solutionDirectory">
        /// The path of the solution directory
        /// </param>
        /// <param name="file">
        /// The path of the file
        /// </param>
        /// <returns>
        /// <c>true</c> when the file is in a <c>bin</c> or <c>obj</c> directory; otherwise <c>false</c>
        /// </returns>
        private static bool IsBuildOutput(string solutionDirectory, string file)
        {
            var directories = Path.GetRelativePath(solutionDirectory, file).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return directories.Any(directory => directory == "bin" || directory == "obj");
        }

        /// <summary>
        /// Checks the license header of a source file
        /// </summary>
        /// <param name="fileName">
        /// The name of the file, without its directory
        /// </param>
        /// <param name="lines">
        /// The first lines of the file, in which the header is expected
        /// </param>
        /// <returns>
        /// A description of each deviation from the expected header; empty when the header is correct
        /// </returns>
        private static List<string> GetViolations(string fileName, List<string> lines)
        {
            var violations = new List<string>();

            if (lines.Count == 0 || !lines[0].StartsWith(SeparatorLineStart, StringComparison.Ordinal))
            {
                violations.Add($"expected the header to start on the first line with '{SeparatorLineStart}'");
            }

            var header = lines
                .Where(line => line.StartsWith("//", StringComparison.Ordinal))
                .Select(line => line.Substring(2).Trim())
                .ToList();

            var fileNames = header.Select(line => FileNameLine.Match(line)).Where(match => match.Success).Select(match => match.Groups["name"].Value).ToList();

            if (fileNames.Count != 1)
            {
                violations.Add("expected one '<copyright file=\"...\">' line");
            }
            else if (fileNames[0] != fileName)
            {
                violations.Add($"the header names the file '{fileNames[0]}'");
            }

            if (!header.Exists(line => CopyrightLine.IsMatch(line)))
            {
                violations.Add("expected a 'Copyright (c) <years> Sam Gerené' line");
            }

            violations.AddRange(LicenseLines.Where(line => !header.Contains(line)).Select(line => $"missing header line '{line}'"));

            return violations;
        }
    }
}
