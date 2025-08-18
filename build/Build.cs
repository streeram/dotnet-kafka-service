using System;
using System.IO;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Compile);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly string Configuration = "Debug";

    AbsolutePath SolutionFile => RootDirectory / "KafkaConfluentCloud.sln";
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";

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
                .SetVerifyNoChanges(false));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(SolutionFile)
                .SetConfiguration(Configuration)
                .EnableNoRestore());
        });

    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetTest(s => s
                .SetProjectFile(SolutionFile)
                .SetConfiguration(Configuration)
                .EnableNoBuild()
                .EnableNoRestore());
        });

    Target Publish => _ => _
        .DependsOn(Test)
        .Executes(() =>
        {
            var apiProject = RootDirectory / "KafkaProducer.Api" / "KafkaProducer.Api.csproj";
            var serviceProject = RootDirectory / "KafkaConsumer.Service" / "KafkaConsumer.Service.csproj";

            DotNetPublish(s => s
                .SetProject(apiProject)
                .SetConfiguration(Configuration)
                .SetOutput(ArtifactsDirectory / "KafkaProducer.Api")
                .EnableNoBuild()
                .EnableNoRestore());

            DotNetPublish(s => s
                .SetProject(serviceProject)
                .SetConfiguration(Configuration)
                .SetOutput(ArtifactsDirectory / "KafkaConsumer.Service")
                .EnableNoBuild()
                .EnableNoRestore());
        });

    Target BuildAll => _ => _
        .DependsOn(Clean, Restore, Format, Compile, Test, Publish)
        .Executes(() =>
        {
            Console.WriteLine("Build pipeline completed successfully!");
        });
}