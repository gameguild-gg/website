using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GameGuild.ArchitectureAnalyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GameGuildArchitectureAnalyzer : DiagnosticAnalyzer
{
    public const string MissingContractId = "MEARCH001";
    public const string UnversionedEventId = "MEARCH002";
    public const string ManualPublishId = "MEARCH003";
    public const string ControllerPersistenceId = "MEARCH004";
    public const string HandlerCardinalityId = "MEARCH005";
    public const string ExecutableDelegateCommandId = "MEARCH006";
    public const string ControllerPersistenceBypassId = "MEARCH007";

    private static readonly DiagnosticDescriptor MissingContract = new(
        MissingContractId,
        "Mutating command requires an event contract",
        "Command '{0}' has no assembly-level UseCaseEventContract",
        "Architecture",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor UnversionedEvent = new(
        UnversionedEventId,
        "Durable events require a versioned event name",
        "Durable event '{0}' must expose a literal event name ending in .v<number>",
        "Architecture",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor ManualPublish = new(
        ManualPublishId,
        "Do not publish manually after persistence",
        "Method '{0}' publishes after SaveChanges; raise a durable event before persistence instead",
        "Architecture",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor ControllerPersistence = new(
        ControllerPersistenceId,
        "Controller mutations must use ISender",
        "Mutation action '{0}' does not send a command through ISender",
        "Architecture",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor HandlerCardinality = new(
        HandlerCardinalityId,
        "Commands require exactly one handler",
        "Command '{0}' has {1} handlers in its assembly; exactly one is required",
        "Architecture",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor ExecutableDelegateCommand = new(
        ExecutableDelegateCommandId,
        "Commands must not carry executable delegates",
        "Command '{0}' carries executable delegate parameter '{1}'",
        "Architecture",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor ControllerPersistenceBypass = new(
        ControllerPersistenceBypassId,
        "Controller mutations must not persist outside command handlers",
        "Mutation action '{0}' performs persistence outside its command handler",
        "Architecture",
        DiagnosticSeverity.Error,
        true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [MissingContract, UnversionedEvent, ManualPublish, ControllerPersistence, HandlerCardinality,
            ExecutableDelegateCommand, ControllerPersistenceBypass];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(StartCompilation);
    }

    private static void StartCompilation(CompilationStartAnalysisContext context)
    {
        var commands = new ConcurrentDictionary<INamedTypeSymbol, Location>(SymbolEqualityComparer.Default);
        var handlersByCommand = new ConcurrentDictionary<INamedTypeSymbol, ConcurrentDictionary<INamedTypeSymbol, byte>>(
            SymbolEqualityComparer.Default);
        var contracts = GetContractCommandTypes(context.Compilation.Assembly);
        var unavailableCommands = GetUnavailableCommandTypes(context.Compilation.Assembly);

        context.RegisterSymbolAction(symbolContext =>
        {
            var type = (INamedTypeSymbol)symbolContext.Symbol;
            if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct) || type.IsAbstract)
                return;

            if (type.AllInterfaces.Any(IsCommandInterface))
            {
                commands.TryAdd(type, type.Locations.FirstOrDefault() ?? Location.None);
                var delegateParameter = type.InstanceConstructors
                    .SelectMany(constructor => constructor.Parameters)
                    .FirstOrDefault(parameter => parameter.Type.TypeKind == TypeKind.Delegate);
                if (delegateParameter is not null)
                    symbolContext.ReportDiagnostic(Diagnostic.Create(
                        ExecutableDelegateCommand,
                        type.Locations.FirstOrDefault(),
                        type.ToDisplayString(),
                        delegateParameter.Name));
            }

            foreach (var contract in type.AllInterfaces.Where(IsRequestHandlerInterface))
            {
                if (contract.TypeArguments[0] is INamedTypeSymbol command)
                {
                    var handlers = handlersByCommand.GetOrAdd(
                        command,
                        _ => new ConcurrentDictionary<INamedTypeSymbol, byte>(SymbolEqualityComparer.Default));
                    handlers.TryAdd(type, 0);
                }
            }

            if (Implements(type, "GameGuild.IDurableIntegrationEvent") && !HasVersionedEventName(type))
                symbolContext.ReportDiagnostic(Diagnostic.Create(
                    UnversionedEvent,
                    type.Locations.FirstOrDefault(),
                    type.ToDisplayString()));
        }, SymbolKind.NamedType);

        context.RegisterSyntaxNodeAction(AnalyzeMethod, Microsoft.CodeAnalysis.CSharp.SyntaxKind.MethodDeclaration);
        context.RegisterCompilationEndAction(endContext =>
        {
            var isContractsAssembly = context.Compilation.AssemblyName?.EndsWith(
                ".Contracts",
                StringComparison.Ordinal) == true;
            foreach (var command in commands)
            {
                if (!contracts.Contains(command.Key, SymbolEqualityComparer.Default))
                    endContext.ReportDiagnostic(Diagnostic.Create(
                        MissingContract,
                        command.Value,
                        command.Key.ToDisplayString()));

                var count = handlersByCommand.TryGetValue(command.Key, out var registeredHandlers)
                    ? registeredHandlers.Count
                    : 0;
                if (unavailableCommands.Contains(command.Key, SymbolEqualityComparer.Default))
                    count++;
                // A contracts-only assembly deliberately declares commands whose handler lives in a
                // consuming module. A single-compilation analyzer cannot see that downstream type;
                // the solution-wide architecture test enforces cardinality across loaded assemblies.
                if (!isContractsAssembly && count != 1)
                    endContext.ReportDiagnostic(Diagnostic.Create(
                        HandlerCardinality,
                        command.Value,
                        command.Key.ToDisplayString(),
                        count));
            }
        });
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        var invocations = method.DescendantNodes().OfType<InvocationExpressionSyntax>().ToArray();
        var saveIndex = Array.FindIndex(invocations, invocation => InvocationName(invocation).StartsWith("SaveChanges", StringComparison.Ordinal));
        if (saveIndex >= 0 && invocations.Skip(saveIndex + 1).Any(invocation =>
                IsManualEventPublish(invocation, context.SemanticModel)))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ManualPublish,
                method.Identifier.GetLocation(),
                method.Identifier.ValueText));
        }

        var isMutationAction = method.AttributeLists.SelectMany(list => list.Attributes)
            .Select(attribute => attribute.Name.ToString())
            .Any(name => name.EndsWith("HttpPost", StringComparison.Ordinal)
                         || name.EndsWith("HttpPut", StringComparison.Ordinal)
                         || name.EndsWith("HttpPatch", StringComparison.Ordinal)
                         || name.EndsWith("HttpDelete", StringComparison.Ordinal));
        if (!isMutationAction)
            return;

        var isExplicitlyUnavailable = method.AttributeLists.SelectMany(list => list.Attributes)
            .Any(attribute => attribute.Name.ToString().EndsWith("UnavailableEndpoint", StringComparison.Ordinal)
                              || attribute.Name.ToString().EndsWith("UnavailableEndpointAttribute", StringComparison.Ordinal));
        if (isExplicitlyUnavailable)
            return;

        var noBusinessMutation = method.AttributeLists.SelectMany(list => list.Attributes)
            .FirstOrDefault(attribute => attribute.Name.ToString().EndsWith("NoBusinessMutationEndpoint", StringComparison.Ordinal)
                                         || attribute.Name.ToString().EndsWith("NoBusinessMutationEndpointAttribute", StringComparison.Ordinal));
        if (noBusinessMutation?.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax reasonLiteral
            && reasonLiteral.Token.Value is string reason
            && !string.IsNullOrWhiteSpace(reason))
            return;

        var containingType = context.SemanticModel.GetDeclaredSymbol(method)?.ContainingType;
        if (containingType is not null && PerformsPersistence(
                method,
                context.SemanticModel,
                containingType,
                new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default)))
            context.ReportDiagnostic(Diagnostic.Create(
                ControllerPersistenceBypass,
                method.Identifier.GetLocation(),
                method.Identifier.ValueText));

        var sendsCommand = containingType is not null
            && SendsCommand(
                method,
                context.SemanticModel,
                containingType,
                new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default));
        if (!sendsCommand)
            context.ReportDiagnostic(Diagnostic.Create(
                ControllerPersistence,
                method.Identifier.GetLocation(),
                method.Identifier.ValueText));
    }

    private static bool PerformsPersistence(
        MethodDeclarationSyntax method,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        HashSet<IMethodSymbol> visited)
    {
        foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (InvocationName(invocation).StartsWith("SaveChanges", StringComparison.Ordinal))
                return true;

            if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol calledMethod
                || !SymbolEqualityComparer.Default.Equals(calledMethod.ContainingType, containingType)
                || !visited.Add(calledMethod))
                continue;

            foreach (var syntaxReference in calledMethod.DeclaringSyntaxReferences)
            {
                if (syntaxReference.GetSyntax() is MethodDeclarationSyntax calledDeclaration
                    && calledDeclaration.SyntaxTree == semanticModel.SyntaxTree
                    && PerformsPersistence(calledDeclaration, semanticModel, containingType, visited))
                    return true;
            }
        }

        return false;
    }

    private static bool SendsCommand(
        MethodDeclarationSyntax method,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        HashSet<IMethodSymbol> visited)
    {
        foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (InvocationName(invocation) == "Send"
                && Receiver(invocation) is { } receiver
                && IsSender(semanticModel.GetTypeInfo(receiver).Type)
                && SendsCommandArgument(invocation, semanticModel))
                return true;

            if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol calledMethod
                || !SymbolEqualityComparer.Default.Equals(calledMethod.ContainingType, containingType)
                || !visited.Add(calledMethod))
                continue;

            foreach (var syntaxReference in calledMethod.DeclaringSyntaxReferences)
            {
                if (syntaxReference.GetSyntax() is not MethodDeclarationSyntax calledDeclaration)
                    continue;
                if (calledDeclaration.SyntaxTree != semanticModel.SyntaxTree)
                    continue;
                if (SendsCommand(calledDeclaration, semanticModel, containingType, visited))
                    return true;
            }
        }

        return false;
    }

    private static ImmutableHashSet<INamedTypeSymbol> GetContractCommandTypes(IAssemblySymbol assembly)
    {
        var builder = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var attribute in assembly.GetAttributes().Where(attribute =>
                     attribute.AttributeClass?.ToDisplayString() == "GameGuild.UseCaseEventContractAttribute"))
        {
            if (attribute.ConstructorArguments.FirstOrDefault().Value is INamedTypeSymbol command)
                builder.Add(command);
        }

        return builder.ToImmutable();
    }

    private static ImmutableHashSet<INamedTypeSymbol> GetUnavailableCommandTypes(IAssemblySymbol assembly)
    {
        var builder = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var attribute in assembly.GetAttributes().Where(attribute =>
                     attribute.AttributeClass?.ToDisplayString() == "GameGuild.UseCaseEventContractAttribute"))
        {
            var unavailableReason = attribute.NamedArguments.FirstOrDefault(argument =>
                argument.Key == "UnavailableReason").Value.Value as string;
            if (!string.IsNullOrWhiteSpace(unavailableReason)
                && attribute.ConstructorArguments.FirstOrDefault().Value is INamedTypeSymbol command)
                builder.Add(command);
        }

        return builder.ToImmutable();
    }

    private static bool IsCommandInterface(INamedTypeSymbol @interface) =>
        @interface.OriginalDefinition.Name == "ICommand"
        && @interface.OriginalDefinition.ContainingNamespace.ToDisplayString() == "GameGuild.CQRS";

    private static bool IsRequestHandlerInterface(INamedTypeSymbol @interface) =>
        @interface.TypeArguments.Length > 0
        && @interface.OriginalDefinition.Name is "ICommandHandler" or "IRequestHandler"
        && @interface.OriginalDefinition.ContainingNamespace.ToDisplayString() == "GameGuild.CQRS";

    private static bool Implements(INamedTypeSymbol type, string interfaceName) =>
        type.AllInterfaces.Any(@interface =>
            @interface.OriginalDefinition.ToDisplayString() == interfaceName);

    private static bool HasVersionedEventName(INamedTypeSymbol type)
    {
        foreach (var syntaxReference in type.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is not TypeDeclarationSyntax declaration)
                continue;
            var eventNameProperty = declaration.Members.OfType<PropertyDeclarationSyntax>()
                .FirstOrDefault(property => property.Identifier.ValueText == "EventName");
            var literal = eventNameProperty?.ExpressionBody?.Expression as LiteralExpressionSyntax
                          ?? eventNameProperty?.Initializer?.Value as LiteralExpressionSyntax;
            if (literal?.Token.ValueText is { } eventName
                && Regex.IsMatch(eventName, @"\.v\d+$", RegexOptions.CultureInvariant))
                return true;
        }

        return false;
    }

    private static string InvocationName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        GenericNameSyntax generic => generic.Identifier.ValueText,
        _ => string.Empty
    };

    private static bool IsManualEventPublish(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        if (InvocationName(invocation) is not ("Publish" or "PublishAsync"))
            return false;

        // Match the event publisher contract, not unrelated helpers named PublishAsync
        // (for example, KYC ledger persistence). Concrete implementations are included.
        var publisherType = Receiver(invocation) is { } receiver
            ? semanticModel.GetTypeInfo(receiver).Type as INamedTypeSymbol
            : (semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol)?.ContainingType;
        return publisherType is not null &&
            (publisherType.ToDisplayString() == "GameGuild.CQRS.IPublisher" ||
             publisherType.AllInterfaces.Any(type => type.ToDisplayString() == "GameGuild.CQRS.IPublisher"));
    }

    private static ExpressionSyntax? Receiver(InvocationExpressionSyntax invocation) =>
        invocation.Expression is MemberAccessExpressionSyntax member ? member.Expression : null;

    private static bool IsSender(ITypeSymbol? type) =>
        type is INamedTypeSymbol named
        && ((named.Name == "ISender" && named.ContainingNamespace.ToDisplayString() == "GameGuild.CQRS")
            || named.AllInterfaces.Any(@interface => @interface.Name == "ISender"
                && @interface.ContainingNamespace.ToDisplayString() == "GameGuild.CQRS"));

    private static bool SendsCommandArgument(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        var argument = invocation.ArgumentList.Arguments.FirstOrDefault();
        if (argument is null)
            return false;

        var argumentType = semanticModel.GetTypeInfo(argument.Expression).Type;
        return IsCommandType(argumentType);
    }

    private static bool IsCommandType(ITypeSymbol? type) => type switch
    {
        INamedTypeSymbol named => IsCommandInterface(named) || named.AllInterfaces.Any(IsCommandInterface),
        ITypeParameterSymbol parameter => parameter.ConstraintTypes.Any(IsCommandType),
        _ => false
    };
}
