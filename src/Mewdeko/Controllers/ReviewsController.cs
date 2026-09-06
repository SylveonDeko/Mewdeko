using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Controllers.Common.DashboardAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
/// </summary>
[ApiController]
[Route("botapi/[controller]")]
[Authorize("ApiKeyPolicy")]
public class ReviewsController(DiscordShardedClient client, IDataConnectionFactory dbFactory) : Controller
{
    /// <summary>
    ///     Retrieves all bot reviews from the database
    /// </summary>
    /// <returns>A list of all bot reviews</returns>
    [HttpGet]
    public async Task<IActionResult> GetReviews()
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var reviews = await db.BotReviews.ToListAsync();
        return Ok(reviews);
    }

    /// <summary>
    ///     Submits a new bot review to the database
    /// </summary>
    /// <param name="review">The review data to be submitted</param>
    /// <returns>The submitted review with its assigned ID</returns>
    [HttpPost]
    public async Task<IActionResult> SubmitReview([FromBody] BotReview review)
    {
        var authenticatedUserId = await HttpContext.GetDashboardUserIdAsync();
        if (authenticatedUserId is null)
            return Unauthorized("You must be signed in to leave a review.");

        review.UserId = authenticatedUserId.Value;

        await using var db = await dbFactory.CreateConnectionAsync();
        var user = await client.Rest.GetUserAsync(review.UserId);
        review.AvatarUrl = user.GetAvatarUrl();
        review.Username = user.Username;

        if (review.DateAdded == null)
            review.DateAdded = DateTime.UtcNow;

        review.Id = await db.InsertWithInt32IdentityAsync(review);
        return Ok(review);
    }

    /// <summary>
    ///     Yeets a review
    /// </summary>
    /// <param name="id">The ID of the review to delete</param>
    /// <returns>OK if deleted successfully, NotFound if review doesn't exist</returns>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteReview(int id)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var exists = await db.BotReviews
            .AnyAsync(r => r.Id == id);

        if (!exists)
            return NotFound();

        await db.BotReviews
            .Where(r => r.Id == id)
            .DeleteAsync();

        return Ok();
    }
}