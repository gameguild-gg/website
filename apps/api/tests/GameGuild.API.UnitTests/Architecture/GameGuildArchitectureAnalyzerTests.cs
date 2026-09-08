using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using GameGuild.ArchitectureAnalyzers;

namespace GameGuild.API.UnitTests.Architecture;

public sealed class GameGuildArchitectureAnalyzerTests
{
    [Theory]
    [InlineData("Publish")]
    [InlineData("PublishAsync")]
    public async Task ManualPublisherAfterPersistence_IsRejected(string methodName)
    {
        var source = $$"""
            namespace GameGuild.CQRS { public interface IPublisher { void {{methodName}}(object message); } }
            public sealed class Handler(GameGuild.CQRS.IPublisher publisher)
            {
                private void SaveChangesAsync() { }
                public void Execute() { SaveChangesAsync(); publisher.{{methodName}}(new object()); }
            }
            """;
        var diagnostics = await AnalyzeAsync(source);
        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == GameGuildArchitectureAnalyzer.ManualPublishId);
    }

    [Fact]
    public async Task LocalPersistenceHelperNamedPublishAsync_IsNotMistakenForEventPublisher()
    {
        const string source = """
            public sealed class KycOrchestrator
            {
                private void SaveChangesAsync() { }
                private void PublishAsync() { }
                public void Execute() { SaveChangesAsync(); PublishAsync(); }
            }
            """;
        var diagnostics = await AnalyzeAsync(source);
        diagnostics.Should().NotContain(diagnostic => diagnostic.Id == GameGuildArchitectureAnalyzer.ManualPublishId);
    }

    [Fact]
    public async Task MutationAction_DelegatingToSameTypeHelperThatSendsCommand_IsAccepted()
    {
        const string source = """
            using System;
            namespace Microsoft.AspNetCore.Mvc { public sealed class HttpPostAttribute : Attribute { } }
            namespace GameGuild.CQRS
            {
                public interface ICommand { }
                public interface ISender { void Send(object command); }
            }
            public sealed class ChangeStateCommand : GameGuild.CQRS.ICommand { }
            public sealed class Controller(GameGuild.CQRS.ISender sender)
            {
                [Microsoft.AspNetCore.Mvc.HttpPost]
                public void Mutate() => SendCommand();
                private void SendCommand() => sender.Send(new ChangeStateCommand());
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        diagnostics.Should().NotContain(diagnostic =>
            diagnostic.Id == GameGuildArchitectureAnalyzer.ControllerPersistenceId);
    }

    [Fact]
    public async Task MutationAction_SendingOnlyAQuery_IsRejected()
    {
        const string source = """
            using System;
            namespace Microsoft.AspNetCore.Mvc { public sealed class HttpPostAttribute : Attribute { } }
            namespace GameGuild.CQRS
            {
                public interface IQuery<T> { }
                public interface ISender { void Send(object request); }
            }
            public sealed class ReadStateQuery : GameGuild.CQRS.IQuery<int> { }
            public sealed class Controller(GameGuild.CQRS.ISender sender)
            {
                [Microsoft.AspNetCore.Mvc.HttpPost]
                public void Mutate() => sender.Send(new ReadStateQuery());
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Id == GameGuildArchitectureAnalyzer.ControllerPersistenceId);
    }

    [Theory]
    [InlineData("Read-only compatibility operation", false)]
    [InlineData("", true)]
    public async Task PostShapedReadOnlyAction_RequiresAnExplicitNonEmptyReason(string reason, bool expectedDiagnostic)
    {
        var source = $$"""
            using System;
            namespace Microsoft.AspNetCore.Mvc { public sealed class HttpPostAttribute : Attribute { } }
            public sealed class NoBusinessMutationEndpointAttribute : Attribute
            {
                public NoBusinessMutationEndpointAttribute(string reason) { }
            }
            public sealed class Controller
            {
                [NoBusinessMutationEndpoint("{{reason}}")] 
                [Microsoft.AspNetCore.Mvc.HttpPost]
                public void Check() { }
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        diagnostics.Any(diagnostic => diagnostic.Id == GameGuildArchitectureAnalyzer.ControllerPersistenceId)
            .Should().Be(expectedDiagnostic);
    }

    [Fact]
    public async Task MutationAction_DelegatingToService_IsRejected()
    {
        const string source = """
            using System;
            namespace Microsoft.AspNetCore.Mvc { public sealed class HttpPostAttribute : Attribute { } }
            public sealed class MutationService { public void Mutate() { } }
            public sealed class Controller(MutationService service)
            {
                [Microsoft.AspNetCore.Mvc.HttpPost]
                public void Mutate() => service.Mutate();
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Id == GameGuildArchitectureAnalyzer.ControllerPersistenceId);
    }

    [Fact]
    public async Task ExplicitlyUnavailableMutationAction_IsExcludedFromSenderRule()
    {
        const string source = """
            using System;
            namespace Microsoft.AspNetCore.Mvc { public sealed class HttpPostAttribute : Attribute { } }
            public sealed class UnavailableEndpointAttribute : Attribute { }
            public sealed class Controller
            {
                [UnavailableEndpoint]
                [Microsoft.AspNetCore.Mvc.HttpPost]
                public void Mutate() { }
            }
            """;

        var diagnostics = await AnalyzeAsync(source);

        diagnostics.Should().NotContain(diagnostic =>
            diagnostic.Id == GameGuildArchitectureAnalyzer.ControllerPersistenceId);
    }

    [Fact]
    public async Task CommandDeclaredInContractsAssembly_DefersHandlerCardinalityToSolutionGate()
    {
        const string source = """
            namespace GameGuild.CQRS { public interface ICommand<T> { } }
            public sealed record CrossModuleCommand : GameGuild.CQRS.ICommand<int>;
            """;

        var diagnostics = await AnalyzeAsync(source, "Sample.Contracts");

        diagnostics.Should().NotContain(diagnostic =>
            diagnostic.Id == GameGuildArchitectureAnalyzer.HandlerCardinalityId);
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        string source,
        string assemblyName = "AnalyzerTest")
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location))
            .Cast<MetadataReference>()
            .ToArray();
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new GameGuildArchitectureAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
    }
}
