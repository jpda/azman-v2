using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace azman_v2.Model
{
    public class ResourceGraphQueryModel
    {
        [JsonPropertyName("subscriptions")]
        public List<string> Subscriptions { get; set; }
        [JsonPropertyName("query")]
        public string Query { get; set; }

        public ResourceGraphQueryModel(IEnumerable<string> subscriptions, string query)
        {
            Subscriptions = subscriptions.ToList();
            Query = query;
        }
    }
}