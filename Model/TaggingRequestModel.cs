using System;
using System.Collections.Generic;
using System.Linq;

namespace azman_v2.Model
{
    public class TagSuiteModel : TagModel
    {
        public TagSuiteModel(string groupName, string subscriptionId, string user, DateTime managementDate) : base(groupName, subscriptionId)
        {
            this.ResourceOwner = user;
            this.TrackingStartDate = managementDate;
        }

        public string ResourceOwner { get; set; }
        public DateTime TrackingStartDate { get; set; }

        public void GenerateBaseTags()
        {
            var newDate = this.TrackingStartDate.AddDays(30).Date;

            this.Tags.Add(new KeyValuePair<string, string>("expires", newDate.ToString("yyyy-MM-dd")));
            this.Tags.Add(new KeyValuePair<string, string>("tagged-by", "thrazman"));
            this.Tags.Add(new KeyValuePair<string, string>("owner", this.ResourceOwner));

            // service-msft-prod-azman-main-compute
            var groupNamePieces = ResourceGroupName.Split('-');
            if (groupNamePieces.Count() >= 3)
            {
                this.Tags.Add(new KeyValuePair<string, string>("function", groupNamePieces[0])); // service
                this.Tags.Add(new KeyValuePair<string, string>("customer", groupNamePieces[1])); // msft
                this.Tags.Add(new KeyValuePair<string, string>("env", groupNamePieces[2])); // prod
                this.Tags.Add(new KeyValuePair<string, string>("project", string.Join('-', groupNamePieces.Skip(3)))); //azman-main-compute
            }
        }
    }

    public class TagModel
    {
        public string ResourceGroupName { get; set; }
        public string SubscriptionId { get; set; }
        public List<KeyValuePair<string, string>> Tags { get; set; }

        public TagModel(string groupName, string subscriptionId, params KeyValuePair<string, string>[] tags)
        {
            this.ResourceGroupName = groupName;
            this.SubscriptionId = subscriptionId;
            this.Tags = new List<KeyValuePair<string, string>>();
            Tags.AddRange(tags);
        }
    }
}