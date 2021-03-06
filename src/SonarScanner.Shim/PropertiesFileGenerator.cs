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
using System.Text;
using SonarQube.Common;
using SonarQube.Common.Interfaces;

namespace SonarScanner.Shim
{
    public class PropertiesFileGenerator
    {
        private const string ProjectPropertiesFileName = "sonar-project.properties";
        public const string ReportFileCsharpPropertyKey = "sonar.cs.roslyn.reportFilePath";
        public const string ReportFilesCsharpPropertyKey = "sonar.cs.roslyn.reportFilePaths";
        public const string ReportFileVbnetPropertyKey = "sonar.vbnet.roslyn.reportFilePath";
        public const string ReportFilesVbnetPropertyKey = "sonar.vbnet.roslyn.reportFilePaths";

        private readonly AnalysisConfig analysisConfig;
        private readonly ILogger logger;
        private readonly IRoslynV1SarifFixer fixer;

        public /*for testing*/ PropertiesFileGenerator(AnalysisConfig analysisConfig, ILogger logger,
            IRoslynV1SarifFixer fixer)
        {
            this.analysisConfig = analysisConfig ?? throw new ArgumentNullException(nameof(analysisConfig));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.fixer = fixer ?? throw new ArgumentNullException(nameof(fixer));
        }

        public PropertiesFileGenerator(AnalysisConfig analysisConfig, ILogger logger)
            : this(analysisConfig, logger, new RoslynV1SarifFixer())
        {
        }

        /// <summary>
        /// Locates the ProjectInfo.xml files and uses the information in them to generate
        /// a sonar-scanner properties file
        /// </summary>
        /// <returns>Information about each of the project info files that was processed, together with
        /// the full path to generated file.
        /// Note: the path to the generated file will be null if the file could not be generated.</returns>
        public ProjectInfoAnalysisResult GenerateFile()
        {
            var projectPropertiesPath = Path.Combine(analysisConfig.SonarOutputDir, ProjectPropertiesFileName);
            logger.LogDebug(Resources.MSG_GeneratingProjectProperties, projectPropertiesPath);

            var result = new ProjectInfoAnalysisResult();

            var writer = new PropertiesWriter(analysisConfig);

            var success = TryWriteProperties(writer, out IEnumerable<ProjectData> projects);

            if (success)
            {
                var contents = writer.Flush();

                File.WriteAllText(projectPropertiesPath, contents, Encoding.ASCII);

                result.FullPropertiesFilePath = projectPropertiesPath;
            }

            result.Projects.AddRange(projects);

            return result;
        }

        public bool TryWriteProperties(PropertiesWriter writer, out IEnumerable<ProjectData> allProjects)
        {
            var projects = ProjectLoader.LoadFrom(analysisConfig.SonarOutputDir);

            if (projects == null || !projects.Any())
            {
                logger.LogError(Resources.ERR_NoProjectInfoFilesFound);
                allProjects = Enumerable.Empty<ProjectData>();
                return false;
            }

            var projectPaths = projects.Select(p => p.GetProjectDirectory()).ToList();

            var analysisProperties = analysisConfig.ToAnalysisProperties(logger);

            FixSarifAndEncoding(projects, analysisProperties);

            allProjects = projects
                .GroupBy(p => p.ProjectGuid)
                .Select(ToProjectData)
                .ToList();

            var validProjects = allProjects
                .Where(d => d.Status == ProjectInfoValidity.Valid)
                .ToList();

            if (validProjects.Count == 0)
            {
                logger.LogError(Resources.ERR_NoValidProjectInfoFiles);
                return false;
            }

            var rootProjectBaseDir = ComputeRootProjectBaseDir(projectPaths);
            if (rootProjectBaseDir == null ||
                !Directory.Exists(rootProjectBaseDir))
            {
                logger.LogError(Resources.ERR_ProjectBaseDirDoesNotExist);
                return false;
            }

            var rootModuleFiles = PutFilesToRightModuleOrRoot(validProjects, rootProjectBaseDir);
            PostProcessProjectStatus(validProjects);

            if (rootModuleFiles.Count == 0 && validProjects.All(p => p.Status == ProjectInfoValidity.NoFilesToAnalyze))
            {
                logger.LogError(Resources.ERR_NoValidProjectInfoFiles);
                return false;
            }

            writer.WriteSonarProjectInfo(rootProjectBaseDir);
            writer.WriteSharedFiles(rootModuleFiles);

            validProjects.ForEach(writer.WriteSettingsForProject);

            // Handle global settings
            writer.WriteGlobalSettings(analysisProperties);

            return true;
        }

