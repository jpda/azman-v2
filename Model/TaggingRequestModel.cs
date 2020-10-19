using System;

namespace azman_v2.Model
{
    public class TaggingRequestModel
    {
        public TaggingRequestModel(string groupName, string user, string subscriptionId, DateTime created)
        {
            this.ResourceGroupName = groupName;
            this.CreatedByUser = user;
            this.SubscriptionId = subscriptionId;
            this.DateCreated = created;
        }

        public string ResourceGroupName { get; set; }
        public string CreatedByUser { get; set; }
        public string SubscriptionId { get; set; }
        public DateTime DateCreated { get; set; }
    }


}