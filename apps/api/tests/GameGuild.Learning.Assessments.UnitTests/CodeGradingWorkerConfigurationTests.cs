using System.Text.Json;
using FluentAssertions;
using GameGuild.Learning.Assessments.Grading.Code;
using GameGuild.Learning.Courses;
using Microsoft.Extensions.Options;
using Xunit;

namespace GameGuild.Learning.Assessments.Tests;

public sealed class CodeGradingWorkerConfigurationTests
{
    [Fact]
    public void Launcher_UsesAbsoluteInstalledFilesAndLiteralArgumentsWithoutAShell()
    {
        using var installation = new WorkerInstallation();
        var options = installation.Configuration;
        var start = CodeGradingWorker.CreateStartInfo(options);

        start.FileName.Should().Be(options.NodeExecutable);
        start.WorkingDirectory.Should().Be(installation.Root);
        start.UseShellExecute.Should().BeFalse();
        start.CreateNoWindow.Should().BeTrue();
        start.RedirectStandardInput.Should().BeTrue();
        start.RedirectStandardOutput.Should().BeTrue();
        start.RedirectStandardError.Should().BeTrue();
        start.Arguments.Should().BeEmpty();
        start.ArgumentList.Should().Equal("--max-old-space-size=512", options.ScriptPath,
            options.RuntimeDirectory, options.CdnDirectory);
        start.Environment.Keys.Should().OnlyContain(key => new[]
        {
            "PATH", "SystemRoot", "WINDIR", "TEMP", "TMP", "HOME", "USERPROFILE", "LOCALAPPDATA",
            "PLAYWRIGHT_BROWSERS_PATH",
        }.Contains(key));
        start.Environment["PLAYWRIGHT_BROWSERS_PATH"].Should().Be(options.BrowserDirectory);
        start.Environment.Should().NotContainKey("GAMEGUILD_CODE_WORKER_BROWSER_CHANNEL");
    }

    [Fact]
    public void Launcher_SelectsInstalledChromeOnlyFromDeploymentConfiguration()
    {
        using var installation = new WorkerInstallation();
        var options = installation.Configuration;
        options.BrowserChannel = "chrome";

        var start = CodeGradingWorker.CreateStartInfo(options);

        start.Environment["GAMEGUILD_CODE_WORKER_BROWSER_CHANNEL"].Should().Be("chrome");
        start.UseShellExecute.Should().BeFalse();
        start.ArgumentList.Should().Equal("--max-old-space-size=512", options.ScriptPath,
            options.RuntimeDirectory, options.CdnDirectory);
    }

    [Theory]
    [InlineData("")]
    [InlineData("chromium")]
    [InlineData("chrome-beta")]
    [InlineData("chrome --no-sandbox")]
    [InlineData("--no-sandbox")]
    [InlineData("CHROME")]
    public void Launcher_RejectsUnknownBrowserChannelsAndFlags(string channel)
    {
        using var installation = new WorkerInstallation();
        installation.Configuration.BrowserChannel = channel;

        Action create = () => CodeGradingWorker.CreateStartInfo(installation.Configuration);

        create.Should().Throw<InvalidOperationException>().WithMessage("*trusted Code worker*");
    }

