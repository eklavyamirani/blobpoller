using System.Threading.Tasks;

namespace blobPoller
{
    public interface IPollingStrategy
    {
        Task CheckForUpdatesAsync(string entity);
    }
}