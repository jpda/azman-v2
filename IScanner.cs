using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace azman_v2
{
    public interface IScanner
    {
        Task<IEnumerable<ResourceSearchResult>> ScanForUntaggedResources();
        Task<IEnumerable<ResourceSearchResult>> ScanForExpiredResources();
        Task<IEnumerable<ResourceSearchResult>> ScanForExpiredResources(DateTime expirationDate);
        Task<IEnumerable<ResourceSearchResult>> ScanForExpiredResources(string kustoDateExpression);
        Task<IEnumerable<ResourceSearchResult>> FindSpecificResources(string searchExpression);
    }
}