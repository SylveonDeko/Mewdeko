using Mewdeko.Votes.Common;
using Mewdeko.Votes.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Mewdeko.Votes.Controllers;

[ApiController]
[Route("[controller]")]
public class DiscordsController : ControllerBase
{
    private readonly ILogger<TopGgController> _logger;
    private readonly FileVotesCache _cache;

    public DiscordsController(ILogger<TopGgController> logger, FileVotesCache cache)
    {
        _logger = logger;
        _cache = cache;
    }

    [HttpGet("new")]
    [Authorize(Policy = Policies.DISCORDS_AUTH)]
    public async Task<IEnumerable<Vote>> New()
    {
        var votes = await _cache.GetNewDiscordsVotesAsync();
        if(votes.Count > 0)
            _logger.LogInformation("Sending {NewDiscordsVotes} new discords votes.", votes.Count);
        return votes;
    }
}