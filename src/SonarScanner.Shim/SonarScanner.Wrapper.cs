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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SonarQube.Common;

namespace SonarScanner.Shim
{
    public class SonarScannerWrapper : ISonarScanner
    {
        /// <summary>
        /// Env variable that controls the amount of memory the JVM can use for the sonar-scanner.
        /// </summary>
        /// <remarks>Large projects error out with OutOfMemoryException if not set</remarks>
        private const string SonarScannerOptsVariableName = "SONAR_SCANNER_OPTS";

        /// <summary>
        /// Env variable that locates the sonar-scanner
        /// </summary>
        /// <remarks>Existing values set by the user might cause failures/remarks>
        public const string SonarScannerHomeVariableName = "SONAR_SCANNER_HOME";

        /// <summary>
        /// Name of the command line argument used to specify the generated project settings file to use
        /// </summary>
        public const string ProjectSettingsFileArgName = "project.settings";

        /// <summary>
        /// Default value for the SONAR_SCANNER_OPTS
        /// </summary>
        /// <remarks>Reserving more than is available on the agent will cause the sonar-scanner to fail</remarks>
        private const string SonarScannerOptsDefaultValue = "-Xmx1024m";

        private const string CmdLineArgPrefix = "-D";

        private const string SonarScannerVersion = "3.0.3.778";

        #region ISonarScanner interface

        public ProjectInfoAnalysisResult Execute(AnalysisConfig config, IEnumerable<string> userCmdLineArguments, ILogger logger)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            if (userCmdLineArguments == null)
            {
                throw new ArgumentNullException("userCmdLineArguments");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            var result = new PropertiesFileGenerator(config, logger).GenerateFile();
            Debug.Assert(result != null, "Not expecting the file generator to return null");
            result.RanToCompletion = false;

            SonarProjectPropertiesValidator.Validate(
                config.SonarScannerWorkingDirectory,
                result.Projects,
                onValid: () =>
                {
                    ProjectInfoReportBuilder.WriteSummaryReport(config, result, logger);

                    result.RanToCompletion = InternalExecute(config, userCmdLineArguments, logger, result.FullPropertiesFilePath);
                },
                onInvalid: (invalidFolders) =>
                {
                    // LOG error message
                    logger.LogError(Resources.ERR_ConflictingSonarProjectProperties, string.Join(", ", invalidFolders));
                });

            return result;
        }

        #endregion ISonarScanner interface

        #region Private methods

        private static bool InternalExecute(AnalysisConfig config, IEnumerable<string> userCmdLineArguments, ILogger logger, string fullPropertiesFilePath)
        {
            if (fullPropertiesFilePath == null)
            {
                // We expect a detailed error message to have been logged explaining
                // why the properties file generation could not be performed
                logger.LogInfo(Resources.MSG_PropertiesGenerationFailed);
                return false;
            }

            var exeFileName = FindScannerExe();
            return ExecuteJavaRunner(config, userCmdLineArguments, logger, exeFileName, fullPropertiesFilePath);
        }

        private static string FindScannerExe()
        {
            var binFolder = Path.GetDirectoryName(typeof(SonarScannerWrapper).Assembly.Location);
            var fileExtension = PlatformHelper.IsWindows() ? ".bat" : "";
            return Path.Combine(binFolder, $"sonar-scanner-{SonarScannerVersion}", "bin", $"sonar-scanner{fileExtension}");
        }

        public /* for test purposes */ static bool ExecuteJavaRunner(AnalysisConfig config, IEnumerable<string> userCmdLineArguments, ILogger logger, string exeFileName, string propertiesFileName)
        {
            Debug.Assert(File.Exists(exeFileName), "The specified exe file does not exist: " + exeFileName);
            Debug.Assert(File.Exists(propertiesFileName), "The specified properties file does not exist: " + propertiesFileName);

            IgnoreSonarScannerHome(logger);

            var allCmdLineArgs = GetAllCmdLineArgs(propertiesFileName, userCmdLineArguments, config);

            var envVarsDictionary = GetAdditionalEnvVariables(logger);
            Debug.Assert(envVarsDictionary != null);

            logger.LogInfo(Resources.MSG_SonarScannerCalling);

            Debug.Assert(!string.IsNullOrWhiteSpace(config.SonarScannerWorkingDirectory), "The working dir should have been set in the analysis config");
            Debug.Assert(Directory.Exists(config.SonarScannerWorkingDirectory), "The working dir should exist");

            var scannerArgs = new ProcessRunnerArguments(exeFileName, PlatformHelper.IsWindows(), logger)
            {
                CmdLineArgs = allCmdLineArgs,
                WorkingDirectory = config.SonarScannerWorkingDirectory,
                EnvironmentVariables = envVarsDictionary
            };

            var runner = new ProcessRunner();

            // SONARMSBRU-202 Note that the Sonar Scanner may write warnings to stderr so
            // we should only rely on the exit code when deciding if it ran successfully
            var success = runner.Execute(scannerArgs);

            if (success)
            {
                logger.LogInfo(Resources.MSG_SonarScannerCompleted);
            }
            else
            {
                logger.LogError(Resources.ERR_SonarScannerExecutionFailed);
            }
            return success;
        }

