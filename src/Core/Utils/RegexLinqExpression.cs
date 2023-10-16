using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;

namespace SqlBuildingBlocks.Utils;

internal static class RegexLinqExpression
{

    public static Expression IsMatch(Expression column, Expression likePattern)
    {
        var methodInfo = typeof(Regex).GetMethod(nameof(Regex.IsMatch), BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(string), typeof(string) }, null);
        if (methodInfo == null)
            throw new Exception("Regex.IsMatch method not found.");

        var regexPattern = LikePatternToRegexPattern(likePattern);
        return Expression.Call(methodInfo, column, regexPattern);
    }

    private static Expression LikePatternToRegexPattern(Expression likePattern)
    {
        //Convert the likeValue into a Regex value. (with Linq Expressions)
        //Doing the equivalent of:  "^" + Regex.Escape(likePattern).Replace("_", ".").Replace("%", ".*") + "$"
        var escaped = Expression.Call(typeof(Regex), nameof(Regex.Escape), null, likePattern);
        var replaceUnderscore = Expression.Call(escaped, nameof(string.Replace), null, Expression.Constant("_"), Expression.Constant("."));
        var replacePercent = Expression.Call(replaceUnderscore, nameof(string.Replace), null, Expression.Constant("%"), Expression.Constant(".*"));

        var methodInfo = typeof(string).GetMethod(nameof(string.Concat), BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(object), typeof(object), typeof(object) }, null);
        if (methodInfo == null)
            throw new Exception("string.Concat method not found.");

        var concatStrings = Expression.Call(methodInfo, Expression.Constant("^"), replacePercent, Expression.Constant("$"));
        return concatStrings;
    }
}
