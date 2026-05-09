using System.Reflection;

namespace SqlBuildingBlocks.Utils;

// #129 (Wave 12 of /uber-report 2026-05-09): the QueryEngine reflection-dispatch sites
// that justified this helper have been replaced with cached compiled delegates in
// SqlBuildingBlocks.Utils.CompiledQueryDispatch. The Wave 9 BenchmarkDotNet baseline
// (Docs/Benchmarks/baseline-2026-05-09-*.json) showed ExecuteJoinedSelect at ~360 ms
// for two 100-row tables; the dispatch was a real cost.
//
// This helper is retained as [Obsolete] rather than deleted because it ships in the
// public SqlBuildingBlocks.Core NuGet contract — external consumers may still call it.
// A separate cleanup PR will remove it once consumers are confirmed not to use it.
[Obsolete("Reflection dispatch was replaced by SqlBuildingBlocks.Utils.CompiledQueryDispatch in Wave 12 of /uber-report 2026-05-09 (issue #129). This helper is retained for back-compat and will be removed in a future release. Prefer cached compiled delegates for any new generic-dispatch needs.")]
public static class ReflectionHelper
{
    public static TResult? CallMethod<TResult>(object instance, string methodName, Type typeParameter, params object?[] methodArguments) =>
        CallMethod<TResult>(instance, methodName, new Type[] { typeParameter }, methodArguments);

    public static TResult? CallMethod<TResult>(object instance, string methodName, Type[] typeParameters, params object?[] methodArguments) =>
        CallMethod<TResult>(instance, instance.GetType(), methodName, typeParameters, methodArguments);

    public static TResult? CallMethod<TResult, TInstance>(TInstance instance, string methodName, Type typeParameter, params object?[] methodArguments) =>
        CallMethod<TResult, TInstance>(instance, methodName, new Type[] { typeParameter }, methodArguments);

    public static TResult? CallMethod<TResult, TInstance>(TInstance instance, string methodName, Type[] typeParameters, params object?[] methodArguments) =>
        CallMethod<TResult>(instance, typeof(TInstance), methodName, typeParameters, methodArguments);

    public static MethodInfo GetSpecializedMethod(Type typeWithMethod, string methodName, Type[] typeParameters)
    {
        var genericMethod = typeWithMethod.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (genericMethod == null)
            throw new Exception($"{nameof(genericMethod)} is null.  Unable to find method named {methodName} on type {typeWithMethod}.");

        var method = genericMethod!.MakeGenericMethod(typeParameters);
        if (method == null)
        {
            var genericTypes = string.Join(", ", typeParameters.Select(type => type.ToString()));

            throw new Exception($"{nameof(method)} is null.  Unable to make the generic method {genericMethod} specialized with the types {genericTypes}");
        }

        return method;
    }

    private static TResult? CallMethod<TResult>(object? instance, Type typeWithMethod, string methodName, Type[] typeParameters, params object?[] methodArguments)
    {
        if (instance == null)
            throw new ArgumentNullException(nameof(instance));

        MethodInfo method = GetSpecializedMethod(typeWithMethod, methodName, typeParameters);

        var methodReturnValue = method.Invoke(instance, methodArguments);

        return (TResult?)methodReturnValue;
    }

}
