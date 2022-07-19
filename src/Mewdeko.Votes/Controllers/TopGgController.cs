using Mewdeko.Votes.Common;
using Mewdeko.Votes.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mewdeko.Votes.Controllers;

[ApiController]
[Route("[controller]")]
public class TopGgController : ControllerBase
{
    private readonly ILogger<TopGgController> _logger;
    private readonly FileVotesCache _cache;

    public TopGgController(Logger<TopGgController> logger, FileVotesCache cache)
    {
        _logger = logger;
        _cache = cache;
    }

    [HttpGet("new")]
    [Authorize(Policy = Policies.TOPGG_AUTH)]
    public async Task<IEnumerable<Vote>> New()
    {
        var votes = await _cache.GetNewTopGgVotesAsync();
        if (votes.Count > 0)
            _logger.LogInformation("Sending {NewTopggVotes} new topgg votes", votes.Count);

        return votes;
    }
}