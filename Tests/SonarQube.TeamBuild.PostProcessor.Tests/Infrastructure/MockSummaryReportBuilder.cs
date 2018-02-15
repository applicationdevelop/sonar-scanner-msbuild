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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using SonarScanner.Shim;

namespace SonarQube.TeamBuild.PostProcessor.Tests
{
    internal class MockSummaryReportBuilder : ISummaryReportBuilder
    {
        private bool methodCalled;

        #region ISummaryReportBuilder interface

        public void GenerateReports(ITeamBuildSettings settings, AnalysisConfig config, ProjectInfoAnalysisResult result, ILogger logger)
        {
            Assert.IsFalse(methodCalled, "Generate reports has already been called");

            methodCalled = true;
        }

        #endregion ISummaryReportBuilder interface

        #region Checks

        public void AssertExecuted()
        {
            Assert.IsTrue(methodCalled, "Expecting ISummaryReportBuilder.GenerateReports to have been called");
        }

        public void AssertNotExecuted()
        {
            Assert.IsFalse(methodCalled, "Not expecting ISummaryReportBuilder.GenerateReports to have been called");
        }

        #endregion Checks
    }
}
