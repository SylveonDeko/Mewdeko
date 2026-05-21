using Mewdeko.Controllers.Common.Todo;
using Mewdeko.Modules.Todo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for managing todo lists and items via API
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class TodoController(
    TodoService todoService,
    IDashboardAuditContext auditContext)
    : Controller
{
    /// <summary>
    ///     Gets all todo lists accessible to a user in a guild
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="userId">The user ID</param>
    /// <returns>List of accessible todo lists</returns>
    [HttpGet("lists/{userId}")]
    public async Task<IActionResult> GetTodoLists(ulong guildId, ulong userId)
    {
        var lists = await todoService.GetUserTodoListsAsync(guildId, userId);
        return Ok(lists);
    }

    /// <summary>
    ///     Gets a specific todo list by ID
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="listId">The list ID</param>
    /// <param name="userId">The user ID requesting access</param>
    /// <returns>The todo list if accessible</returns>
    [HttpGet("lists/{listId}/{userId}")]
    public async Task<IActionResult> GetTodoList(ulong guildId, int listId, ulong userId)
    {
        var list = await todoService.GetTodoListAsync(listId, userId, guildId);
        if (list == null)
            return NotFound("Todo list not found or access denied");

        return Ok(list);
    }

    /// <summary>
    ///     Gets all items in a todo list
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="listId">The list ID</param>
    /// <param name="userId">The user ID requesting access</param>
    /// <param name="includeCompleted">Whether to include completed items</param>
    /// <returns>List of todo items</returns>
    [HttpGet("lists/{listId}/items/{userId}")]
    public async Task<IActionResult> GetTodoItems(ulong guildId, int listId, ulong userId,
        bool includeCompleted = false)
    {
        var items = await todoService.GetTodoItemsAsync(listId, userId, includeCompleted);
        return Ok(items);
    }

    /// <summary>
    ///     Creates a new todo list
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="request">The list creation request</param>
    /// <returns>The created todo list</returns>
    [HttpPost("lists")]
    public async Task<IActionResult> CreateTodoList(ulong guildId, [FromBody] CreateTodoListRequest request)
    {
        var list = await todoService.CreateTodoListAsync(guildId, request.UserId, request.Name, request.Description,
            request.IsServerList);
        if (list == null)
            return BadRequest("Failed to create todo list");

        return CreatedAtAction(nameof(GetTodoList), new
        {
            guildId, listId = list.Id, userId = request.UserId
        }, list);
    }

    /// <summary>
    ///     Adds an item to a todo list
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="listId">The list ID</param>
    /// <param name="request">The item creation request</param>
    /// <returns>The created todo item</returns>
    [HttpPost("lists/{listId}/items")]
    public async Task<IActionResult> AddTodoItem(ulong guildId, int listId, [FromBody] AddTodoItemRequest request)
    {
        var item = await todoService.AddTodoItemAsync(listId, request.UserId, request.Title, request.Description,
            request.Priority, request.DueDate);
        if (item == null)
            return BadRequest("Failed to add todo item");

        return Ok(item);
    }

    /// <summary>
    ///     Completes a todo item
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="itemId">The item ID</param>
    /// <param name="userId">The user ID</param>
    /// <returns>Success or error response</returns>
    [HttpPut("items/{itemId}/complete/{userId}")]
    public async Task<IActionResult> CompleteTodoItem(ulong guildId, int itemId, ulong userId)
    {
        var success = await todoService.CompleteTodoItemAsync(itemId, userId);
        if (!success)
            return BadRequest("Failed to complete todo item");

        return Ok();
    }


    /// <summary>
    ///     Deletes a todo item
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="itemId">The item ID</param>
    /// <param name="userId">The user ID</param>
    /// <returns>Success or error response</returns>
    [HttpDelete("items/{itemId}/{userId}")]
    public async Task<IActionResult> DeleteTodoItem(ulong guildId, int itemId, ulong userId)
    {
        var success = await todoService.DeleteTodoItemAsync(itemId, userId);
        if (!success)
            return BadRequest("Failed to delete todo item");

        return Ok();
    }

    /// <summary>
    ///     Deletes a todo list
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="listId">The list ID</param>
    /// <param name="userId">The user ID</param>
    /// <returns>Success or error response</returns>
    [HttpDelete("lists/{listId}/{userId}")]
    public async Task<IActionResult> DeleteTodoList(ulong guildId, int listId, ulong userId)
    {
        auditContext.RecordBefore(await todoService.GetTodoListAsync(listId, userId, guildId));
        var success = await todoService.DeleteTodoListAsync(listId, userId);
        if (!success)
            return BadRequest("Failed to delete todo list");

        return Ok();
    }

    /// <summary>
    ///     Updates a todo item
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="itemId">The item ID</param>
    /// <param name="request">The update request</param>
    /// <returns>Success or error response</returns>
    [HttpPut("items/{itemId}")]
    public async Task<IActionResult> UpdateTodoItem(ulong guildId, int itemId, [FromBody] UpdateTodoItemRequest request)
    {
        var success = await todoService.EditTodoItemAsync(itemId, request.UserId, request.Title, request.Description);
        if (!success)
            return BadRequest("Failed to update todo item");

        return Ok();
    }

    /// <summary>
    ///     Sets due date for a todo item
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="itemId">The item ID</param>
    /// <param name="request">The due date request</param>
    /// <returns>Success or error response</returns>
    [HttpPut("items/{itemId}/duedate")]
    public async Task<IActionResult> SetTodoItemDueDate(ulong guildId, int itemId, [FromBody] SetDueDateRequest request)
    {
        var success = await todoService.SetTodoItemDueDateAsync(itemId, request.UserId, request.DueDate);
        if (!success)
            return BadRequest("Failed to set due date");

        return Ok();
    }

    /// <summary>
    ///     Adds a tag to a todo item
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="itemId">The item ID</param>
    /// <param name="request">The tag request</param>
    /// <returns>Success or error response</returns>
    [HttpPost("items/{itemId}/tags")]
    public async Task<IActionResult> AddTodoItemTag(ulong guildId, int itemId, [FromBody] TagRequest request)
    {
        var success = await todoService.AddTagToTodoItemAsync(itemId, request.UserId, request.Tag);
        if (!success)
            return BadRequest("Failed to add tag");

        return Ok();
    }

    /// <summary>
    ///     Removes a tag from a todo item
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="itemId">The item ID</param>
    /// <param name="request">The tag request</param>
    /// <returns>Success or error response</returns>
    [HttpDelete("items/{itemId}/tags")]
    public async Task<IActionResult> RemoveTodoItemTag(ulong guildId, int itemId, [FromBody] TagRequest request)
    {
        var success = await todoService.RemoveTagFromTodoItemAsync(itemId, request.UserId, request.Tag);
        if (!success)
            return BadRequest("Failed to remove tag");

        return Ok();
    }

    /// <summary>
    ///     Gets permissions for a todo list
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="listId">The list ID</param>
    /// <param name="userId">The user ID</param>
    /// <returns>List of permissions</returns>
    [HttpGet("lists/{listId}/permissions/{userId}")]
    public async Task<IActionResult> GetTodoListPermissions(ulong guildId, int listId, ulong userId)
    {
        var permissions = await todoService.GetTodoListPermissionsAsync(listId, userId);
        return Ok(permissions);
    }

    /// <summary>
    ///     Grants permissions to a user for a todo list
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="listId">The list ID</param>
    /// <param name="request">The permission grant request</param>
    /// <returns>Success or error response</returns>
    [HttpPost("lists/{listId}/permissions")]
    public async Task<IActionResult> GrantTodoListPermissions(ulong guildId, int listId,
        [FromBody] GrantPermissionRequest request)
    {
        var success = await todoService.GrantPermissionsAsync(listId, request.TargetUserId, request.RequestingUserId,
            request.CanView, request.CanEdit, request.CanManage);
        if (!success)
            return BadRequest("Failed to grant permissions");

        return Ok();
    }

    /// <summary>
    ///     Revokes all permissions for a user on a todo list
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="listId">The list ID</param>
    /// <param name="targetUserId">The user to revoke permissions from</param>
    /// <param name="requestingUserId">The user requesting the revocation</param>
    /// <returns>Success or error response</returns>
    [HttpDelete("lists/{listId}/permissions/{targetUserId}/{requestingUserId}")]
    public async Task<IActionResult> RevokeTodoListPermissions(ulong guildId, int listId, ulong targetUserId,
        ulong requestingUserId)
    {
        var success = await todoService.RevokeTodoListPermissionsAsync(listId, targetUserId, requestingUserId);
        if (!success)
            return BadRequest("Failed to revoke permissions");

        return Ok();
    }
}