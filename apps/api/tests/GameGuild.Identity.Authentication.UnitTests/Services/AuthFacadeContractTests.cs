using System.Reflection;
using FluentAssertions;
using GameGuild.Identity.Authentication;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class AuthFacadeContractTests
{
    public static TheoryData<Type, string> Operations => new()
    {
        { typeof(ILocalAuthService), nameof(IAuthService.LocalSignInAsync) },
        { typeof(ILocalAuthService), nameof(IAuthService.LocalSignUpAsync) },
        { typeof(ILocalAuthService), nameof(IAuthService.RefreshTokenAsync) },
        { typeof(ILocalAuthService), nameof(IAuthService.RevokeRefreshTokenAsync) },
        { typeof(IOAuthAuthService), nameof(IAuthService.GitHubSignInAsync) },
        { typeof(IOAuthAuthService), nameof(IAuthService.GoogleSignInAsync) },
        { typeof(IOAuthAuthService), nameof(IAuthService.MicrosoftSignInAsync) },
        { typeof(IOAuthAuthService), nameof(IAuthService.GoogleIdTokenSignInAsync) },
        { typeof(IOAuthAuthService), nameof(IAuthService.DiscordSignInAsync) },
        { typeof(IOAuthAuthService), nameof(IAuthService.OidcSignInAsync) },
        { typeof(IOAuthAuthService), nameof(IAuthService.GetGitHubAuthUrlAsync) },
        { typeof(IOAuthAuthService), nameof(IAuthService.GetGoogleAuthUrlAsync) },
        { typeof(IPasswordService), nameof(IAuthService.SendEmailVerificationAsync) },
        { typeof(IPasswordService), nameof(IAuthService.VerifyEmailAsync) },
        { typeof(IPasswordService), nameof(IAuthService.ForgotPasswordAsync) },
        { typeof(IPasswordService), nameof(IAuthService.ResetPasswordAsync) },
        { typeof(IPasswordService), nameof(IAuthService.ChangePasswordAsync) },
        { typeof(IWeb3AuthService), nameof(IAuthService.GenerateWeb3ChallengeAsync) },
        { typeof(IWeb3AuthService), nameof(IAuthService.VerifyWeb3SignatureAsync) }
    };

    public static IEnumerable<object[]> CancelableOperations => Operations
        .Where(operation => ((Type)operation[0]).GetMethod((string)operation[1])!
            .GetParameters().Any(parameter => parameter.ParameterType == typeof(CancellationToken)));

    [Fact]
    public void ExplicitOperationMatrix_CoversEverySpecializedContractAndFacadeMethod()
    {
        var expected = Operations.Select(operation => (string)operation[1]).ToArray();
        expected.Should().HaveCount(19).And.OnlyHaveUniqueItems();
        var contracts = typeof(IAuthService).GetInterfaces().SelectMany(contract => contract.GetMethods());
        contracts.Select(method => method.Name).Should().BeEquivalentTo(expected);
        typeof(AuthService).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => method.Name).Should().BeEquivalentTo(expected);
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task Success_PreservesExactTaskResultAndAllArguments(Type contract, string methodName)
    {
        using var cancellation = new CancellationTokenSource();
        var operation = new Operation(contract, methodName, cancellation.Token);
        operation.Owner.ReturnTask = CreateCompletedTask(operation.Method.ReturnType);

        var returned = operation.Invoke();

        returned.Should().BeSameAs(operation.Owner.ReturnTask);
        await returned;
        if (operation.Method.ReturnType.IsGenericType)
        {
            operation.Method.ReturnType.GetProperty("Result")!.GetValue(returned).Should()
                .BeSameAs(operation.Method.ReturnType.GetProperty("Result")!.GetValue(operation.Owner.ReturnTask));
        }

        operation.AssertSingleDelegation();
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task FaultedTask_PreservesExactExceptionAndTask(Type contract, string methodName)
    {
        var operation = new Operation(contract, methodName, CancellationToken.None);
        var failure = new InvalidOperationException("Specialized service failure");
        operation.Owner.ReturnTask = CreateFaultedTask(operation.Method.ReturnType, failure);

        var returned = operation.Invoke();

        returned.Should().BeSameAs(operation.Owner.ReturnTask);
        var observed = await Assert.ThrowsAsync<InvalidOperationException>(() => returned);
        observed.Should().BeSameAs(failure);
        operation.AssertSingleDelegation();
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public void SynchronousFailure_PropagatesWithoutTranslation(Type contract, string methodName)
    {
        var operation = new Operation(contract, methodName, CancellationToken.None);
        var failure = new UnauthorizedAccessException("Specialized synchronous failure");
        operation.Owner.SynchronousFailure = failure;

        var reflected = Assert.Throws<TargetInvocationException>(() => { _ = operation.Invoke(); });

        reflected.InnerException.Should().BeSameAs(failure);
        operation.AssertSingleDelegation();
    }

    [Theory]
    [MemberData(nameof(CancelableOperations))]
    public async Task PreCanceledTask_PreservesTaskAndCancellationToken(Type contract, string methodName)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var operation = new Operation(contract, methodName, cancellation.Token);
        operation.Owner.ReturnTask = CreateCanceledTask(operation.Method.ReturnType, cancellation.Token);

        var returned = operation.Invoke();

        returned.Should().BeSameAs(operation.Owner.ReturnTask);
        var observed = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => returned);
        observed.CancellationToken.Should().Be(cancellation.Token);
        operation.AssertSingleDelegation();
    }

    [Theory]
    [MemberData(nameof(CancelableOperations))]
    public async Task CancellationAfterDelegation_PreservesPendingTaskAndToken(Type contract, string methodName)
    {
        using var cancellation = new CancellationTokenSource();
        var operation = new Operation(contract, methodName, cancellation.Token);
        var (pending, cancel) = CreatePendingTask(operation.Method.ReturnType);
        operation.Owner.ReturnTask = pending;

        var returned = operation.Invoke();

        returned.Should().BeSameAs(pending);
        returned.IsCompleted.Should().BeFalse();
        cancellation.Cancel();
        cancel(cancellation.Token);
        var observed = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => returned);
        observed.CancellationToken.Should().Be(cancellation.Token);
        operation.AssertSingleDelegation();
    }

    private static Task CreateCompletedTask(Type returnType) => returnType == typeof(Task)
        ? Task.CompletedTask
        : InvokeTaskFactory(nameof(CompletedTask), returnType);

    private static Task CreateFaultedTask(Type returnType, Exception failure) => returnType == typeof(Task)
        ? Task.FromException(failure)
        : InvokeTaskFactory(nameof(FaultedTask), returnType, failure);

    private static Task CreateCanceledTask(Type returnType, CancellationToken token) => returnType == typeof(Task)
        ? Task.FromCanceled(token)
        : InvokeTaskFactory(nameof(CanceledTask), returnType, token);

    private static Task InvokeTaskFactory(string name, Type returnType, params object[] arguments) =>
        (Task)typeof(AuthFacadeContractTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(returnType.GetGenericArguments().Single()).Invoke(null, arguments)!;

    private static Task CompletedTask<T>()
    {
        var result = typeof(T) == typeof(string) ? "https://identity.example/authorize" : Activator.CreateInstance(typeof(T));
        return Task.FromResult((T)result!);
    }

    private static Task FaultedTask<T>(Exception failure) => Task.FromException<T>(failure);

    private static Task CanceledTask<T>(CancellationToken token) => Task.FromCanceled<T>(token);

    private static (Task Task, Action<CancellationToken> Cancel) CreatePendingTask(Type returnType)
    {
        if (returnType == typeof(Task))
        {
            var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return (source.Task, token => source.SetCanceled(token));
        }

        return ((Task, Action<CancellationToken>))typeof(AuthFacadeContractTests)
            .GetMethod(nameof(PendingTask), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(returnType.GetGenericArguments().Single()).Invoke(null, null)!;
    }

    private static (Task, Action<CancellationToken>) PendingTask<T>()
    {
        var source = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        return (source.Task, token => source.SetCanceled(token));
    }

    private sealed class Operation
    {
        private readonly AuthService _facade;
        private readonly Dictionary<Type, RecordingAuthService> _services;
        private readonly object?[] _arguments;

        internal Operation(Type contract, string methodName, CancellationToken token)
        {
            var local = DispatchProxy.Create<ILocalAuthService, RecordingAuthService>();
            var oauth = DispatchProxy.Create<IOAuthAuthService, RecordingAuthService>();
            var password = DispatchProxy.Create<IPasswordService, RecordingAuthService>();
            var web3 = DispatchProxy.Create<IWeb3AuthService, RecordingAuthService>();
            _facade = new AuthService(local, oauth, password, web3);
            _services = new Dictionary<Type, RecordingAuthService>
            {
                [typeof(ILocalAuthService)] = (RecordingAuthService)local,
                [typeof(IOAuthAuthService)] = (RecordingAuthService)oauth,
                [typeof(IPasswordService)] = (RecordingAuthService)password,
                [typeof(IWeb3AuthService)] = (RecordingAuthService)web3
            };
            Owner = _services[contract];
            Method = typeof(AuthService).GetMethod(methodName)!;
            _arguments = Method.GetParameters().Select(parameter => CreateArgument(parameter, token)).ToArray();
        }

        internal RecordingAuthService Owner { get; }

        internal MethodInfo Method { get; }

        internal Task Invoke() => (Task)Method.Invoke(_facade, _arguments)!;

        internal void AssertSingleDelegation()
        {
            Owner.Calls.Should().Be(1);
            Owner.LastMethod!.Name.Should().Be(Method.Name);
            Owner.LastArguments.Should().HaveCount(_arguments.Length);
            for (var index = 0; index < _arguments.Length; index++)
            {
                if (Method.GetParameters()[index].ParameterType.IsValueType)
                {
                    Owner.LastArguments![index].Should().Be(_arguments[index]);
                }
                else
                {
                    Owner.LastArguments![index].Should().BeSameAs(_arguments[index]);
                }
            }

            _services.Values.Where(service => !ReferenceEquals(service, Owner))
                .Should().OnlyContain(service => service.Calls == 0);
        }

        private static object? CreateArgument(ParameterInfo parameter, CancellationToken token)
        {
            if (parameter.ParameterType == typeof(CancellationToken))
            {
                return token;
            }

            if (parameter.ParameterType == typeof(Guid))
            {
                return Guid.NewGuid();
            }

            return parameter.ParameterType == typeof(string)
                ? $"preserved-{parameter.Name}-{Guid.NewGuid():N}"
                : Activator.CreateInstance(parameter.ParameterType);
        }
    }

    public class RecordingAuthService : DispatchProxy
    {
        public Task? ReturnTask { get; set; }

        public Exception? SynchronousFailure { get; set; }

        public int Calls { get; private set; }

        public MethodInfo? LastMethod { get; private set; }

        public object?[]? LastArguments { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Calls++;
            LastMethod = targetMethod;
            LastArguments = args;
            if (SynchronousFailure is not null)
            {
                throw SynchronousFailure;
            }

            return ReturnTask ?? throw new InvalidOperationException("Missing specialized task");
        }
    }
}
