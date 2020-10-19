using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;

namespace azman_v2
{
    public static class Extensions
    {
        public async static Task AddRangeAsync<T>(this IAsyncCollector<T> queueCollector, IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                await queueCollector.AddAsync(item);
            }
        }
    }
}