using Microsoft.Azure.Cosmos;

public static class CosmosDbExtensions
{
    /// <summary>
    /// Convert a feed iterator to IAsyncEnumerable
    /// </summary>
    /// <typeparam name="TModel"></typeparam>
    /// <param name="setIterator"></param>
    /// <returns></returns>
    public static async IAsyncEnumerable<TModel> ToAsyncEnumerable<TModel>(this FeedIterator<TModel> setIterator)
    {
        while (setIterator.HasMoreResults)
        {
            foreach (var item in await setIterator.ReadNextAsync())
            {
                yield return item;
            }
        }
    }

    /// <summary>
    /// Convert a feed iterator to a List
    /// </summary>
    /// <typeparam name="TModel"></typeparam>
    /// <param name="setIterator"></param>
    /// <returns></returns>
    public static List<TModel> ToList<TModel>(this FeedIterator<TModel> setIterator)
    {
        while (setIterator.HasMoreResults)
        {
            foreach (var item in await setIterator.ReadNextAsync())
            {
                yield return item;
            }
        }
    }
}