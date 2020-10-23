namespace azman_v2
{
    public struct ResourceSearchResult
    {
        public string SubscriptionId { get; set; }
        public string ResourceId { get; set; }
        //public IEnumerable<KeyValuePair<string, T> ExtraData {get;set;}
        public ResourceSearchResult(string subscriptionId, string resourceId)
        {
            this.SubscriptionId = subscriptionId;
            this.ResourceId = resourceId;
        }
    }
