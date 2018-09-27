﻿namespace NServiceBus.Transport.AzureStorageQueues
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage.Table;

    static class TableExtensions
    {
        public static async Task<IList<T>> ExecuteQueryAsync<T>(this CloudTable table, TableQuery<T> query, int take, CancellationToken cancellationToken)
            where T : ITableEntity, new()
        {
            var items = new List<T>();
            TableContinuationToken token = null;

            do
            {
                var seg = await table.ExecuteQuerySegmentedAsync(query, token, null, null, cancellationToken).ConfigureAwait(false);
                token = seg.ContinuationToken;

                if (items.Count + seg.Results.Count > take)
                {
                    var numberToTake = items.Count + seg.Results.Count - take;
                    items.AddRange(seg.Take(seg.Results.Count - numberToTake));
                }
                else
                {
                    items.AddRange(seg);
                }
            } while (token != null && !cancellationToken.IsCancellationRequested && items.Count < take);

            return items;
        }
    }
}