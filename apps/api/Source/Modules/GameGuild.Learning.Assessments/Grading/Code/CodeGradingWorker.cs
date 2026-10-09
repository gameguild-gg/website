using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Courses;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameGuild.Learning.Assessments.Grading.Code;
public sealed class CodeGradingWorkerOptions
{
    public static string Section { get; } = "CodeGradingWorker";
    public string? NodeExecutable { get; set; }
    public string? ScriptPath { get; set; }
    public string? RuntimeDirectory { get; set; }
    public string? CdnDirectory { get; set; }
    public string? BrowserDirectory { get; set; }
    public string? BrowserChannel { get; set; }
    public int DeadlineSeconds { get; set; } = 300;
}

/// <summary>
/// Credential-free process boundary for the server-owned WebAssembly worker.
/// A bounded wait and one active execution prevent compiler requests from
/// exhausting the API. Worker errors leave the academic transaction uncommitted.
/// </summary>
public sealed class CodeGradingWorker(IOptions<CodeGradingWorkerOptions> configured, ILogger<CodeGradingWorker> logger) : ICodeAssessmentExecutor, IDisposable
{
    private static readonly Action<ILogger, int, string, string, Exception?> WorkerFailure =
        LoggerMessage.Define<int, string, string>(LogLevel.Error, new EventId(6991, "CodeWorkerInfrastructureFailure"),
            "Code worker failed with exit {ExitCode} during {Phase}: {FailureKind}.");
    private readonly SemaphoreSlim _slot = new(1, 1);

    public CodeGradingWorker(IOptions<CodeGradingWorkerOptions> configured)
        : this(configured, NullLogger<CodeGradingWorker>.Instance)
    {
    }

    public async Task<CodeExecutionReceipt> ExecuteAsync(CodingAssignmentContent definition, JsonElement files, CodeToolchainIdentity toolchain, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = configured.Value;
        if (options.DeadlineSeconds is < 1 or > 300)
        {
            throw new InvalidOperationException("The trusted Code grading worker is not configured.");
        }

        var start = CreateStartInfo(options);

        if (!await _slot.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The trusted Code grading worker is busy. Retry this submission.");
        }

        try
        {
            var request = JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, definition = JsonSerializer.SerializeToElement(definition, CodeAssessmentContracts.ContentJson), files, toolchain = JsonSerializer.SerializeToElement(toolchain, GradingJson.Options), });
            if (request.Length > 12_500_000)
            {
                throw new InvalidOperationException("Code worker input exceeds its byte budget.");
            }

