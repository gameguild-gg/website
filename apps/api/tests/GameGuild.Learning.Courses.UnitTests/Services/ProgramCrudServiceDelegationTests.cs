using System.Reflection;
using FluentAssertions;
using Xunit;

namespace GameGuild.Learning.Courses.UnitTests.Services;

public sealed class ProgramCrudServiceDelegationTests
{
    [Fact]
    public async Task EveryCompatibilityMethodDelegatesToItsFocusedServiceWithoutChangingArguments()
    {
        var read = RecordingDispatchProxy<IProgramReadService>.Create();
        var write = RecordingDispatchProxy<IProgramWriteService>.Create();
        var service = new ProgramCrudService(read.Service, write.Service);
        var facadeMethods = typeof(ProgramCrudService)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .OrderBy(method => method.Name)
            .ThenBy(method => method.GetParameters().Length)
            .ToArray();

        facadeMethods.Should().NotBeEmpty();
        foreach (var method in facadeMethods)
        {
            var parameterTypes = method.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
            var readMethod = typeof(IProgramReadService).GetMethod(method.Name, parameterTypes);
            var writeMethod = typeof(IProgramWriteService).GetMethod(method.Name, parameterTypes);
            (readMethod is not null ^ writeMethod is not null).Should().BeTrue(
                $"{method} must belong to exactly one focused service contract");

            var target = readMethod is not null ? (IRecordingProxy)read : write;
            var other = readMethod is not null ? (IRecordingProxy)write : read;
            var arguments = method.GetParameters().Select(CreateArgument).ToArray();
            var targetCallsBefore = target.Invocations.Count;
            var otherCallsBefore = other.Invocations.Count;

            var result = method.Invoke(service, arguments);
            result.Should().BeAssignableTo<Task>();
            await ((Task)result!);

            target.Invocations.Should().HaveCount(targetCallsBefore + 1);
            other.Invocations.Should().HaveCount(otherCallsBefore);
            var invocation = target.Invocations[^1];
            invocation.Method.Name.Should().Be(method.Name);
            invocation.Method.GetParameters().Select(parameter => parameter.ParameterType)
                .Should().Equal(parameterTypes);
            invocation.Arguments.Should().Equal(arguments);
        }
    }

    private static object? CreateArgument(ParameterInfo parameter)
    {
        var type = parameter.ParameterType;
        if (type == typeof(Guid))
        {
            return Guid.NewGuid();
        }

        if (type == typeof(string))
        {
            return $"value-for-{parameter.Name}";
        }

        if (type == typeof(int))
        {
            return 7;
        }

        if (type.IsEnum)
        {
            return Enum.GetValues(type).GetValue(0);
        }

        if (Nullable.GetUnderlyingType(type) is not null)
        {
            return null;
        }

        if (type == typeof(List<Guid>))
        {
            return new List<Guid> { Guid.NewGuid() };
        }

        if (type == typeof(Program))
        {
            return new Program();
        }

        if (type == typeof(ProgramContent))
        {
            return new ProgramContent();
        }

        return null;
    }

    private interface IRecordingProxy
    {
        List<RecordedInvocation> Invocations { get; }
    }

    private class RecordingDispatchProxy<T> : DispatchProxy, IRecordingProxy where T : class
    {
        public T Service => (T)(object)this;
        public List<RecordedInvocation> Invocations { get; } = [];

        public static RecordingDispatchProxy<T> Create()
        {
            var service = DispatchProxy.Create<T, RecordingDispatchProxy<T>>();
            return (RecordingDispatchProxy<T>)(object)service;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            targetMethod.Should().NotBeNull();
            var arguments = args ?? [];
            Invocations.Add(new RecordedInvocation(targetMethod!, arguments));
            var returnType = targetMethod!.ReturnType;
            if (returnType == typeof(Task))
            {
                return Task.CompletedTask;
            }

            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var resultType = returnType.GetGenericArguments()[0];
                var result = resultType.IsValueType ? Activator.CreateInstance(resultType) : null;
                return typeof(Task)
                    .GetMethod(nameof(Task.FromResult))!
                    .MakeGenericMethod(resultType)
                    .Invoke(null, [result]);
            }
            throw new NotSupportedException($"Unexpected compatibility return type {returnType}.");
        }
    }

    private sealed record RecordedInvocation(MethodInfo Method, IReadOnlyList<object?> Arguments);
}
