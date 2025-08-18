using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[ShutdownDotNetAfterServerBuild]
class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Publish);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution] readonly Solution Solution;
    [GitRepository] readonly GitRepository GitRepository;

    AbsolutePath SolutionFile => RootDirectory / "KafkaConfluentCloud.sln";
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    AbsolutePath PublishDirectory => ArtifactsDirectory / "publish";

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            RootDirectory.GlobDirectories("**/bin", "**/obj")
                .Where(x => !x.ToString().Contains("build"))
                .ForEach(x => x.DeleteDirectory());
            ArtifactsDirectory.CreateOrCleanDirectory();
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(SolutionFile));
        });

    Target Format => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetFormat(s => s
                .SetProject(SolutionFile)
                .SetVerifyNoChanges(EnvironmentInfo.Variables.ContainsKey("TF_BUILD")));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            var defaultProperties = new Dictionary<string, object>
            {
                { "AnalysisLevel", "latest-recommended" },
                { "EnforceCodeStyleInBuild", "true" }
            };

            DotNetBuild(s => s
                .EnableNoRestore()
                .SetProjectFile(SolutionFile)
                .SetConfiguration(Configuration)
                .SetProperties(defaultProperties));
        });

    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetTest(s => s
                .SetProjectFile(SolutionFile)
                .SetConfiguration(Configuration)
                .EnableNoBuild()
                .EnableNoRestore()
                .SetLoggers("trx")
                .SetDataCollector("XPlat Code Coverage"));
        });

    Target PublishApps => _ => _
        .DependsOn(Test)
        .Executes(() =>
        {
            // Publish KafkaProducer.Api
            DotNetPublish(s => s
                .SetProject(RootDirectory / "KafkaProducer.Api" / "KafkaProducer.Api.csproj")
                .SetConfiguration(Configuration.Release)
                .SetOutput(PublishDirectory / "KafkaProducer.Api")
                .EnableNoRestore()
                .SetProperty("UseAppHost", false));

            // Publish KafkaConsumer.Service
            DotNetPublish(s => s
                .SetProject(RootDirectory / "KafkaConsumer.Service" / "KafkaConsumer.Service.csproj")
                .SetConfiguration(Configuration.Release)
                .SetOutput(PublishDirectory / "KafkaConsumer.Service")
                .EnableNoRestore()
                .SetProperty("UseAppHost", false));
        });

    Target Publish => _ => _
        .DependsOn(PublishApps)
        .Executes(() =>
        {
            DotNetPack(s => s
            .EnableNoRestore()
            .SetConfiguration(Configuration.Release)
            .SetOutputDirectory(ArtifactsDirectory));
        });

    Target BuildAll => _ => _
        .DependsOn(Clean, Restore, Format, Compile, Test, Publish)
        .Executes(() =>
        {
            Console.WriteLine("Build pipeline completed successfully!");
        });
}