        private static void IgnoreSonarScannerHome(ILogger logger)
        {
            if (!string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(SonarScannerHomeVariableName)))
            {
                logger.LogInfo(Resources.MSG_SonarScannerHomeIsSet);
                Environment.SetEnvironmentVariable(SonarScannerHomeVariableName, string.Empty);
            }
        }

        /// <summary>
        /// Returns any additional environment variables that need to be passed to
        /// the sonar-scanner
        /// </summary>
        private static IDictionary<string, string> GetAdditionalEnvVariables(ILogger logger)
        {
            IDictionary<string, string> envVarsDictionary = new Dictionary<string, string>();

            // Always set a value for SONAR_SCANNER_OPTS just in case it is set at process-level
            // which wouldn't be inherited by the child sonar-scanner process.
            var sonarScannerOptsValue = GetSonarScannerOptsValue(logger);
            envVarsDictionary.Add(SonarScannerOptsVariableName, sonarScannerOptsValue);

            return envVarsDictionary;
        }

        /// <summary>
        /// Get the value of the SONAR_SCANNER_OPTS variable that controls the amount of memory available to the JDK so that the sonar-scanner doesn't
        /// hit OutOfMemory exceptions. If no env variable with this name is defined then a default value is used.
        /// </summary>
        private static string GetSonarScannerOptsValue(ILogger logger)
        {
            var existingValue = Environment.GetEnvironmentVariable(SonarScannerOptsVariableName);

            if (!string.IsNullOrWhiteSpace(existingValue))
            {
                logger.LogInfo(Resources.MSG_SonarScannerOptsAlreadySet, SonarScannerOptsVariableName, existingValue);
                return existingValue;
            }
            else
            {
                logger.LogInfo(Resources.MSG_SonarScannerOptsDefaultUsed, SonarScannerOptsVariableName, SonarScannerOptsDefaultValue);
                return SonarScannerOptsDefaultValue;
            }
        }

        /// <summary>
        /// Returns all of the command line arguments to pass to sonar-scanner
        /// </summary>
        private static IEnumerable<string> GetAllCmdLineArgs(string projectSettingsFilePath, IEnumerable<string> userCmdLineArguments, AnalysisConfig config)
        {
            // We don't know what all of the valid command line arguments are so we'll
            // just pass them on for the sonar-scanner to validate.
            var args = new List<string>(userCmdLineArguments);

            // Add any sensitive arguments supplied in the config should be passed on the command line
            args.AddRange(GetSensitiveFileSettings(config, userCmdLineArguments));

            // Add the project settings file and the standard options.
            // Experimentation suggests that the sonar-scanner won't error if duplicate arguments
            // are supplied - it will just use the last argument.
            // So we'll set our additional properties last to make sure they take precedence.
            args.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}{1}={2}", CmdLineArgPrefix, ProjectSettingsFileArgName, projectSettingsFilePath));

            return args;
        }

        private static IEnumerable<string> GetSensitiveFileSettings(AnalysisConfig config, IEnumerable<string> userCmdLineArguments)
        {
            var allPropertiesFromConfig = config.GetAnalysisSettings(false).GetAllProperties();

            return allPropertiesFromConfig.Where(p => p.ContainsSensitiveData() && !UserSettingExists(p, userCmdLineArguments))
                .Select(p => p.AsSonarScannerArg());
        }

        private static bool UserSettingExists(Property fileProperty, IEnumerable<string> userArgs)
        {
            return userArgs.Any(userArg => userArg.IndexOf(CmdLineArgPrefix + fileProperty.Id, StringComparison.Ordinal) == 0);
        }

        #endregion Private methods
    }
}
