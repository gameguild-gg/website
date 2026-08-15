using System.Reflection;
using System.Reflection.Emit;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using GameGuild.API.Setup;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Tenants;
using Moq;

namespace GameGuild.API.UnitTests.Core;

public sealed class LayerExtensionCoverageTests
{
    [Fact]
    public void AddApplicationLayer_WithBuilderOptions_RegistersAndReturnsBuilder()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });

        var result = builder.AddApplicationLayer(options =>
        {
            options.ModuleConfiguration.EnabledModules = [];
            options.LogHandlerStatistics = false;
        });

        result.Should().BeSameAs(builder);
        builder.Services.Should().NotBeEmpty();
    }

    [Fact]
    public void AddInfrastructureLayer_WithBuilderOptions_RegistersAndReturnsBuilder()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });
        builder.Configuration["InfrastructureLayer:EnableDatabase"] = "false";

        var result = builder.AddInfrastructureLayer(options =>
        {
            options.UseInMemoryDatabase = true;
        });

        result.Should().BeSameAs(builder);
        builder.Services.Should().NotBeEmpty();
    }

    [Fact]
    public void AddDatabase_WhenConnectionIsMissing_ThrowsClearError()
    {
        var method = typeof(InfrastructureLayerExtensions).GetMethod(
            "AddDatabase",
            BindingFlags.Static | BindingFlags.NonPublic);
        method.Should().NotBeNull();

        var action = () => method!.Invoke(
            null,
            [new ServiceCollection(), new ConfigurationBuilder().Build(), null]);

        action.Should().Throw<TargetInvocationException>()
            .WithInnerException<InvalidOperationException>()
            .WithMessage("*Connection string 'DefaultConnection' not found*");
    }

    [Fact]
    public void CountHandlersAndValidators_WhenAssemblyPartiallyLoads_CountsAvailableTypes()
    {
        var assembly = CreatePartiallyLoadedAssembly(typeof(LoadableHandlerValidator));

        var result = InvokePrivate<(int handlers, int validators)>(
            typeof(ApplicationLayerExtensions),
            "CountHandlersAndValidators",
            assembly.Object);

        result.handlers.Should().Be(1);
        result.validators.Should().Be(1);
    }

    [Fact]
    public void LogHandlerStatistics_WhenAssemblyNameIsMissing_UsesUnknownModuleName()
    {
        var assembly = new Mock<Assembly>();
        assembly.Setup(value => value.GetName()).Returns(new AssemblyName());
        assembly.Setup(value => value.GetTypes()).Returns([]);
        var namedAssembly = new Mock<Assembly>();
        namedAssembly.Setup(value => value.GetName()).Returns(new AssemblyName("GameGuild.Coverage"));
        namedAssembly.Setup(value => value.GetTypes()).Returns([]);
        var logger = new Mock<ILogger>();

        InvokePrivate<object?>(
            typeof(ApplicationLayerExtensions),
            "LogHandlerStatistics",
            new[] { assembly.Object, namedAssembly.Object },
            new ModuleConfiguration(),
            logger.Object);

        logger.Verify(
            value => value.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("Unknown", StringComparison.Ordinal)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void GetLoadablePublicTypes_WhenAssemblyPartiallyLoads_ReturnsPublicConcreteTypes()
    {
        var assembly = CreatePartiallyLoadedAssembly(
            typeof(InfrastructureLoadableType),
            typeof(InfrastructureAbstractType),
            typeof(LoadableHandlerValidator));
        var logger = new Mock<ILogger>();

        var types = InvokePrivate<List<Type>>(
            typeof(InfrastructureLayerExtensions),
            "GetLoadablePublicTypes",
            assembly.Object,
            logger.Object);

        types.Should().ContainSingle().Which.Should().Be(typeof(InfrastructureLoadableType));
        logger.Verify(
            value => value.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((_, _) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void AddRepositories_RegistersReadersAndReplacesFailClosedMembershipFallback()
    {
        var (readerInterface, readerImplementation) = CreateReaderAssembly();
        var services = new ServiceCollection();
        services.AddScoped<ITenantMembershipChecker, FailClosedTenantMembershipChecker>();
        var logger = new Mock<ILogger>();

        InvokePrivate<object?>(
            typeof(InfrastructureLayerExtensions),
            "AddRepositories",
            services,
            logger.Object);

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == readerInterface && descriptor.ImplementationType == readerImplementation);
        services.Where(descriptor => descriptor.ServiceType == typeof(ITenantMembershipChecker))
            .Should().ContainSingle()
            .Which.ImplementationType.Should().Be<TenantMembershipChecker>();
    }

    [Fact]
    public void ShouldSkipServiceRegistration_HandlesMissingFallbackAndExplicitRegistrations()
    {
        var logger = new Mock<ILogger>();
        var empty = new ServiceCollection();
        var fallback = new ServiceCollection();
        fallback.AddScoped<ITenantMembershipChecker, FailClosedTenantMembershipChecker>();
        var explicitRegistration = new ServiceCollection();
        explicitRegistration.AddScoped<ITenantMembershipChecker, TenantMembershipChecker>();

        InvokePrivate<bool>(
                typeof(InfrastructureLayerExtensions),
                "ShouldSkipServiceRegistration",
                empty,
                typeof(ITenantMembershipChecker),
                logger.Object)
            .Should().BeFalse();
        InvokePrivate<bool>(
                typeof(InfrastructureLayerExtensions),
                "ShouldSkipServiceRegistration",
                fallback,
                typeof(ITenantMembershipChecker),
                logger.Object)
            .Should().BeFalse();
        fallback.Should().NotContain(descriptor => descriptor.ServiceType == typeof(ITenantMembershipChecker));
        InvokePrivate<bool>(
                typeof(InfrastructureLayerExtensions),
                "ShouldSkipServiceRegistration",
                explicitRegistration,
                typeof(ITenantMembershipChecker),
                logger.Object)
            .Should().BeTrue();
    }

    [Fact]
    public void AddRepositories_WhenCandidateHasNoPublicConstructor_ShouldRemainUnregistered()
    {
        var implementation = CreateUnmatchedServiceWithoutPublicConstructor();
        var services = new ServiceCollection();
        var logger = new Mock<ILogger>();

        InvokePrivate<object?>(
            typeof(InfrastructureLayerExtensions),
            "AddRepositories",
            services,
            logger.Object);

        services.Should().NotContain(descriptor => descriptor.ImplementationType == implementation);
    }

    [Fact]
    public void AddRepositories_WhenCandidateImplementsSkippableInterfaces_ShouldIgnoreThem()
    {
        var implementation = CreateServiceWithSkippableInterfaces();
        var services = new ServiceCollection();
        var logger = new Mock<ILogger>();

        InvokePrivate<object?>(
            typeof(InfrastructureLayerExtensions),
            "AddRepositories",
            services,
            logger.Object);

        services.Should().NotContain(descriptor => descriptor.ImplementationType == implementation);
    }

    [Fact]
    public void AddRepositories_HandlesNonPrefixedInterfacesAndRepositoryWithoutPublicConstructor()
    {
        var nonPrefixedImplementation = CreateServiceWithNonPrefixedInterfaces();
        var (repositoryInterface, repositoryImplementation) = CreateRepositoryWithoutPublicConstructor();
        var services = new ServiceCollection();
        var logger = new Mock<ILogger>();

        InvokePrivate<object?>(
            typeof(InfrastructureLayerExtensions),
            "AddRepositories",
            services,
            logger.Object);

        services.Should().NotContain(descriptor => descriptor.ImplementationType == nonPrefixedImplementation);
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == repositoryInterface &&
            descriptor.ImplementationType == repositoryImplementation);
    }

    [Fact]
    public void FormatInterfaceName_CoversPrefixDecisionStates()
    {
        InvokePrivate<string>(typeof(InfrastructureLayerExtensions), "FormatInterfaceName", "I")
            .Should().Be("I");
        InvokePrivate<string>(typeof(InfrastructureLayerExtensions), "FormatInterfaceName", "IuserService")
            .Should().Be("Iuser Service");
        InvokePrivate<string>(typeof(InfrastructureLayerExtensions), "FormatInterfaceName", "IUserRepository")
            .Should().Be("User Repository");
    }

    private static Mock<Assembly> CreatePartiallyLoadedAssembly(params Type?[] availableTypes)
    {
        var exception = new ReflectionTypeLoadException(
            availableTypes.Append(null).ToArray(),
            [new TypeLoadException("Unavailable dependency"), null]);
        var assembly = new Mock<Assembly>();
        assembly.Setup(value => value.GetTypes()).Throws(exception);
        assembly.Setup(value => value.GetName()).Returns(new AssemblyName("PartiallyLoaded"));
        return assembly;
    }

    private static (Type Interface, Type Implementation) CreateReaderAssembly()
    {
        var assemblyName = new AssemblyName($"GameGuild.Coverage{Guid.NewGuid():N}AI");
        var assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule(assemblyName.Name!);
        var interfaceBuilder = module.DefineType(
            "ICoverageReader",
            TypeAttributes.Interface | TypeAttributes.Abstract | TypeAttributes.Public);
        var readerInterface = interfaceBuilder.CreateType()!;
        var implementationBuilder = module.DefineType(
            "CoverageReader",
            TypeAttributes.Class | TypeAttributes.Public);
        implementationBuilder.AddInterfaceImplementation(readerInterface);
        implementationBuilder.DefineDefaultConstructor(MethodAttributes.Public);
        var readerImplementation = implementationBuilder.CreateType()!;
        return (readerInterface, readerImplementation);
    }

    private static Type CreateUnmatchedServiceWithoutPublicConstructor()
    {
        var assemblyName = new AssemblyName($"GameGuild.Coverage{Guid.NewGuid():N}AI");
        var assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule(assemblyName.Name!);
        var interfaceBuilder = module.DefineType(
            "ICoverageFeature",
            TypeAttributes.Interface | TypeAttributes.Abstract | TypeAttributes.Public);
        var featureInterface = interfaceBuilder.CreateType()!;
        var implementationBuilder = module.DefineType(
            "UnmatchedService",
            TypeAttributes.Class | TypeAttributes.Public);
        implementationBuilder.AddInterfaceImplementation(featureInterface);
        var constructor = implementationBuilder.DefineConstructor(
            MethodAttributes.Private,
            CallingConventions.Standard,
            Type.EmptyTypes);
        var generator = constructor.GetILGenerator();
        generator.Emit(OpCodes.Ldarg_0);
        generator.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes)!);
        generator.Emit(OpCodes.Ret);
        return implementationBuilder.CreateType()!;
    }

    private static Type CreateServiceWithSkippableInterfaces()
    {
        var assemblyName = new AssemblyName($"GameGuild.Coverage{Guid.NewGuid():N}AI");
        var assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule(assemblyName.Name!);
        var genericBuilder = module.DefineType(
            "ICoverageGeneric`1",
            TypeAttributes.Interface | TypeAttributes.Abstract | TypeAttributes.Public);
        genericBuilder.DefineGenericParameters("T");
        var genericInterface = genericBuilder.CreateType()!.MakeGenericType(typeof(string));
        var disposableInterface = module.DefineType(
            "IDisposableCoverage",
            TypeAttributes.Interface | TypeAttributes.Abstract | TypeAttributes.Public).CreateType()!;
        var asyncDisposableInterface = module.DefineType(
            "IAsyncDisposableCoverage",
            TypeAttributes.Interface | TypeAttributes.Abstract | TypeAttributes.Public).CreateType()!;
        var regularInterface = module.DefineType(
            "ICoverageFeature",
            TypeAttributes.Interface | TypeAttributes.Abstract | TypeAttributes.Public).CreateType()!;
        var implementationBuilder = module.DefineType(
            "UnmatchedCoverageService",
            TypeAttributes.Class | TypeAttributes.Public);
        implementationBuilder.AddInterfaceImplementation(genericInterface);
        implementationBuilder.AddInterfaceImplementation(disposableInterface);
        implementationBuilder.AddInterfaceImplementation(asyncDisposableInterface);
        implementationBuilder.AddInterfaceImplementation(regularInterface);
        implementationBuilder.DefineDefaultConstructor(MethodAttributes.Public);
        return implementationBuilder.CreateType()!;
    }

    private static Type CreateServiceWithNonPrefixedInterfaces()
    {
        var assemblyName = new AssemblyName($"GameGuild.Coverage{Guid.NewGuid():N}AI");
        var assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule(assemblyName.Name!);
        var repositoryInterface = module.DefineType(
            "CoverageRepository",
            TypeAttributes.Interface | TypeAttributes.Abstract | TypeAttributes.Public).CreateType()!;
        var serviceInterface = module.DefineType(
            "CoverageService",
            TypeAttributes.Interface | TypeAttributes.Abstract | TypeAttributes.Public).CreateType()!;
        var readerInterface = module.DefineType(
            "CoverageReader",
            TypeAttributes.Interface | TypeAttributes.Abstract | TypeAttributes.Public).CreateType()!;
        var implementationBuilder = module.DefineType(
            "NonPrefixedConventionType",
            TypeAttributes.Class | TypeAttributes.Public);
        implementationBuilder.AddInterfaceImplementation(repositoryInterface);
        implementationBuilder.AddInterfaceImplementation(serviceInterface);
        implementationBuilder.AddInterfaceImplementation(readerInterface);
        implementationBuilder.DefineDefaultConstructor(MethodAttributes.Public);
        return implementationBuilder.CreateType()!;
    }

    private static (Type Interface, Type Implementation) CreateRepositoryWithoutPublicConstructor()
    {
        var assemblyName = new AssemblyName($"GameGuild.Coverage{Guid.NewGuid():N}AI");
        var assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule(assemblyName.Name!);
        var interfaceBuilder = module.DefineType(
            "ICoverageNoConstructorRepository",
            TypeAttributes.Interface | TypeAttributes.Abstract | TypeAttributes.Public);
        var repositoryInterface = interfaceBuilder.CreateType()!;
        var implementationBuilder = module.DefineType(
            "CoverageNoConstructorRepository",
            TypeAttributes.Class | TypeAttributes.Public);
        implementationBuilder.AddInterfaceImplementation(repositoryInterface);
        var constructor = implementationBuilder.DefineConstructor(
            MethodAttributes.Private,
            CallingConventions.Standard,
            Type.EmptyTypes);
        var generator = constructor.GetILGenerator();
        generator.Emit(OpCodes.Ldarg_0);
        generator.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes)!);
        generator.Emit(OpCodes.Ret);
        return (repositoryInterface, implementationBuilder.CreateType()!);
    }

    private static T InvokePrivate<T>(Type declaringType, string name, params object?[] arguments)
    {
        var method = declaringType.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
        method.Should().NotBeNull();
        var value = method!.Invoke(null, arguments);
        if (value is null)
            return default!;
        return value.Should().BeAssignableTo<T>().Subject;
    }

    private interface IRequestHandler<T>;
    private interface IValidator<T>;
    private sealed class LoadableHandlerValidator : IRequestHandler<string>, IValidator<string>;
}

public sealed class InfrastructureLoadableType;
public abstract class InfrastructureAbstractType;
