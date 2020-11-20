using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using azman_v2.Model;

namespace azman_v2
{
    public interface IResourceManagementService
    {
        TagSuiteModel? ProcessAlert(dynamic alert);
        Task AddTagSuite(TagSuiteModel request);
        Task AddTags(TagModel request);
        Task AddTags(string resourceGroup, string subscriptionId, params KeyValuePair<string, string>[] tags);
        Task<string> GetRawTagValue(string subscriptionId, string resourceGroupName, string tagName);
        Task<T> GetTagValue<T>(string subscriptionId, string resourceGroupName, string tagName, Func<string, T> converter, Func<T> error);
        Task<string> ExportResourceGroupTemplateByName(string subscriptionId, string groupName);
        Task<Azure.ResourceManager.Resources.Models.ResourceGroup> GetResourceGroup(string subscriptionId,
            string resourceGroupName);
    }
}