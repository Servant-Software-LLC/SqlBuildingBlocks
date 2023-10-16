using System.Reflection;

namespace SqlBuildingBlocks.Utils;

public static class ReflectionHelper
{
    public static TResult? CallMethod<TResult>(object instance, string methodName, Type typeParameter, params object[] methodArguments) =>
        CallMethod<TResult>(instance, methodName, new Type[] { typeParameter }, methodArguments);

    public static TResult? CallMethod<TResult>(object instance, string methodName, Type[] typeParameters, params object[] methodArguments) =>
        CallMethod<TResult>(instance, instance.GetType(), methodName, typeParameters, methodArguments);

    public static TResult? CallMethod<TResult, TInstance>(TInstance instance, string methodName, Type typeParameter, params object[] methodArguments) =>
        CallMethod<TResult, TInstance>(instance, methodName, new Type[] { typeParameter }, methodArguments);

    public static TResult? CallMethod<TResult, TInstance>(TInstance instance, string methodName, Type[] typeParameters, params object[] methodArguments) =>
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

    private static TResult? CallMethod<TResult>(object? instance, Type typeWithMethod, string methodName, Type[] typeParameters, params object[] methodArguments)
    {
        if (instance == null)
            throw new ArgumentNullException(nameof(instance));

        MethodInfo method = GetSpecializedMethod(typeWithMethod, methodName, typeParameters);

        var methodReturnValue = method.Invoke(instance, methodArguments);

        return (TResult?)methodReturnValue;
    }

}
