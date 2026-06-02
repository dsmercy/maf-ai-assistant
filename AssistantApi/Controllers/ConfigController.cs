using AssistantApi.Application.Configuration;
using AssistantApi.Application.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AssistantApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigController : ControllerBase
{
    private readonly AssistantOptions _options;

    public ConfigController(IOptions<AssistantOptions> options)
    {
        _options = options.Value;
    }

    [HttpGet]
    public ActionResult<ConfigResponse> GetConfig()
    {
        return Ok(new ConfigResponse
        {
            ChatModel = _options.ChatModel,
            EmbeddingModel = _options.EmbeddingModel,
            ChunkSize = _options.ChunkSize,
            ChunkOverlap = _options.ChunkOverlap,
            TopK = _options.TopK,
            Temperature = _options.Temperature,
            FeatureFlags = new Dictionary<string, bool>
            {
                ["streaming"] = false,   // enabled Phase 4
                ["rag"] = false,          // enabled Phase 3
                ["auth"] = false          // enabled Phase 5
            }
        });
    }
}
