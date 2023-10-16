using System.Linq.Expressions;
using System.Reflection;

namespace SqlBuildingBlocks.Utils;

public class LambdaHelper<TSource>
{
    /// <summary>Gets property infos from lambda.</summary>
    /// <param name="lambda">The lambda.</param>
    /// <typeparam name="TProperty">The property.</typeparam>
    /// <returns>The property infos.</returns>
    public static PropertyInfo GetPropertyInfo<TProperty>(Expression<Func<TSource, TProperty>> lambda)
    {
        if (lambda == null)
            throw new ArgumentNullException(nameof(lambda));

        if (lambda.Body is not MemberExpression member)
        {
            throw new ArgumentException($"Expression '{lambda}' body is not member expression.");
        }

        if (member.Member is not PropertyInfo propertyInfo)
        {
            throw new ArgumentException($"Expression '{lambda}' does not refer to a property.");
        }

        if (propertyInfo.ReflectedType is null)
        {
            throw new ArgumentException($"Expression '{lambda}' does not refer to a property.");
        }

        Type type = typeof(TSource);
        if (type != propertyInfo.ReflectedType && !propertyInfo.ReflectedType.IsAssignableFrom(type))
        {
            throw new ArgumentException($"Expression '{lambda}' refers to a property that is not from type {type}.");
        }

        return propertyInfo;
    }

}
