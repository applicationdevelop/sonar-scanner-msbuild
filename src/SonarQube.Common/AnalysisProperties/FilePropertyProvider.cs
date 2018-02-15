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
using System.IO;
using System.Linq;

namespace SonarQube.Common
{
    /// <summary>
    /// Handles locating an analysis properties file and returning the appropriate properties
    /// </summary>
    public class FilePropertyProvider : IAnalysisPropertyProvider
    {
        public const string DefaultFileName = "SonarQube.Analysis.xml";
        public const string Prefix = "/s:";

        public static readonly ArgumentDescriptor Descriptor = ArgumentDescriptor.Create(
            new string[] { Prefix }, Resources.CmdLine_ArgDescription_PropertiesFilePath);

        private readonly bool isDefaultPropertiesFile;

        #region Public methods

        /// <summary>
        /// Attempts to construct and return a file-based properties provider
        /// </summary>
        /// <param name="defaultPropertiesFileDirectory">Directory in which to look for the default properties file (optional)</param>
        /// <param name="commandLineArguments">List of command line arguments (optional)</param>
        /// <returns>False if errors occurred when constructing the provider, otherwise true</returns>
        /// <remarks>If a properties file could not be located then an empty provider will be returned</remarks>
        public static bool TryCreateProvider(IEnumerable<ArgumentInstance> commandLineArguments, string defaultPropertiesFileDirectory,
            ILogger logger, out IAnalysisPropertyProvider provider)
        {
            if (commandLineArguments == null)
            {
                throw new ArgumentNullException("commandLineArguments");
            }
            if (string.IsNullOrWhiteSpace(defaultPropertiesFileDirectory))
            {
                throw new ArgumentNullException("defaultPropertiesFileDirectory");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            // If the path to a properties file was specified on the command line, use that.
            // Otherwise, look for a default properties file in the default directory.
            var settingsFileArgExists = Descriptor.TryGetArgumentValue(commandLineArguments, out string propertiesFilePath);

            if (ResolveFilePath(propertiesFilePath, defaultPropertiesFileDirectory, logger,
                out AnalysisProperties locatedPropertiesFile))
            {
                if (locatedPropertiesFile == null)
                {
                    provider = EmptyPropertyProvider.Instance;
                }
                else
                {
                    provider = new FilePropertyProvider(locatedPropertiesFile, !settingsFileArgExists);
                }
                return true;
            }

            provider = null;
            return false;
        }

        public static FilePropertyProvider Load(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentNullException("filePath");
            }
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(Resources.ERROR_SettingsFileNotFound, filePath);
            }

            var properties = AnalysisProperties.Load(filePath);
            var provider = new FilePropertyProvider(properties, false);
            return provider;
        }

        public AnalysisProperties PropertiesFile { get; }

        public bool IsDefaultSettingsFile { get { return IsDefaultPropertiesFile; } }

        public bool IsDefaultPropertiesFile => isDefaultPropertiesFile;

        #endregion Public methods

        #region IAnalysisPropertyProvider methods

        public IEnumerable<Property> GetAllProperties()
        {
            return PropertiesFile ?? Enumerable.Empty<Property>();
        }

        public bool TryGetProperty(string key, out Property property)
        {
            return Property.TryGetProperty(key, PropertiesFile, out property);
        }

        #endregion IAnalysisPropertyProvider methods

        #region Private methods

        private FilePropertyProvider(AnalysisProperties properties, bool isDefaultPropertiesFile)
        {
            PropertiesFile = properties ?? throw new ArgumentNullException("properties");
            this.isDefaultPropertiesFile = isDefaultPropertiesFile;
        }

        /// <summary>
        /// Attempt to find a properties file - either the one specified by the user, or the default properties file.
        /// Returns true if the path to a file could be resolved, othewise false.
        /// </summary>
        private static bool ResolveFilePath(string propertiesFilePath, string defaultPropertiesFileDirectory, ILogger logger,
            out AnalysisProperties properties)
        {
            properties = null;
            var isValid = true;

            var resolvedPath = propertiesFilePath ?? TryGetDefaultPropertiesFilePath(defaultPropertiesFileDirectory, logger);

            if (resolvedPath != null)
            {
                if (File.Exists(resolvedPath))
                {
                    try
                    {
                        logger.LogDebug(Resources.MSG_Properties_LoadingPropertiesFromFile, resolvedPath);
                        properties = AnalysisProperties.Load(resolvedPath);
                    }
                    catch (InvalidOperationException)
                    {
                        logger.LogError(Resources.ERROR_Properties_InvalidPropertiesFile, resolvedPath);
                        isValid = false;
                    }
                }
                else
                {
                    logger.LogError(Resources.ERROR_Properties_GlobalPropertiesFileDoesNotExist, resolvedPath);
                    isValid = false;
                }
            }
            return isValid;
        }

        private static string TryGetDefaultPropertiesFilePath(string defaultDirectory, ILogger logger)
        {
            var fullPath = Path.Combine(defaultDirectory, DefaultFileName);
            if (File.Exists(fullPath))
            {
                logger.LogDebug(Resources.MSG_Properties_DefaultPropertiesFileFound, fullPath);
                return fullPath;
            }
            else
            {
                logger.LogDebug(Resources.MSG_Properties_DefaultPropertiesFileNotFound, fullPath);

                return null;
            }
        }

        #endregion Private methods
    }
}
