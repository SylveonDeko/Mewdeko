using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LinqToDB;
using Mewdeko.Core.Services.Database;
using Mewdeko.Core.Services.Database.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GuildConfigsController : ControllerBase
    {
        private readonly MewdekoContext _context;

        public GuildConfigsController(MewdekoContext context)
        {
            _context = context;
        }

        // GET: api/GuildConfigs
        [HttpGet]
        public async Task<ActionResult<IEnumerable<GuildConfig>>> GetGuildConfigs()
        {
            return AsyncExtensions.ToListAsync(_context.GuildConfigs).Result;
        }

        // GET: api/GuildConfigs/5
        [HttpGet("{id}")]
        public async Task<GuildConfig> GetGuildConfig(ulong id)
        {
            var guildConfig1 = await AsyncExtensions.ToListAsync(_context.GuildConfigs);
            var guildConfig = guildConfig1.FirstOrDefault(x => x.GuildId == id);

            return guildConfig;
        }

        // PUT: api/GuildConfigs/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutGuildConfig(int id, GuildConfig guildConfig)
        {
            if (id != guildConfig.Id) return BadRequest();

            _context.Entry(guildConfig).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!GuildConfigExists(id))
                    return NotFound();
                throw;
            }

            return NoContent();
        }

        // POST: api/GuildConfigs
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<GuildConfig>> PostGuildConfig(GuildConfig guildConfig)
        {
            _context.GuildConfigs.Add(guildConfig);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetGuildConfig", new { id = guildConfig.GuildId }, guildConfig);
        }

        // DELETE: api/GuildConfigs/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteGuildConfig(int id)
        {
            var guildConfig = await _context.GuildConfigs.FindAsync(id);
            if (guildConfig == null) return NotFound();

            _context.GuildConfigs.Remove(guildConfig);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool GuildConfigExists(int id)
        {
            return EntityFrameworkQueryableExtensions.AnyAsync(_context.GuildConfigs, e => e.Id == id).Result;
        }
    }
}