    [Theory]
    [InlineData("node-relative")]
    [InlineData("worker-relative")]
    [InlineData("runtime-relative")]
    [InlineData("cdn-relative")]
    [InlineData("browser-relative")]
    [InlineData("node-name")]
    [InlineData("worker-name")]
    [InlineData("node-missing")]
    [InlineData("worker-missing")]
    [InlineData("runtime-missing")]
    [InlineData("cdn-missing")]
    [InlineData("browser-missing")]
    public void Launcher_RejectsRelativeMissingOrUnexpectedConfiguredFiles(string invalid)
    {
        using var installation = new WorkerInstallation();
        var options = installation.Configuration;
        switch (invalid)
        {
            case "node-relative": options.NodeExecutable = "node"; break;
            case "worker-relative": options.ScriptPath = "worker.mjs"; break;
            case "runtime-relative": options.RuntimeDirectory = "runtime"; break;
            case "cdn-relative": options.CdnDirectory = "cdn"; break;
            case "browser-relative": options.BrowserDirectory = "browsers"; break;
            case "node-name": options.NodeExecutable = installation.CreateFile("other-executable"); break;
            case "worker-name": options.ScriptPath = installation.CreateFile("other-script.mjs"); break;
            case "node-missing": File.Delete(options.NodeExecutable!); break;
            case "worker-missing": File.Delete(options.ScriptPath!); break;
            case "runtime-missing": options.RuntimeDirectory = Path.Combine(installation.Root, "missing-runtime"); break;
            case "cdn-missing": options.CdnDirectory = Path.Combine(installation.Root, "missing-cdn"); break;
            case "browser-missing": options.BrowserDirectory = Path.Combine(installation.Root, "missing-browser"); break;
            default: throw new InvalidOperationException("Unknown launcher test case.");
        }

        Action create = () => CodeGradingWorker.CreateStartInfo(options);
        create.Should().Throw<InvalidOperationException>().WithMessage("*trusted Code worker*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(301)]
    public async Task Worker_RejectsAnUnboundedDeadlineBeforeLaunchingAnything(int deadline)
    {
        using var worker = new CodeGradingWorker(Options.Create(new CodeGradingWorkerOptions
        {
            DeadlineSeconds = deadline,
        }));
        Func<Task> execute = () => worker.ExecuteAsync(Definition(),
            JsonSerializer.SerializeToElement(new { }), CodeToolchainIdentity.Version1, CancellationToken.None);

        await execute.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not configured*");
    }

    [Fact]
    public async Task Worker_HonorsCancellationBeforeInspectingTheInstallation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var worker = new CodeGradingWorker(Options.Create(new CodeGradingWorkerOptions()));
        Func<Task> execute = () => worker.ExecuteAsync(Definition(),
            JsonSerializer.SerializeToElement(new { }), CodeToolchainIdentity.Version1, cancellation.Token);

        await execute.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData("browser startup", "browser-sandbox-unavailable")]
    [InlineData("artifact verification", "artifact-binding-failed")]
    public void Diagnostics_AcceptsOnlyTheKnownInfrastructureContract(string phase, string kind)
    {
        var diagnostic = JsonSerializer.Serialize(new { schemaVersion = 1, phase, kind });
        CodeGradingWorker.ReadFailureDiagnostic(diagnostic).Should().Be((phase, kind));
    }

    [Theory]
    [InlineData("private-source-and-test-name")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("{\"schemaVersion\":1,\"phase\":\"private-test-name\",\"kind\":\"execution-failed\"}")]
    [InlineData("{\"schemaVersion\":1,\"phase\":\"browser startup\",\"kind\":\"private-source\"}")]
    [InlineData("{\"schemaVersion\":1,\"phase\":\"browser startup\",\"kind\":\"browser-startup-failed\",\"error\":\"private-source\"}")]
    [InlineData("{\"schemaVersion\":2,\"phase\":\"browser startup\",\"kind\":\"browser-startup-failed\"}")]
    public void Diagnostics_RedactsUnexpectedRawErrorsAndPrivateContent(string diagnostic)
    {
        CodeGradingWorker.ReadFailureDiagnostic(diagnostic).Should().Be(("unavailable", "unknown"));
    }

    private static CodingAssignmentContent Definition() => new()
    {
        Environment = new CodingEnvironment { Language = "cpp", Tools = "clang" },
        Data = new WorkspaceData { Files = new Dictionary<string, BundleFileMeta>() },
        Tests = new TestSuite { Public = [], Private = [] },
        Grading = new GradingConfig { MaxScore = 100 },
    };

    private sealed class WorkerInstallation : IDisposable
    {
        private readonly string _temporaryRoot = Path.GetFullPath(Path.GetTempPath());
        public string Root { get; }
        public CodeGradingWorkerOptions Configuration { get; }

        public WorkerInstallation()
        {
            Root = Path.Combine(_temporaryRoot, "gg-code-config-" + Guid.NewGuid().ToString("N") + " space;data");
            Directory.CreateDirectory(Root);
            Configuration = new CodeGradingWorkerOptions
            {
                NodeExecutable = CreateFile(OperatingSystem.IsWindows() ? "node.exe" : "node"),
                ScriptPath = CreateFile("worker.mjs"),
                RuntimeDirectory = CreateDirectory("runtime"),
                CdnDirectory = CreateDirectory("cdn"),
                BrowserDirectory = CreateDirectory("browsers"),
            };
        }

        public string CreateFile(string name)
        {
            var fileName = Path.GetFileName(name);
            if (!string.Equals(name, fileName, StringComparison.Ordinal) ||
                name.IndexOfAny(['/', '\\']) >= 0)
            {
                throw new InvalidOperationException("Worker fixture files require a plain filename.");
            }

            var path = Path.Combine(Root, fileName);
            File.WriteAllText(path, string.Empty);
            return path;
        }

        private string CreateDirectory(string name)
        {
            var path = Path.Combine(Root, name);
            Directory.CreateDirectory(path);
            return path;
        }

        public void Dispose()
        {
            var parent = Path.GetDirectoryName(Path.GetFullPath(Root));
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!string.Equals(parent, Path.TrimEndingDirectorySeparator(_temporaryRoot), comparison) ||
                !Path.GetFileName(Root).StartsWith("gg-code-config-", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The owned worker test directory escaped its temporary root.");
            }

            Directory.Delete(Root, recursive: true);
        }
    }
}
