namespace SqlBuildingBlocks.Extensions;

//NOTE:  This extension is available natively in .NET Standard 2.1, but we cannot upgrade from 2.0 -> 2.1, because Irony's GrammarExplorer
//       still only supports .NET Framework and the .NET Framework doesn't support .NET Standard 2.1.
internal static class DictionaryExtensions
{
    public static bool TryAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue value)
    {
        if (dictionary == null) throw new ArgumentNullException(nameof(dictionary));

        if (dictionary.ContainsKey(key)) return false;

        dictionary.Add(key, value);
        return true;
    }
}
