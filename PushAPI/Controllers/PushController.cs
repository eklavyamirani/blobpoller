using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PushAPI.Services;

namespace PushAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PushController : ControllerBase
    {
        private readonly BlobClient pushApiClient;

        public PushController(BlobClient pushApiClient)
        {
            if (pushApiClient is null)
            {
                throw new System.ArgumentNullException(nameof(pushApiClient));
            }

            this.pushApiClient = pushApiClient;
        }

        [HttpGet]
        public IActionResult Get()
        {
            return Ok(1);
        }

        [HttpPost]
        [Route("create/{entity}")]
        public async Task<IActionResult> CreateEntity(string entity)
        {
            if (string.IsNullOrWhiteSpace(entity))
            {
                throw new System.ArgumentException($"'{nameof(entity)}' cannot be null or whitespace", nameof(entity));
            }

            await pushApiClient.CreateEntityAsync(entity);
            return CreatedAtAction(actionName: nameof(PushRows), value:entity);
        }

        [HttpPost]
        [Route("{entityName}/addRows")]
        public async Task<IActionResult> PushRows(string entityName, [FromBody] object row)
        {
            if (string.IsNullOrWhiteSpace(entityName))
            {
                throw new System.ArgumentException($"'{nameof(entityName)}' cannot be null or whitespace", nameof(entityName));
            }

            if (row == null)
            {
                throw new System.ArgumentException($"'{nameof(row)}' cannot be null or whitespace", nameof(row));
            }

            var path = await this.pushApiClient.PostRowsAsync(entityName, Newtonsoft.Json.JsonConvert.SerializeObject(row));
            return Ok(path);
        }
    }
}