        /// <summary>
        ///     This method iterates through all referenced files and will either:
        ///     - Skip the file if:
        ///         - it doesn't exists
        ///         - it is located outside of the <see cref="rootProjectBaseDir"/> folder
        ///     - Add the file to the SonarQubeModuleFiles property of the only project it was referenced by (if the project was
        ///       found as being the closest folder to the file.
        ///     - Add the file to the list of files returns by this method in other cases.
        /// </summary>
        /// <remarks>
        ///     This method has some side effects.
        /// </remarks>
        /// <returns>The list of files to attach to the root module.</returns>
        private ICollection<string> PutFilesToRightModuleOrRoot(IEnumerable<ProjectData> projects, string rootProjectBaseDir)
        {
            var fileWithProjects = projects
                .SelectMany(p => p.ReferencedFiles.Select(f => new { Project = p, File = f }))
                .GroupBy(group => group.File, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Select(x => x.Project).ToList());

            var rootModuleFiles = new List<string>();

            foreach (var group in fileWithProjects)
            {
                var file = group.Key;

                if (!File.Exists(file))
                {
                    logger.LogWarning(Resources.WARN_FileDoesNotExist, file);
                    logger.LogDebug(Resources.DEBUG_FileReferencedByProjects, string.Join("', '",
                        group.Value.Select(x => x.Project.FullPath)));
                    continue;
                }

                if (!PathHelper.IsInFolder(file, rootProjectBaseDir)) // File is outside of the SonarQube root module
                {
                    logger.LogWarning(Resources.WARN_FileIsOutsideProjectDirectory, file);
                    logger.LogDebug(Resources.DEBUG_FileReferencedByProjects, string.Join("', '",
                        group.Value.Select(x => x.Project.FullPath)));
                    continue;
                }

                if (group.Value.Count >= 1)
                {
                    var closestProject = GetClosestProjectOrDefault(file, group.Value);

                    if (closestProject == null)
                    {
                        rootModuleFiles.Add(file);
                    }
                    else
                    {
                        closestProject.SonarQubeModuleFiles.Add(file);
                    }
                }
            }

            return rootModuleFiles;
        }

        private void PostProcessProjectStatus(IEnumerable<ProjectData> projects)
        {
            foreach (var project in projects)
            {
                if (project.SonarQubeModuleFiles.Count == 0)
                {
                    project.Status = ProjectInfoValidity.NoFilesToAnalyze;
                }
            }
        }

