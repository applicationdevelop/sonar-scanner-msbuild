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

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SonarQube.Common;

namespace SonarQube.TeamBuild.Integration
{
    public class CoverageReportProcessor : ICoverageReportProcessor
    {
        private const string XmlReportFileExtension = "coveragexml";
        private readonly ICoverageReportConverter converter;
        private readonly ICoverageReportLocator locator;
        private readonly ILogger logger;
        private AnalysisConfig config;
        private ITeamBuildSettings settings;

        private bool succesfullyInitialised = false;

        public CoverageReportProcessor(ICoverageReportConverter converter, ICoverageReportLocator locator, ILogger logger)
        {
            this.converter = converter ?? throw new ArgumentNullException(nameof(converter));
            this.locator = locator ?? throw new ArgumentNullException(nameof(locator));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool Initialise(AnalysisConfig config, ITeamBuildSettings settings)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            succesfullyInitialised = converter.Initialize(logger);
            return succesfullyInitialised;
        }

        public bool ProcessCoverageReports()
        {
            if (!succesfullyInitialised)
            {
                throw new InvalidOperationException(Resources.EX_CoverageReportProcessorNotInitialised);
            }

            Debug.Assert(config != null, "Expecting the config to not be null. Did you call Initialise() ?");

            // Fetch all of the report URLs
            logger.LogInfo(Resources.PROC_DIAG_FetchingCoverageReportInfoFromServer);

            var success = locator.TryGetBinaryCoveragePath(config, settings, out string binaryFilePath);

            if (success &&
                !string.IsNullOrEmpty(binaryFilePath) &&
                TryConvertCoverageReport(binaryFilePath, out var xmlCoveragePath) &&
                !string.IsNullOrEmpty(xmlCoveragePath) &&
                !config.LocalSettings.Any(IsVsCoverageXmlReportsPaths))
            {
                config.LocalSettings.Add(new Property { Id = SonarProperties.VsCoverageXmlReportsPaths, Value = xmlCoveragePath });
            }

            if (!config.LocalSettings.Any(IsVsTestReportsPaths) &&
                locator.TryGetTestResultsPath(config, settings, out var testResultsPath) &&
                !string.IsNullOrEmpty(testResultsPath))
            {
                config.LocalSettings.Add(new Property { Id = SonarProperties.VsTestReportsPaths, Value = testResultsPath });
            }

            return success;
        }

        private static bool IsVsCoverageXmlReportsPaths(Property property) =>
            Property.AreKeysEqual(property.Id, SonarProperties.VsCoverageXmlReportsPaths);

        private static bool IsVsTestReportsPaths(Property property) =>
            Property.AreKeysEqual(property.Id, SonarProperties.VsTestReportsPaths);

        private bool TryConvertCoverageReport(string binaryCoverageFilePath, out string coverageReportFileName)
        {
            coverageReportFileName = null;
            var xmlFileName = Path.ChangeExtension(binaryCoverageFilePath, XmlReportFileExtension);

            Debug.Assert(!File.Exists(xmlFileName),
                "Not expecting a file with the name of the binary-to-XML conversion output to already exist: " + xmlFileName);

            if (converter.ConvertToXml(binaryCoverageFilePath, xmlFileName, logger))
            {
                coverageReportFileName = xmlFileName;
                return true;
            }

            return false;
        }
    }
}
