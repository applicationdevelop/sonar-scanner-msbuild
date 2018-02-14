/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
 * mailto:info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace SonarQube.Common.UnitTests
{
    [TestClass]
    public class CommandLineParserTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void Parser_InvalidArguments()
        {
            AssertException.Expects<ArgumentNullException>(() => new CommandLineParser(null, true));
        }

        [TestMethod]
        public void Parser_DuplicateDescriptors()
        {
            var d1 = ArgumentDescriptor.Create(new string[] { "a" }, "desc1");

            AssertException.Expects<ArgumentException>(() => new CommandLineParser(
                new ArgumentDescriptor[] { d1, d1 }, true));
        }

        [TestMethod]
        public void Parser_UnrecognizedArguments()
        {
            CommandLineParser parser;
            IEnumerable<ArgumentInstance> instances;
            TestLogger logger;

            var args = new string[] { "/a:XXX", "/unrecognized" };

            var d1 = ArgumentDescriptor.Create(new string[] { "/a:" }, "desc1");

            // 1. Don't allow unrecognized
            parser = new CommandLineParser(new ArgumentDescriptor[] { d1 }, false);

            logger = CheckProcessingFails(parser, args);

            logger.AssertSingleErrorExists("/unrecognized");
            logger.AssertErrorsLogged(1);

            // 2. Allow unrecognized
            parser = new CommandLineParser(new ArgumentDescriptor[] { d1 }, true);
            logger = new TestLogger();
            instances = CheckProcessingSucceeds(parser, logger, args);

            AssertExpectedValue(d1, "XXX", instances);
            AssertExpectedInstancesCount(1, instances);
            logger.AssertMessagesLogged(0); // expecting unrecognized arguments to be ignored silently
        }

        [TestMethod]
        public void Parser_CaseSensitivity()
        {
            var args = new string[] { "aaa:all lowercase", "AAA:all uppercase", "aAa: mixed case" };

            var d1 = ArgumentDescriptor.Create(new string[] { "AAA:" }, "desc1", allowMultiple: true);
            var parser = new CommandLineParser(new ArgumentDescriptor[] { d1 }, allowUnrecognized: true);

            // Act
            var instances = CheckProcessingSucceeds(parser, new TestLogger(), args);

            AssertExpectedValue(d1, "all uppercase", instances);
            AssertExpectedInstancesCount(1, instances);
        }

        [TestMethod]
        public void Parser_Multiples()
        {
            CommandLineParser parser;
            IEnumerable<ArgumentInstance> instances;
            TestLogger logger;

            var args = new string[] { "zzzv1", "zzzv2", "zzzv3" };

            // 1. Don't allow multiples
            var d1 = ArgumentDescriptor.Create(new string[] { "zzz" }, "desc1", allowMultiple: false);
            parser = new CommandLineParser(new ArgumentDescriptor[] { d1 }, false);

            logger = CheckProcessingFails(parser, args);

            logger.AssertSingleErrorExists("zzzv2", "v1");
            logger.AssertSingleErrorExists("zzzv3", "v1");
            logger.AssertErrorsLogged(2);

            // 2. Allow multiples
            d1 = ArgumentDescriptor.Create(new string[] { "zzz" }, "desc1", allowMultiple: true);
            parser = new CommandLineParser(new ArgumentDescriptor[] { d1 }, true);
            logger = new TestLogger();
            instances = CheckProcessingSucceeds(parser, logger, args);

            AssertExpectedValues(d1, instances, "v1", "v2", "v3");
            AssertExpectedInstancesCount(3, instances);
        }

        [TestMethod]
        public void Parser_Required()
        {
            CommandLineParser parser;
            IEnumerable<ArgumentInstance> instances;
            TestLogger logger;

            var args = new string[] { };

            // 1. Argument is required
            var d1 = ArgumentDescriptor.Create(new string[] { "AAA" }, "desc1", required: true);
            parser = new CommandLineParser(new ArgumentDescriptor[] { d1 }, false);

            logger = CheckProcessingFails(parser, args);

            logger.AssertSingleErrorExists("desc1");
            logger.AssertErrorsLogged(1);

            // 2. Argument is not required
            d1 = ArgumentDescriptor.Create(new string[] { "AAA" }, "desc1", required: false);
            parser = new CommandLineParser(new ArgumentDescriptor[] { d1 }, true);
            logger = new TestLogger();
            instances = CheckProcessingSucceeds(parser, logger, args);

            AssertExpectedInstancesCount(0, instances);
        }

        [TestMethod]
        [TestCategory("Verbs")]
        public void Parser_Verbs_ExactMatchesOnly()
        {
            CommandLineParser parser;
            IEnumerable<ArgumentInstance> instances;
            TestLogger logger;

            var verb1 = ArgumentDescriptor.CreateVerb("begin", "desc1");
            parser = new CommandLineParser(new ArgumentDescriptor[] { verb1 }, allowUnrecognized: true);

            // 1. Exact match -> matched
            logger = new TestLogger();
            instances = CheckProcessingSucceeds(parser, logger, "begin");
            AssertExpectedValue(verb1, "", instances);
            AssertExpectedInstancesCount(1, instances);

            // 2. Partial match -> not matched
            logger = new TestLogger();
            instances = CheckProcessingSucceeds(parser, logger, "beginX");
            AssertExpectedInstancesCount(0, instances);

            // 3. Combination -> only exact matches matched
            logger = new TestLogger();
            instances = CheckProcessingSucceeds(parser, logger, "beginX", "begin", "beginY");
            Assert.AreEqual(string.Empty, instances.First().Value, "Value for verb should be empty");
            AssertExpectedInstancesCount(1, instances);
            AssertExpectedValue(verb1, "", instances);
        }

        [TestMethod]
        [TestCategory("Verbs")]
        public void Parser_OverlappingVerbsAndPrefixes()
        {
            // Tests handling of verbs and non-verbs that start with the same values
            CommandLineParser parser;
            IEnumerable<ArgumentInstance> instances;
            TestLogger logger;

            var verb1 = ArgumentDescriptor.CreateVerb("X", "verb1 desc");
            var prefix1 = ArgumentDescriptor.Create(new string[] { "XX" }, "prefix1 desc");
            var verb2 = ArgumentDescriptor.CreateVerb("XXX", "verb2 desc");
            var prefix2 = ArgumentDescriptor.Create(new string[] { "XXXX" }, "prefix2 desc");

            // NOTE: this test only works because the descriptors are supplied to parser ordered
            // by decreasing prefix length
            parser = new CommandLineParser(new ArgumentDescriptor[] { prefix2, verb2, prefix1, verb1 }, allowUnrecognized: true);

            // 1. Exact match -> matched
            logger = new TestLogger();
            instances = CheckProcessingSucceeds(parser, logger,
                "X", // verb 1 - exact match
                "XXAAA", // prefix 1 - has value A,
                "XXX", // verb 2 - exact match,
                "XXXXB" // prefix 2 - has value B,
                );

            AssertExpectedValue(verb1, "", instances);
            AssertExpectedValue(prefix1, "AAA", instances);
            AssertExpectedValue(verb2, "", instances);
            AssertExpectedValue(prefix2, "B", instances);
        }

        #endregion Tests

        #region Checks

        private static IEnumerable<ArgumentInstance> CheckProcessingSucceeds(CommandLineParser parser, TestLogger logger, params string[] args)
        {
            var success = parser.ParseArguments(args, logger, out IEnumerable<ArgumentInstance> instances);
            Assert.IsTrue(success, "Expecting parsing to succeed");
            Assert.IsNotNull(instances, "Instances should not be null if parsing succeeds");
            logger.AssertErrorsLogged(0);
            return instances;
        }

        private static TestLogger CheckProcessingFails(CommandLineParser parser, params string[] args)
        {
            var logger = new TestLogger();
            var success = parser.ParseArguments(args, logger, out IEnumerable<ArgumentInstance> instances);

            Assert.IsFalse(success, "Expecting parsing to fail");
            Assert.IsNotNull(instances, "Instances should not be null even if parsing fails");
            AssertExpectedInstancesCount(0, instances);

            logger.AssertErrorsLogged();

            return logger;
        }

        private static void AssertExpectedInstancesCount(int expected, IEnumerable<ArgumentInstance> actual)
        {
            Assert.AreEqual(expected, actual.Count(), "Unexpected number of arguments recognized");
        }

        private static void AssertExpectedValue(ArgumentDescriptor descriptor, string expectedValue, IEnumerable<ArgumentInstance> actual)
        {
            var found = descriptor.TryGetArgumentValue(actual, out string value);
            Assert.IsTrue(found, "Expected argument was not found: {0}", descriptor);
            Assert.IsNotNull(actual);

            Assert.AreEqual(expectedValue, value, "Unexpected instance value: {0}", descriptor);

            var actualValues = actual.Where(descriptor.IsMatch).Select(a => a.Value).ToArray();
            Assert.AreEqual(1, actualValues.Length, "Not expecting to find multiple values of {0}", descriptor);
        }

        private static void AssertExpectedValues(ArgumentDescriptor descriptor, IEnumerable<ArgumentInstance> actual, params string[] expectedValues)
        {
            var actualValues = actual.Where(descriptor.IsMatch).Select(a => a.Value).ToArray();

            CollectionAssert.AreEqual(expectedValues, actualValues);
        }

        #endregion Checks
    }
}
