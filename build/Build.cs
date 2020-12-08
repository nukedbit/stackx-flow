using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.AzurePipelines;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Coverlet;
using Nuke.Common.Tools.DotCover;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;
using Nuke.Common.Tools.NUnit;
using Nuke.Common.Tools.OpenCover;
using Nuke.Common.Tools.ReportGenerator;
using Nuke.Common.Tools.SonarScanner;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.NUnit.NUnitTasks;
using static Nuke.Common.CI.ArtifactExtensions;
using static Nuke.Common.CI.AzurePipelines.AzurePipelines;
using static Nuke.Common.Tools.SonarScanner.SonarScannerTasks;
using static Nuke.Common.Tools.ReportGenerator.ReportGeneratorTasks;
using Nuke.Common.Tools.ReportGenerator;

[CheckBuildProjectConfigurations]
[UnsetVisualStudioEnvironmentVariables]
class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main() => Execute<Build>(x => x.Test);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    string Configuration { get; } = IsLocalBuild ? "Debug" : "Release";

    [Solution] readonly Solution Solution;
    [Required] [GitRepository] readonly GitRepository GitRepository;
    [Required] [GitVersion(Framework = "netcoreapp3.1", NoFetch = true)] readonly GitVersion GitVersion;
      
    [CI] readonly AzurePipelines AzurePipelines;
    
    const string MasterBranch = "master";
    const string DevelopBranch = "develop";
    const string ReleaseBranchPrefix = "release";
    const string HotfixBranchPrefix = "hotfix";


    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath TestsDirectory => RootDirectory / "tests";
    AbsolutePath TestResultDirectory => RootDirectory / ".results" ;
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
            TestsDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
            EnsureCleanDirectory(ArtifactsDirectory);
        });

    Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });
    
    Target SonarQubeBegin =>  _ => _
        .Executes(() =>
        {
            SonarScannerBegin(config => config.SetFramework("net5.0")
                .SetProcessArgumentConfigurator(cfg => cfg.Add("/o:nukedbit")
                    .Add("/k:nukedbit_stackx-flow")
                    .Add("/d:sonar.host.url=https://sonarcloud.io")
                    .Add("/d:sonar.cs.opencover.reportsPaths=\"tests/results/StackX.Flow.Tests.xml\"")));
        });

    Target Compile => _ => _
        .DependsOn(Clean, Restore, SonarQubeBegin)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetAssemblyVersion(GitVersion.AssemblySemVer)
                .SetFileVersion(GitVersion.NuGetVersion)
                .SetInformationalVersion(GitVersion.InformationalVersion)
                .EnableNoRestore());
        });

    [Partition(2)] readonly Partition TestPartition;
    IEnumerable<Project> TestProjects => TestPartition.GetCurrent(Solution.GetProjects("*.Tests"));
    
    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetTest(_ =>
                _.SetConfiguration(Configuration)
                    .SetNoBuild(true)
                    .ResetVerbosity()
                    .SetResultsDirectory(TestResultDirectory)
                    .EnableCollectCoverage()
                    .SetCoverletOutputFormat(CoverletOutputFormat.opencover)
                    .CombineWith(TestProjects, (_, project) =>  
                        _.SetProjectFile(project)
                        .SetLogger($"trx;LogFileName={project.Name}.trx")
                        .SetCoverletOutput(TestResultDirectory / $"{project.Name}.xml")));

            TestResultDirectory.GlobFiles("*.trx").ForEach(x =>
                AzurePipelines?.PublishTestResults(
                    type: AzurePipelinesTestResultsType.VSTest,
                    title: $"{Path.GetFileNameWithoutExtension(x)} ({AzurePipelines.StageDisplayName})",
                    files: new string[] { x }));
        });
    
    
    string CoverageReportDirectory => TestResultDirectory / "coverage-report";
    
    Target Coverage =>  _ => _
        .DependsOn(Test)
        .TriggeredBy(Test)
        .Executes(() =>
        {
            ReportGenerator(_ => _
                .SetReports(TestResultDirectory / "*.xml")
                .SetReportTypes(ReportTypes.Cobertura)
                .SetTargetDirectory(CoverageReportDirectory)
                .SetFramework("net5.0"));
            
            TestResultDirectory.GlobFiles("*.xml").ForEach(x =>
                AzurePipelines?.PublishCodeCoverage(
                    AzurePipelinesCodeCoverageToolType.Cobertura,
                    x,
                    CoverageReportDirectory));
        });
    
    Target SonarQubeEnd =>  _ => _
        .DependsOn(Compile, Test, Coverage)
        .TriggeredBy(Coverage)
        .Executes(() =>
        {
            SonarScannerEnd(config => config.SetFramework("net5.0"));
        });
    
    Target Pack => _ => _
        .DependsOn(Test, Coverage, SonarQubeEnd)
        .Produces(ArtifactsDirectory / "*.nupkg")
        .Executes(() =>
        {
            DotNetPack(s => s
                .SetProject(Solution)
                .SetOutputDirectory(ArtifactsDirectory)
                .SetConfiguration(Configuration)
                .SetAssemblyVersion(GitVersion.AssemblySemVer)
                .SetFileVersion(GitVersion.NuGetVersion)
                .SetVersion(GitVersion.NuGetVersion)
                .SetInformationalVersion(GitVersion.InformationalVersion)
                .EnableNoBuild()
                .EnableNoRestore());
        });
        
        Target PushNuGet => _ => _
            .DependsOn(Pack)
            .Executes(() => {
                GlobFiles(ArtifactsDirectory, "*.nupkg").NotEmpty()
               // .Where(x => !x.EndsWith("symbols.nupkg"))
                .ForEach(x =>
                {
                    DotNetNuGetPush(s => s
                        .SetTargetPath(x)
                        .SetApiKey(Environment.GetEnvironmentVariable("NUGET_API_KEY"))
                        .SetSource(Environment.GetEnvironmentVariable("NUGET_NUKEDBIT_FEED")));
                });
                
            });
}
