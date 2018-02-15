﻿/*
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

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using SonarQube.TeamBuild.Integration.XamlBuild;
using SonarQube.TeamBuild.Integration.Tests.Infrastructure;
using TestUtilities;

namespace SonarQube.TeamBuild.Integration.Tests
{
    /*
     * Scenarios:
     * - happy path: one report url, downloads ok, converted ok
     * - no report urls -> success
     * - multiple report urls -> warning, only one downloaded
     * - can't convert files -> no download
     * - failures - exceptions at each stage
     */

    /// <summary>
    /// Unit tests for the orchestration of the code coverage handling
    /// </summary>
    [TestClass]
    public class TfsLegacyCoverageReportProcessorTests
    {
        private const string ValidUrl1 = "vstsf:///foo";
        private const string ValidUrl2 = "vstsf:///foo2";

        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        [TestCategory("CodeCoverage")]
        [Description("Should early out if the files can't be converted")]
        public void ReportProcessor_CannotConvertFiles()
        {
            // Arrange
            var urlProvider = new MockReportUrlProvider() { UrlsToReturn = new string[] { ValidUrl1 } };
            var downloader = new MockReportDownloader();
            var converter = new MockReportConverter() { CanConvert = false };
            var context = CreateValidContext();
            var settings = CreateValidSettings();
            var logger = new TestLogger();

            var processor = new CoverageReportProcessor(
                converter,
                new TfsLegacyCoverageReportLocator(urlProvider, downloader, logger),
                logger);

            // Act
            var initResult = processor.Initialise(context, settings);

            // Assert
            Assert.IsFalse(initResult, "Expecting false: processor should not have been initialised successfully");

            urlProvider.AssertGetUrlsNotCalled();
            downloader.AssertDownloadNotCalled();
            converter.AssertConvertNotCalled();

            logger.AssertWarningsLogged(0);
            logger.AssertErrorsLogged(0);
        }

        [TestMethod]
        [TestCategory("CodeCoverage")]
        public void ReportProcessor_NoUrlsFound()
        {
            // Arrange
            var urlProvider = new MockReportUrlProvider() { UrlsToReturn = new string[] { } };
            var downloader = new MockReportDownloader();
            var converter = new MockReportConverter() { CanConvert = true };
            var context = CreateValidContext();
            var settings = CreateValidSettings();
            var logger = new TestLogger();

            var processor = new CoverageReportProcessor(
                converter,
                new TfsLegacyCoverageReportLocator(urlProvider, downloader, logger),
                logger);

            // Act
            var initResult = processor.Initialise(context, settings);
            Assert.IsTrue(initResult, "Expecting true: processor should have been initialised successfully");
            var result = processor.ProcessCoverageReports();

            // Assert
            urlProvider.AssertGetUrlsCalled();
            downloader.AssertDownloadNotCalled(); // no urls returned, so should go any further
            converter.AssertConvertNotCalled();
            Assert.IsTrue(result, "Expecting true: no coverage reports is a valid scenario");

            logger.AssertWarningsLogged(0);
            logger.AssertErrorsLogged(0);
        }

        [TestMethod]
        [TestCategory("CodeCoverage")]
        [Description("Should early out if multiple reports are found")]
        public void ReportProcessor_MultipleUrlsFound()
        {
            // Arrange
            var urlProvider = new MockReportUrlProvider() { UrlsToReturn = new string[] { ValidUrl1, ValidUrl2 } };
            var downloader = new MockReportDownloader();
            var converter = new MockReportConverter() { CanConvert = true };
            var context = CreateValidContext();
            var settings = CreateValidSettings();
            var logger = new TestLogger();

            var processor = new CoverageReportProcessor(
                converter,
                new TfsLegacyCoverageReportLocator(urlProvider, downloader, logger),
                logger);

            // Act
            var initResult = processor.Initialise(context, settings);
            Assert.IsTrue(initResult, "Expecting true: processor should have been initialised successfully");
            var result = processor.ProcessCoverageReports();

            // Assert
            urlProvider.AssertGetUrlsCalled();
            downloader.AssertDownloadNotCalled(); // Multiple urls so should early out
            converter.AssertConvertNotCalled();
            Assert.IsFalse(result, "Expecting false: can't process multiple coverage reports");

            logger.AssertErrorsLogged(1);
            logger.AssertWarningsLogged(0);
        }

        [TestMethod]
        [TestCategory("CodeCoverage")]
        public void ReportProcessor_SingleUrlFound_NotDownloaded()
        {
            // Arrange
            var urlProvider = new MockReportUrlProvider() { UrlsToReturn = new string[] { ValidUrl1 } };
            var downloader = new MockReportDownloader();
            var converter = new MockReportConverter() { CanConvert = true };
            var context = CreateValidContext();
            var settings = CreateValidSettings();
            var logger = new TestLogger();

            var processor = new CoverageReportProcessor(
                converter,
                new TfsLegacyCoverageReportLocator(urlProvider, downloader, logger),
                logger);

            // Act
            var initResult = processor.Initialise(context, settings);
            Assert.IsTrue(initResult, "Expecting true: processor should have been initialised successfully");
            var result = processor.ProcessCoverageReports();

            // Assert
            urlProvider.AssertGetUrlsCalled();
            downloader.AssertExpectedDownloads(1);
            converter.AssertConvertNotCalled();

            downloader.AssertExpectedUrlsRequested(ValidUrl1);

            Assert.IsFalse(result, "Expecting false: report could not be downloaded");

            logger.AssertErrorsLogged(1);
            logger.AssertWarningsLogged(0);
        }

        [TestMethod]
        [TestCategory("CodeCoverage")]
        public void ReportProcessor_SingleUrlFound_DownloadedOk()
        {
            // Arrange
            var urlProvider = new MockReportUrlProvider() { UrlsToReturn = new string[] { ValidUrl2 } };
            var downloader = new MockReportDownloader();
            var converter = new MockReportConverter() { CanConvert = true };
            var context = CreateValidContext();
            var settings = CreateValidSettings();
            var logger = new TestLogger();

            downloader.CreateFileOnDownloadRequest = true;

            var processor = new CoverageReportProcessor(
                converter,
                new TfsLegacyCoverageReportLocator(urlProvider, downloader, logger),
                logger);

            // Act
            var initResult = processor.Initialise(context, settings);
            Assert.IsTrue(initResult, "Expecting true: processor should have been initialised successfully");
            var result = processor.ProcessCoverageReports();

            // Assert
            urlProvider.AssertGetUrlsCalled();
            downloader.AssertExpectedDownloads(1);
            converter.AssertExpectedNumberOfConversions(1);

            downloader.AssertExpectedUrlsRequested(ValidUrl2);
            downloader.AssertExpectedTargetFileNamesSupplied(Path.Combine(context.SonarOutputDir, TfsLegacyCoverageReportLocator.DownloadFileName));
            Assert.IsTrue(result, "Expecting true: happy path");

            logger.AssertWarningsLogged(0);
            logger.AssertErrorsLogged(0);
        }

        #endregion Tests

        #region Private methods

        private AnalysisConfig CreateValidContext()
        {
            var context = new AnalysisConfig()
            {
                SonarOutputDir = TestContext.DeploymentDirectory, // tests can write to this directory
                SonarConfigDir = TestContext.TestRunResultsDirectory, // we don't read anything from this directory, we just want it to be different from the output directory
                LocalSettings = new AnalysisProperties(),
            };
            return context;
        }

        private TeamBuildSettings CreateValidSettings()
        {
            return TeamBuildSettings.CreateNonTeamBuildSettingsForTesting(TestContext.DeploymentDirectory);
        }

        #endregion Private methods
    }
}
