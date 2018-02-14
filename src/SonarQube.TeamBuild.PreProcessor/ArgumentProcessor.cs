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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SonarQube.Common;

namespace SonarQube.TeamBuild.PreProcessor
{
    /// <summary>
    /// Process and validates the pre-processor command line arguments and reports any errors
    /// </summary>
    public static class ArgumentProcessor // was internal
    {
        /// <summary>
        /// Regular expression to validate a project key.
        /// See http://docs.sonarqube.org/display/SONAR/Project+Administration#ProjectAdministration-AddingaProject
        /// </summary>
        /// <remarks>Should match the java regex here: https://github.com/SonarSource/sonarqube/blob/5.1.1/sonar-core/src/main/java/org/sonar/core/component/ComponentKeys.java#L36
        /// "Allowed characters are alphanumeric, '-', '_', '.' and ':', with at least one non-digit"
        /// </remarks>
        private static readonly Regex ProjectKeyRegEx = new Regex(@"^[a-zA-Z0-9:\-_\.]*[a-zA-Z:\-_\.]+[a-zA-Z0-9:\-_\.]*$", RegexOptions.Compiled | RegexOptions.Singleline);

        private static ArgumentDescriptor ProjectKeyDescriptor = ArgumentDescriptor.Create(
                new string[] { "/key:", "/k:" }, Resources.CmdLine_ArgDescription_ProjectKey, required: true);

        private static ArgumentDescriptor ProjectNameDescriptor = ArgumentDescriptor.Create(
                new string[] { "/name:", "/n:" }, Resources.CmdLine_ArgDescription_ProjectName);

        private static ArgumentDescriptor ProjectVersionDescriptor = ArgumentDescriptor.Create(
                new string[] { "/version:", "/v:" }, Resources.CmdLine_ArgDescription_ProjectVersion);

        private static ArgumentDescriptor OrganizationDescriptor = ArgumentDescriptor.Create(
                new string[] { "/organization:", "/o:" }, Resources.CmdLine_ArgDescription_Organization);

        private static ArgumentDescriptor InstallDescriptor = ArgumentDescriptor.Create(
                new string[] { "/install:" }, Resources.CmdLine_ArgDescription_InstallTargets);

        private static IList<ArgumentDescriptor> Descriptors;

        static ArgumentProcessor()
        {
            // Initialise the set of valid descriptors.
            // To add a new argument, just add it to the list.
            Descriptors = new List<ArgumentDescriptor>
            {
                ProjectKeyDescriptor,
                ProjectNameDescriptor,
                ProjectVersionDescriptor,
                OrganizationDescriptor,
                InstallDescriptor,
                FilePropertyProvider.Descriptor,
                CmdLineArgPropertyProvider.Descriptor
            };

            Debug.Assert(Descriptors.All(d => d.Prefixes != null && d.Prefixes.Any()), "All descriptors must provide at least one prefix");
            Debug.Assert(Descriptors.Distinct().Count() == Descriptors.Count, "All descriptors must be unique");
        }

        /// <summary>
        /// Attempts to process the supplied command line arguments and
        /// reports any errors using the logger.
        /// Returns null unless all of the properties are valid.
        /// </summary>
        public static ProcessedArgs TryProcessArgs(string[] commandLineArgs, ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            ProcessedArgs processed = null;

            // This call will fail if there are duplicate, missing, or unrecognized arguments
            var parser = new CommandLineParser(Descriptors, allowUnrecognized: false);
            var parsedOk = parser.ParseArguments(commandLineArgs, logger, out IEnumerable<ArgumentInstance> arguments);

            // Handle the /install: command line only argument
            parsedOk &= TryGetInstallTargetsEnabled(arguments, logger, out bool installLoaderTargets);

            // Handler for command line analysis properties
            parsedOk &= CmdLineArgPropertyProvider.TryCreateProvider(arguments, logger, out IAnalysisPropertyProvider cmdLineProperties);

            // Handler for scanner environment properties
            parsedOk &= EnvScannerPropertiesProvider.TryCreateProvider(logger, out IAnalysisPropertyProvider scannerEnvProperties);

            // Handler for property file
            var asmPath = Path.GetDirectoryName(typeof(ArgumentProcessor).Assembly.Location);
            parsedOk &= FilePropertyProvider.TryCreateProvider(arguments, asmPath, logger, out IAnalysisPropertyProvider globalFileProperties);

            if (parsedOk)
            {
                Debug.Assert(cmdLineProperties != null);
                Debug.Assert(globalFileProperties != null);

                processed = new ProcessedArgs(
                    GetArgumentValue(ProjectKeyDescriptor, arguments),
                    GetArgumentValue(ProjectNameDescriptor, arguments),
                    GetArgumentValue(ProjectVersionDescriptor, arguments),
                    GetArgumentValue(OrganizationDescriptor, arguments),
                    installLoaderTargets,
                    cmdLineProperties,
                    globalFileProperties,
                    scannerEnvProperties);

                if (!AreParsedArgumentsValid(processed, logger))
                {
                    processed = null;
                }
            }

            return processed;
        }

        private static string GetArgumentValue(ArgumentDescriptor descriptor, IEnumerable<ArgumentInstance> arguments)
        {
            descriptor.TryGetArgumentValue(arguments, out var value);
            return value;
        }

        /// <summary>
        /// Performs any additional validation on the parsed arguments and logs errors
        /// if necessary.
        /// </summary>
        /// <returns>True if the arguments are valid, otherwise false</returns>
        private static bool AreParsedArgumentsValid(ProcessedArgs args, ILogger logger)
        {
            var areValid = true;

            var projectKey = args.ProjectKey;
            if (!IsValidProjectKey(projectKey))
            {
                logger.LogError(Resources.ERROR_InvalidProjectKeyArg);
                areValid = false;
            }

            return areValid;
        }

        private static bool TryGetInstallTargetsEnabled(IEnumerable<ArgumentInstance> arguments, ILogger logger, out bool installTargetsEnabled)
        {
            if (InstallDescriptor.TryGetArgumentValue(arguments, out string value))
            {
                if (!bool.TryParse(value, out installTargetsEnabled))
                {
                    logger.LogError(Resources.ERROR_CmdLine_InvalidInstallTargetsValue, value);
                    return false;
                }
            }
            else
            {
                installTargetsEnabled = TargetsInstaller.DefaultInstallSetting;
            }

            return true;
        }

        private static bool IsValidProjectKey(string projectKey)
        {
            return ProjectKeyRegEx.IsMatch(projectKey);
        }
    }
}
