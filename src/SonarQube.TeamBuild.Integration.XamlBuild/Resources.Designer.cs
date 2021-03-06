﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace SonarQube.TeamBuild.Integration.XamlBuild {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "15.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("SonarQube.TeamBuild.Integration.XamlBuild.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Connected to {0}.
        /// </summary>
        internal static string DOWN_DIAG_ConnectedToTFS {
            get {
                return ResourceManager.GetString("DOWN_DIAG_ConnectedToTFS", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Downloading coverage file from {0} to {1}.
        /// </summary>
        internal static string DOWN_DIAG_DownloadCoverageReportFromTo {
            get {
                return ResourceManager.GetString("DOWN_DIAG_DownloadCoverageReportFromTo", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to No code coverage reports were found for the current build..
        /// </summary>
        internal static string PROC_DIAG_NoCodeCoverageReportsFound {
            get {
                return ResourceManager.GetString("PROC_DIAG_NoCodeCoverageReportsFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to download the code coverage report..
        /// </summary>
        internal static string PROC_ERROR_FailedToDownloadReport {
            get {
                return ResourceManager.GetString("PROC_ERROR_FailedToDownloadReport", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &quot;Failed to download the code coverage report from {0}. The HTTP status code was {1} and the reason \&quot;{2}\&quot;&quot;.
        /// </summary>
        internal static string PROC_ERROR_FailedToDownloadReportReason {
            get {
                return ResourceManager.GetString("PROC_ERROR_FailedToDownloadReportReason", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to More than one code coverage result file was created. Only one report can be uploaded to SonarQube. Please modify the build definition so either SonarQube analysis is disabled or only one platform/flavor is built.
        /// </summary>
        internal static string PROC_ERROR_MultipleCodeCoverageReportsFound {
            get {
                return ResourceManager.GetString("PROC_ERROR_MultipleCodeCoverageReportsFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to SonarQube Analysis Summary.
        /// </summary>
        internal static string SonarQubeSummarySectionHeader {
            get {
                return ResourceManager.GetString("SonarQubeSummarySectionHeader", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Connecting to TFS....
        /// </summary>
        internal static string URL_DIAG_ConnectingToTfs {
            get {
                return ResourceManager.GetString("URL_DIAG_ConnectingToTfs", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Coverage Id: {0}, Platform {1}, Flavor {2}.
        /// </summary>
        internal static string URL_DIAG_CoverageReportInfo {
            get {
                return ResourceManager.GetString("URL_DIAG_CoverageReportInfo", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Fetching build information....
        /// </summary>
        internal static string URL_DIAG_FetchingBuildInfo {
            get {
                return ResourceManager.GetString("URL_DIAG_FetchingBuildInfo", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Fetch code coverage report info....
        /// </summary>
        internal static string URL_DIAG_FetchingCoverageReportInfo {
            get {
                return ResourceManager.GetString("URL_DIAG_FetchingCoverageReportInfo", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to ...done..
        /// </summary>
        internal static string URL_DIAG_Finished {
            get {
                return ResourceManager.GetString("URL_DIAG_Finished", resourceCulture);
            }
        }
    }
}
