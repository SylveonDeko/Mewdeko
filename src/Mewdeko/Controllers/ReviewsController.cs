using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using LinqToDB;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Services;
using Mewdeko.Database.Models;

namespace Mewdeko.Controllers
{
    /// <summary>
    ///
    /// </summary>
    [ApiController]
    [Route("botapi/[controller]")]
    [Authorize("ApiKeyPolicy")]
    public class ReviewsController(DbContextProvider dbContext, DiscordShardedClient client) : Controller
    {

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> GetReviews()
        {
            await using var db = await dbContext.GetContextAsync();
            var reviews = await db.BotReviews.ToListAsync();
            return Ok(reviews);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="review"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> SubmitReview([FromBody] BotReviews review)
        {
            await using var db = await dbContext.GetContextAsync();
            var user = await client.Rest.GetUserAsync(review.UserId);
            review.AvatarUrl = user.GetAvatarUrl();
            review.Username = user.Username;

            await db.BotReviews.AddAsync(review);
            await db.SaveChangesAsync();
            return Ok(review);
        }

        /// <summary>
        /// Yeets a review
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReview(int id)
        {
            await using var db = await dbContext.GetContextAsync();
            var review = await db.BotReviews.FindAsync(id);
            if (review == null)
                return NotFound();
            db.BotReviews.Remove(review);
            await db.SaveChangesAsync();
            return Ok();
        }
    }
}