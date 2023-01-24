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
    /// Convert a feed iterator to IAsyncEnumerable with a custom transform
    /// </summary>
    /// <typeparam name="TModel"></typeparam>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="setIterator"></param>
    /// <returns></returns>
    public static async IAsyncEnumerable<TResult> ToAsyncEnumerable<TModel, TResult>(this FeedIterator<TModel> setIterator, Func<TModel, TResult> transform)
    {
        while (setIterator.HasMoreResults)
        {
            foreach (var item in await setIterator.ReadNextAsync())
            {
                yield return transform(item);
            }
        }
    }
}