        private static ProjectData GetClosestProjectOrDefault(string filePath, IEnumerable<ProjectData> projects)
        {
            var longestMatchingPath = (Length: 0, Items: new List<ProjectData>());

            foreach (var project in projects)
            {
                var projectPath = project.Project.GetProjectDirectory();

                // Ensure that folder path always ends with backslash so a directory like 'c:\aaa\bbb' doesn't match
                // a file like 'c:\aaa\bbb.cs' nor 'c:\aaa\bbbxxxx\myfile.cs'
                if (!projectPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                {
                    projectPath += Path.DirectorySeparatorChar;
                }

                if (filePath.StartsWith(projectPath))
                {
                    if (filePath.Length == longestMatchingPath.Length)
                    {
                        longestMatchingPath.Items.Add(project);
                    }
                    else if (filePath.Length > longestMatchingPath.Length)
                    {
                        longestMatchingPath = (Length: filePath.Length, Items: new List<ProjectData> { project });
                    }
                }
            }

            if (longestMatchingPath.Items.Count == 1)
            {
                return longestMatchingPath.Items[0];
            }

            return null;
        }

        internal /* for testing */ ProjectData ToProjectData(IGrouping<Guid, ProjectInfo> projects)
        {
            // To ensure consistently sending of metrics from the same configuration we sort the project outputs
            // and use only the first one for metrics.
            var orderedProjects = projects
                .OrderBy(p => $"{p.Configuration}_{p.Platform}_{p.TargetFramework}")
                .ToList();

            var projectData = new ProjectData(orderedProjects[0])
            {
                Status = ProjectInfoValidity.ExcludeFlagSet
            };

            if (projects.Key == Guid.Empty)
            {
                projectData.Status = ProjectInfoValidity.InvalidGuid;
                return projectData;
            }

            foreach (var p in orderedProjects)
            {
                var status = p.Classify(logger);
                // If we find just one valid configuration, everything is valid
                if (status == ProjectInfoValidity.Valid)
                {
                    projectData.Status = ProjectInfoValidity.Valid;
                    AddProjectFiles(p, projectData);
                    AddRoslynOutputFilePath(p, projectData);
                    AddAnalyzerOutputFilePath(p, projectData);
                }
            }

            if (projectData.ReferencedFiles.Count == 0)
            {
                projectData.Status = ProjectInfoValidity.NoFilesToAnalyze;
            }

            return projectData;
        }

        private void AddAnalyzerOutputFilePath(ProjectInfo project, ProjectData projectData)
        {
            var property = project.AnalysisSettings.FirstOrDefault(p => p.Id.EndsWith(".analyzer.projectOutPath"));
            if (property != null)
            {
                projectData.AnalyzerOutPaths.Add(property.Value);
            }
        }

        private void AddRoslynOutputFilePath(ProjectInfo project, ProjectData projectData)
        {
            var property = project.AnalysisSettings.FirstOrDefault(p => p.Id.EndsWith(".roslyn.reportFilePath"));
            if (property != null)
            {
                projectData.RoslynReportFilePaths.Add(property.Value);
            }
        }

        private void FixSarifAndEncoding(IList<ProjectInfo> projects, AnalysisProperties analysisProperties)
        {
            var globalSourceEncoding = GetSourceEncoding(analysisProperties, new SonarQube.Common.EncodingProvider());

            foreach (var project in projects)
            {
                TryFixSarifReport(project);
                FixEncoding(project, globalSourceEncoding);
            }
        }

        private void TryFixSarifReport(ProjectInfo project)
        {
            TryFixSarifReport(project, RoslynV1SarifFixer.CSharpLanguage, ReportFileCsharpPropertyKey);
            TryFixSarifReport(project, RoslynV1SarifFixer.VBNetLanguage, ReportFileVbnetPropertyKey);
        }

        /// <summary>
        /// Appends the sonar.projectBaseDir value. This is calculated as follows:
        /// 1. the user supplied value, or if none
        /// 2. the sources directory if running from TFS Build or XAML Build, or
        /// 3. the common root path of projects, or if there isn't any
        /// 4. the .sonarqube/out directory
        /// </summary>
        public string ComputeRootProjectBaseDir(IEnumerable<string> projectPaths)
        {
            var projectBaseDir = analysisConfig.LocalSettings
                ?.FirstOrDefault(p => ConfigSetting.SettingKeyComparer.Equals(SonarProperties.ProjectBaseDir, p.Id))
                ?.Value;
            if (!string.IsNullOrWhiteSpace(projectBaseDir))
            {
                projectBaseDir = Path.GetFullPath(projectBaseDir);
                logger.LogDebug("Using user supplied project base directory: '{0}'.", projectBaseDir);
                return projectBaseDir;
            }

            projectBaseDir = analysisConfig.SourcesDirectory;
            if (!string.IsNullOrWhiteSpace(projectBaseDir))
            {
                logger.LogDebug("Using TFS/VSTS sources directory as project base directory: '{0}'.", projectBaseDir);
                return projectBaseDir;
            }

            projectBaseDir = PathHelper.GetCommonRoot(projectPaths);
            if (!string.IsNullOrWhiteSpace(projectBaseDir))
            {
                logger.LogDebug("Using longest common projects root path as project base directory: '{0}'.", projectBaseDir);
                return projectBaseDir;
            }

            logger.LogDebug("Using fallback project base directory: '{0}'.", analysisConfig.SonarOutputDir);
            return analysisConfig.SonarOutputDir;
        }

        /// <summary>
        /// Loads SARIF reports from the given projects and attempts to fix
        /// improper escaping from Roslyn V1 (VS 2015 RTM) where appropriate.
        /// </summary>
        private void TryFixSarifReport(ProjectInfo project, string language, string reportFilePropertyKey)
        {
            var tryResult = project.TryGetAnalysisSetting(reportFilePropertyKey, out Property reportPathProperty);
            if (tryResult)
            {
                var reportPath = reportPathProperty.Value;
                var fixedPath = fixer.LoadAndFixFile(reportPath, language, logger);

                if (!reportPath.Equals(fixedPath)) // only need to alter the property if there was no change
                {
                    // remove the property ahead of changing it
                    // if the new path is null, the file was unfixable and we should leave the property out
                    project.AnalysisSettings.Remove(reportPathProperty);

                    if (fixedPath != null)
                    {
                        // otherwise, set the property value (results in no change if the file was already valid)
                        var newReportPathProperty = new Property
                        {
                            Id = reportFilePropertyKey,
                            Value = fixedPath,
                        };
                        project.AnalysisSettings.Add(newReportPathProperty);
                    }
                }
            }
        }

        private static string GetSourceEncoding(AnalysisProperties properties, IEncodingProvider encodingProvider)
        {
            try
            {
                if (Property.TryGetProperty(SonarProperties.SourceEncoding, properties, out var encodingProperty))
                {
                    return encodingProvider.GetEncoding(encodingProperty.Value).WebName;
                }
            }
            catch (Exception)
            {
                // encoding doesn't exist
            }

            return null;
        }

        private void FixEncoding(ProjectInfo projectInfo, string globalSourceEncoding)
        {
            if (projectInfo.Encoding != null)
            {
                if (globalSourceEncoding != null)
                {
                    logger.LogInfo(Resources.WARN_PropertyIgnored, SonarProperties.SourceEncoding);
                }
            }
            else
            {
                if (globalSourceEncoding == null)
                {
                    if (ProjectLanguages.IsCSharpProject(projectInfo.ProjectLanguage) ||
                        ProjectLanguages.IsVbProject(projectInfo.ProjectLanguage))
                    {
                        projectInfo.Encoding = Encoding.UTF8.WebName;
                    }
                }
                else
                {
                    projectInfo.Encoding = globalSourceEncoding;
                }
            }
        }

        /// <summary>
        /// Returns all of the valid files that can be analyzed. Logs warnings/info about
        /// files that cannot be analyzed.
        /// </summary>
        private void AddProjectFiles(ProjectInfo projectInfo, ProjectData projectData)
        {
            foreach (var file in projectInfo.GetAllAnalysisFiles())
            {
                projectData.ReferencedFiles.Add(file);
            }
        }
    }
}
