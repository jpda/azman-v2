using System.Threading.Tasks;
using azman_v2.Model;

namespace azman_v2
{
    public interface IResourceManagementService
    {
        TaggingRequestModel? ProcessAlert(dynamic alert);
        Task TagResource(TaggingRequestModel request);
        Task<string> ExportResourceGroupTemplateByName(string subscriptionId, string groupName);
    }
}