            var requestHash = Convert.ToHexStringLower(SHA256.HashData(request));
            using var process = new Process
            {
                StartInfo = start
            };
            if (!process.Start())
            {
                throw new InvalidOperationException("Code worker did not start.");
            }

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(options.DeadlineSeconds));
            using var stop = deadline.Token.Register(() => StopOwnedProcess(process));
            try
            {
                var stdout = ReadBoundedAsync(process.StandardOutput.BaseStream, 20_000, deadline);
                var stderr = ReadBoundedAsync(process.StandardError.BaseStream, 20_000, deadline);
                async Task WriteRequestAsync()
                {
                    await process.StandardInput.BaseStream.WriteAsync(request, deadline.Token).ConfigureAwait(false);
                    await process.StandardInput.BaseStream.FlushAsync(deadline.Token).ConfigureAwait(false);
                    process.StandardInput.Close();
                }

                // Observe every pipe task and stop a flooding child immediately.
                // Waiting for exit before observing a full pipe can deadlock.
                await Task.WhenAll(stdout, stderr, WriteRequestAsync(), process.WaitForExitAsync(deadline.Token)).ConfigureAwait(false);
                var output = await stdout.ConfigureAwait(false);
                var diagnostic = await stderr.ConfigureAwait(false);
                if (process.ExitCode != 0)
                {
                    var failure = ReadFailureDiagnostic(diagnostic);
                    WorkerFailure(logger, process.ExitCode, failure.Phase, failure.Kind, null);
                    throw new InvalidOperationException("The trusted Code grading worker failed. Retry this submission.");
                }

                using var receipt = JsonDocument.Parse(output);
                var root = receipt.RootElement;
                if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 4 || !root.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal).SetEquals(["schemaVersion", "requestHash", "manifestHash", "passed"]) || root.GetProperty("schemaVersion").GetInt32() != 1 || root.GetProperty("requestHash").GetString() != requestHash)
                {
                    throw new InvalidOperationException("The Code worker receipt does not match its frozen request.");
                }

                var manifestHash = root.GetProperty("manifestHash").GetString();
                if (manifestHash is null || manifestHash.Length != 64 || manifestHash.Any(character => !char.IsAsciiHexDigitLower(character)))
                {
                    throw new InvalidOperationException("The Code worker receipt requires an artifact manifest hash.");
                }

                var passed = root.GetProperty("passed").EnumerateArray().Select(value => value.GetBoolean()).ToArray();
                var expectedTests = definition.Tests.Public.Count + definition.Tests.Private.Count;
                if (passed.Length != expectedTests || passed.Length == 0 || passed.Length > CodeAssessmentContracts.MaxTests)
                {
                    throw new InvalidOperationException("The Code worker test receipt is incomplete.");
                }

                return new CodeExecutionReceipt(passed, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(output))));
            }
            catch (OperationCanceledException)when (!cancellationToken.IsCancellationRequested)
            {
                throw new InvalidOperationException("The trusted Code grading worker exceeded its deadline. Retry this submission.");
            }
            finally
            {
                StopOwnedProcess(process);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        finally
        {
            _slot.Release();
        }
    }

    internal static (string Phase, string Kind) ReadFailureDiagnostic(string diagnostic)
    {
        try
        {
            using var document = JsonDocument.Parse(diagnostic);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 3 ||
                !root.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal)
                    .SetEquals(["schemaVersion", "phase", "kind"]) ||
                root.GetProperty("schemaVersion").ValueKind != JsonValueKind.Number ||
                !root.GetProperty("schemaVersion").TryGetInt32(out var schema) || schema != 1 ||
                root.GetProperty("phase").ValueKind != JsonValueKind.String ||
                root.GetProperty("kind").ValueKind != JsonValueKind.String)
            {
                return ("unavailable", "unknown");
            }

            var phase = root.GetProperty("phase").GetString()!;
            var kind = root.GetProperty("kind").GetString()!;
            return (phase, kind) is
                ("input", "invalid-request") or
                ("artifact verification", "artifact-binding-failed") or
                ("runtime server", "runtime-server-failed") or
                ("browser startup", "browser-startup-failed") or
                ("browser startup", "browser-sandbox-unavailable") or
                ("WebAssembly evaluation", "execution-failed")
                ? (phase, kind) : ("unavailable", "unknown");
        }
        catch (JsonException)
        {
            return ("unavailable", "unknown");
        }
    }

    internal static ProcessStartInfo CreateStartInfo(CodeGradingWorkerOptions options)
    {
        var node = RequireAbsoluteFile(options.NodeExecutable, OperatingSystem.IsWindows() ? "node.exe" : "node");
        var script = RequireAbsoluteFile(options.ScriptPath, "worker.mjs");
        var runtime = RequireAbsoluteDirectory(options.RuntimeDirectory);
        var cdn = RequireAbsoluteDirectory(options.CdnDirectory);
        var browser = string.IsNullOrWhiteSpace(options.BrowserDirectory)
            ? null : RequireAbsoluteDirectory(options.BrowserDirectory);
        if (options.BrowserChannel is not (null or "chrome"))
        {
            throw new InvalidOperationException("The trusted Code worker supports only its bundled browser or the installed Chrome channel.");
        }

        // Only deployment-owned configuration selects these validated files.
        // Learner content is bounded JSON on stdin; it cannot select the executable
        // or arguments. ArgumentList and UseShellExecute=false preserve that boundary
        // even when the installation directory contains spaces or shell metacharacters.
        var start = new ProcessStartInfo
        {
            // Reviewed deployment configuration; see CodeGradingWorkerConfigurationTests
            // and coding-assessment-merge-acceptance-20261006.md. No learner path reaches this sink.
            FileName = node, // nosemgrep: csharp_injection_rule-CommandInjection, Semgrep_csharp_injection_rule-CommandInjection
            WorkingDirectory = Path.GetDirectoryName(script)!,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("--max-old-space-size=512");
        start.ArgumentList.Add(script);
        start.ArgumentList.Add(runtime);
        start.ArgumentList.Add(cdn);
        start.Environment.Clear();
        foreach (var key in new[] { "PATH", "SystemRoot", "WINDIR", "TEMP", "TMP", "HOME", "USERPROFILE", "LOCALAPPDATA" })
        {
            if (Environment.GetEnvironmentVariable(key) is { } value)
            {
                start.Environment[key] = value;
            }
        }

        if (browser is not null)
        {
            start.Environment["PLAYWRIGHT_BROWSERS_PATH"] = browser;
        }

        if (options.BrowserChannel is not null)
        {
            start.Environment["GAMEGUILD_CODE_WORKER_BROWSER_CHANNEL"] = options.BrowserChannel;
        }

        return start;
    }

    private static string RequireAbsoluteFile(string? path, string expectedName)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) ||
            !string.Equals(Path.GetFileName(path), expectedName, comparison) || !File.Exists(path))
        {
            throw new InvalidOperationException("The trusted Code worker requires an existing absolute " + expectedName + " path.");
        }

        return Path.GetFullPath(path);
    }

    private static string RequireAbsoluteDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || !Directory.Exists(path))
        {
            throw new InvalidOperationException("The trusted Code worker requires existing absolute asset directories.");
        }

        return Path.GetFullPath(path);
    }

    internal static async Task<string> ReadBoundedAsync(Stream reader, int maximum, CancellationTokenSource deadline)
    {
        var buffer = new byte[1024];
        using var result = new MemoryStream();
        int length;
        while ((length = await reader.ReadAsync(buffer.AsMemory(), deadline.Token).ConfigureAwait(false)) != 0)
        {
            if (result.Length + length > maximum)
            {
                await deadline.CancelAsync().ConfigureAwait(false);
                throw new InvalidOperationException("Code worker output exceeds its byte budget.");
            }

            result.Write(buffer, 0, length);
        }

        return new UTF8Encoding(false, true).GetString(result.ToArray());
    }

    private static void StopOwnedProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The cancellation callback and finally block can race an already exited
            // process. There is no live owned child left to terminate in that case.
        }
    }

    public void Dispose() => _slot.Dispose();
}
