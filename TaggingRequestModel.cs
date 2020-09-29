using System;

namespace azman_v2
{
    public class TaggingRequestModel
    {
        public string ResourceGroupName { get; set; }
        public string CreatedByUser { get; set; }
        public string SubscriptionId { get; set; }
        public DateTime DateCreated { get; set; }
    }
}