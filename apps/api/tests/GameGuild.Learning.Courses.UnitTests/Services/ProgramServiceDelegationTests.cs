using System.Reflection;
using FluentAssertions;
using Xunit;

namespace GameGuild.Learning.Courses.UnitTests.Services;

public sealed class ProgramServiceDelegationTests
{
    [Fact]
    public async Task EveryCompatibilityMethodDelegatesToItsFocusedServiceWithoutChangingArguments()
    {
        var crud = RecordingDispatchProxy<IProgramCrudService>.Create();
        var lifecycle = RecordingDispatchProxy<IProgramLifecycleService>.Create();
        var service = new ProgramService(crud.Service, lifecycle.Service);
        var facadeMethods = typeof(ProgramService)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .OrderBy(method => method.Name)
            .ThenBy(method => method.GetParameters().Length)
            .ToArray();

        facadeMethods.Should().NotBeEmpty();
        foreach (var method in facadeMethods)
        {
            var parameterTypes = method.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
            var crudMethod = typeof(IProgramCrudService).GetMethod(method.Name, parameterTypes);
            var lifecycleMethod = typeof(IProgramLifecycleService).GetMethod(method.Name, parameterTypes);
            IRecordingProxy target = crudMethod is not null ? crud : lifecycle;
            (crudMethod is not null || lifecycleMethod is not null).Should().BeTrue(
                $"{method} must remain part of one focused service contract");
            var arguments = method.GetParameters().Select(CreateArgument).ToArray();
            var callsBefore = target.Invocations.Count;

            var result = method.Invoke(service, arguments);
            result.Should().BeAssignableTo<Task>();
            await ((Task)result!);

            target.Invocations.Should().HaveCount(callsBefore + 1);
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

        if (type == typeof(DateTime))
        {
            return new DateTime(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);
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
