namespace SqlBuildingBlocks.Utils;

public class Consolidator<TCommonInterface>
{
    public TResult ConsolidateService<TResult>(IEnumerable<TCommonInterface> commonInterfaces, Func<TCommonInterface, TResult> performInterfaceMethod,
                                               string exceptionMessage, Func<TResult, bool> resultFound)
    {
        foreach (var commonInterface in commonInterfaces)
        {
            TResult? result = performInterfaceMethod(commonInterface);
            if (resultFound(result))
                return result;
        }

        if (!string.IsNullOrEmpty(exceptionMessage))
            throw new Exception(exceptionMessage);

        return default!;
    }
}
