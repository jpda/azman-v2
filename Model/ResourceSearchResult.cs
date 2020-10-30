using System;

namespace azman_v2
{
    public class ResourceSearchResult : SearchResult
    {
        public ResourceSearchResult(string subscriptionId, string resourceId) : base(subscriptionId, resourceId) { }

        protected override void Map()
        {
            throw new NotImplementedException();
        }
    }

    public abstract class SearchResult
    {
        public string SubscriptionId { get; set; }
        public string ResourceId { get; set; }
        public SearchResult(string subscriptionId, string resourceId)
        {
            this.SubscriptionId = subscriptionId;
            this.ResourceId = resourceId;
        }

        protected abstract void Map();
    }
}
