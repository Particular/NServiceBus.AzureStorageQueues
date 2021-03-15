namespace NServiceBus.Transport.AzureStorageQueues
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Table;

    static class TableExtensions
    {
        public static async Task<IList<T>> QueryUpTo<T>(this CloudTable table, TableQuery<T> query, int maxItemsToReturn)
            where T : ITableEntity, new()
        {
            var items = new List<T>();
            TableContinuationToken token = null;

            do
            {
                var seg = await table.ExecuteQuerySegmentedAsync(query, token, null, null).ConfigureAwait(false);
                token = seg.ContinuationToken;

                if (items.Count + seg.Results.Count > maxItemsToReturn)
                {
                    var numberToTake = items.Count + seg.Results.Count - maxItemsToReturn;
                    items.AddRange(seg.Take(seg.Results.Count - numberToTake));
                }
                else
                {
                    items.AddRange(seg);
                }
            }
            while (token != null && items.Count < maxItemsToReturn);

            return items;
        }

        public static async Task<IList<T>> QueryAll<T>(this CloudTable table, TableQuery<T> query)
            where T : ITableEntity, new()
        {
            var items = new List<T>();
            TableContinuationToken token = null;

            do
            {
                var seg = await table.ExecuteQuerySegmentedAsync(query, token, null, null).ConfigureAwait(false);
                token = seg.ContinuationToken;
                items.AddRange(seg);
            }
            while (token != null);

            return items;
        }
    }
}