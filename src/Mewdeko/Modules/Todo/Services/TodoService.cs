using DataModel;
using LinqToDB;
using LinqToDB.Async;

namespace Mewdeko.Modules.Todo.Services;

/// <summary>
///     Service for managing todo lists and items with granular permissions.
/// </summary>
public class TodoService : INService
{
    private readonly IDataConnectionFactory dbFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TodoService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    public TodoService(
        IDataConnectionFactory dbFactory)
    {
        this.dbFactory = dbFactory;
    }

    /// <summary>
    ///     Creates a new todo list for a user or server.
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="ownerId">The user who owns this list</param>
    /// <param name="name">The name of the todo list</param>
    /// <param name="description">Optional description</param>
    /// <param name="isServerList">Whether this is a server-wide list</param>
    /// <param name="isPublic">Whether others can view this list</param>
    /// <param name="color">Optional embed color</param>
    /// <returns>The created todo list or null if failed</returns>
    public async Task<TodoList?> CreateTodoListAsync(ulong guildId, ulong ownerId, string name,
        string? description = null, bool isServerList = false, bool isPublic = true, string? color = null)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        // Check if list with same name already exists for this user/server
        var existingList = await ctx.TodoLists
            .Where(x => x.GuildId == guildId && x.OwnerId == ownerId &&
                        x.Name == name && x.IsServerList == isServerList)
            .FirstOrDefaultAsync();

        if (existingList is not null)
            return null; // Already exists

        var todoList = new TodoList
        {
            Name = name,
            Description = description,
            GuildId = guildId,
            OwnerId = ownerId,
            IsServerList = isServerList,
            IsPublic = isPublic,
            Color = color ?? "#7289da",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        todoList.Id = await ctx.InsertWithInt32IdentityAsync(todoList);
        return todoList;
    }

    /// <summary>
    ///     Gets all todo lists for a user in a guild.
    /// </summary>
    /// <param name="guildId">The guild ID</param>
    /// <param name="userId">The user ID</param>
    /// <param name="includeServerLists">Whether to include server lists the user can view</param>
    /// <returns>List of accessible todo lists</returns>
    public async Task<List<TodoList>> GetUserTodoListsAsync(ulong guildId, ulong userId, bool includeServerLists = true)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        var query = ctx.TodoLists.Where(x => x.GuildId == guildId);

        if (includeServerLists)
        {
            // Get personal lists + server lists user can view + server lists with explicit permissions
            query = query.Where(x =>
                (x.OwnerId == userId && !x.IsServerList) || // Personal lists
                (x.IsServerList && x.IsPublic) || // Public server lists
                ctx.TodoListPermissions.Any(p =>
                    p.TodoListId == x.Id && p.UserId == userId && p.CanView)); // Explicit permissions
        }
        else
        {
            // Only personal lists
            query = query.Where(x => x.OwnerId == userId && !x.IsServerList);
        }

        return await query.OrderBy(x => x.Name).ToListAsync();
    }

    /// <summary>
    ///     Gets a specific todo list if the user has access to it.
    /// </summary>
    /// <param name="listId">The list ID</param>
    /// <param name="userId">The user requesting access</param>
    /// <param name="guildId">The guild ID</param>
    /// <returns>The todo list if accessible, null otherwise</returns>
    public async Task<TodoList?> GetTodoListAsync(int listId, ulong userId, ulong guildId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        var todoList = await ctx.TodoLists
            .Where(x => x.Id == listId && x.GuildId == guildId)
            .FirstOrDefaultAsync();

        if (todoList is null)
            return null;

        // Check if user has access
        if (await CanUserViewListAsync(listId, userId))
            return todoList;

        return null;
    }

    /// <summary>
    ///     Adds a new todo item to a list.
    /// </summary>
    /// <param name="listId">The list ID</param>
    /// <param name="userId">The user creating the item</param>
    /// <param name="title">The item title</param>
    /// <param name="description">Optional description</param>
    /// <param name="priority">Priority level (1-4)</param>
    /// <param name="dueDate">Optional due date</param>
    /// <param name="tags">Optional tags</param>
    /// <param name="reminderTime">Optional reminder time</param>
    /// <returns>The created todo item or null if failed</returns>
    public async Task<TodoItem?> AddTodoItemAsync(int listId, ulong userId, string title,
        string? description = null, int priority = 1, DateTime? dueDate = null,
        string[]? tags = null, DateTime? reminderTime = null)
    {
        if (!await CanUserAddToListAsync(listId, userId))
            return null;

        await using var ctx = await dbFactory.CreateConnectionAsync();

        // Get next position
        var maxPosition = await ctx.TodoItems
            .Where(x => x.TodoListId == listId)
            .MaxAsync(x => (int?)x.Position) ?? 0;

        var todoItem = new TodoItem
        {
            TodoListId = listId,
            Title = title,
            Description = description,
            Priority = Math.Clamp(priority, 1, 4),
            DueDate = dueDate,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId,
            Tags = tags ?? [],
            ReminderTime = reminderTime,
            Position = maxPosition + 1
        };

        todoItem.Id = await ctx.InsertWithInt32IdentityAsync(todoItem);

        // Update list timestamp
        await ctx.TodoLists
            .Where(x => x.Id == listId)
            .Set(x => x.UpdatedAt, DateTime.UtcNow)
            .UpdateAsync();

        return todoItem;
    }

    /// <summary>
    ///     Gets all todo items in a list that the user can view.
    /// </summary>
    /// <param name="listId">The list ID</param>
    /// <param name="userId">The user requesting items</param>
    /// <param name="includeCompleted">Whether to include completed items</param>
    /// <returns>List of todo items</returns>
    public async Task<List<TodoItem>> GetTodoItemsAsync(int listId, ulong userId, bool includeCompleted = true)
    {
        if (!await CanUserViewListAsync(listId, userId))
            return [];

        await using var ctx = await dbFactory.CreateConnectionAsync();

        var query = ctx.TodoItems.Where(x => x.TodoListId == listId);

        if (!includeCompleted)
            query = query.Where(x => !x.IsCompleted);

        return await query
            .OrderBy(x => x.IsCompleted)
            .ThenBy(x => x.Position)
            .ThenByDescending(x => x.Priority)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    ///     Completes a todo item.
    /// </summary>
    /// <param name="itemId">The item ID</param>
    /// <param name="userId">The user completing the item</param>
    /// <returns>True if successful</returns>
    public async Task<bool> CompleteTodoItemAsync(int itemId, ulong userId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        var item = await ctx.TodoItems.Where(x => x.Id == itemId).FirstOrDefaultAsync();
        if (item is null) return false;

        if (!await CanUserCompleteItemAsync(item.TodoListId, userId, item.CreatedBy))
            return false;

        var updated = await ctx.TodoItems
            .Where(x => x.Id == itemId && !x.IsCompleted)
            .Set(x => x.IsCompleted, true)
            .Set(x => x.CompletedAt, DateTime.UtcNow)
            .Set(x => x.CompletedBy, userId)
            .UpdateAsync();

        if (updated > 0)
        {
            // Update list timestamp
            await ctx.TodoLists
                .Where(x => x.Id == item.TodoListId)
                .Set(x => x.UpdatedAt, DateTime.UtcNow)
                .UpdateAsync();
        }

        return updated > 0;
    }

    /// <summary>
    ///     Grants permissions to a user for a todo list.
    /// </summary>
    /// <param name="listId">The list ID</param>
    /// <param name="targetUserId">The user to grant permissions to</param>
    /// <param name="grantedBy">The user granting permissions</param>
    /// <param name="canView">Can view the list</param>
    /// <param name="canAdd">Can add items</param>
    /// <param name="canEdit">Can edit others' items</param>
    /// <param name="canComplete">Can complete others' items</param>
    /// <param name="canDelete">Can delete others' items</param>
    /// <param name="canManageList">Can manage list settings and permissions</param>
    /// <returns>True if successful</returns>
    public async Task<bool> GrantPermissionsAsync(int listId, ulong targetUserId, ulong grantedBy,
        bool canView = true, bool canAdd = false, bool canEdit = false, bool canComplete = false,
        bool canDelete = false, bool canManageList = false)
    {
        if (!await CanUserManageListAsync(listId, grantedBy))
            return false;

        await using var ctx = await dbFactory.CreateConnectionAsync();

        // Remove existing permission if it exists
        await ctx.TodoListPermissions
            .Where(x => x.TodoListId == listId && x.UserId == targetUserId)
            .DeleteAsync();

        // Insert new permission
        var permission = new TodoListPermission
        {
            TodoListId = listId,
            UserId = targetUserId,
            CanView = canView,
            CanAdd = canAdd,
            CanEdit = canEdit,
            CanComplete = canComplete,
            CanDelete = canDelete,
            CanManageList = canManageList,
            GrantedBy = grantedBy,
            GrantedAt = DateTime.UtcNow
        };

        await ctx.InsertAsync(permission);
        return true;
    }

    /// <summary>
    ///     Checks if a user can view a todo list.
    /// </summary>
    private async Task<bool> CanUserViewListAsync(int listId, ulong userId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        var list = await ctx.TodoLists.Where(x => x.Id == listId).FirstOrDefaultAsync();
        if (list is null) return false;

        // Owner can always view
        if (list.OwnerId == userId) return true;

        // For server lists, check if public or has explicit permission
        if (list.IsServerList)
        {
            if (list.IsPublic) return true;

            return await ctx.TodoListPermissions
                .AnyAsync(x => x.TodoListId == listId && x.UserId == userId && x.CanView);
        }

        // For personal lists, check explicit permission
        return await ctx.TodoListPermissions
            .AnyAsync(x => x.TodoListId == listId && x.UserId == userId && x.CanView);
    }

    /// <summary>
    ///     Checks if a user can add items to a todo list.
    /// </summary>
    private async Task<bool> CanUserAddToListAsync(int listId, ulong userId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        var list = await ctx.TodoLists.Where(x => x.Id == listId).FirstOrDefaultAsync();
        if (list is null) return false;

        // Owner can always add
        if (list.OwnerId == userId) return true;

        // Check explicit permission
        return await ctx.TodoListPermissions
            .AnyAsync(x => x.TodoListId == listId && x.UserId == userId && x.CanAdd);
    }

    /// <summary>
    ///     Checks if a user can complete a todo item.
    /// </summary>
    private async Task<bool> CanUserCompleteItemAsync(int listId, ulong userId, ulong itemCreatorId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        var list = await ctx.TodoLists.Where(x => x.Id == listId).FirstOrDefaultAsync();
        if (list is null) return false;

        // Owner can always complete
        if (list.OwnerId == userId) return true;

        // Item creator can complete their own items
        if (itemCreatorId == userId) return true;

        // Check explicit permission to complete others' items
        return await ctx.TodoListPermissions
            .AnyAsync(x => x.TodoListId == listId && x.UserId == userId && x.CanComplete);
    }

    /// <summary>
    ///     Checks if a user can manage a todo list (settings, permissions).
    /// </summary>
    private async Task<bool> CanUserManageListAsync(int listId, ulong userId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        var list = await ctx.TodoLists.Where(x => x.Id == listId).FirstOrDefaultAsync();
        if (list is null) return false;

        // Owner can always manage
        if (list.OwnerId == userId) return true;

        // Check explicit permission
        return await ctx.TodoListPermissions
            .AnyAsync(x => x.TodoListId == listId && x.UserId == userId && x.CanManageList);
    }

    /// <summary>
    ///     Deletes a todo list and all its items.
    /// </summary>
    /// <param name="listId">The list ID</param>
    /// <param name="userId">The user requesting deletion</param>
    /// <returns>True if successful</returns>
    public async Task<bool> DeleteTodoListAsync(int listId, ulong userId)
    {
        if (!await CanUserManageListAsync(listId, userId))
            return false;

        await using var ctx = await dbFactory.CreateConnectionAsync();

        // Delete permissions first (foreign key constraint)
        await ctx.TodoListPermissions.Where(x => x.TodoListId == listId).DeleteAsync();

        // Delete items (foreign key constraint)
        await ctx.TodoItems.Where(x => x.TodoListId == listId).DeleteAsync();

        // Delete the list
        var deleted = await ctx.TodoLists.Where(x => x.Id == listId).DeleteAsync();

        return deleted > 0;
    }

    /// <summary>
    ///     Deletes a todo item.
    /// </summary>
    /// <param name="itemId">The item ID</param>
    /// <param name="userId">The user requesting deletion</param>
    /// <returns>True if successful</returns>
    public async Task<bool> DeleteTodoItemAsync(int itemId, ulong userId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        var item = await ctx.TodoItems.Where(x => x.Id == itemId).FirstOrDefaultAsync();
        if (item is null) return false;

        var list = await ctx.TodoLists.Where(x => x.Id == item.TodoListId).FirstOrDefaultAsync();
        if (list is null) return false;

        // Owner can delete any item
        if (list.OwnerId == userId)
        {
            var deleted = await ctx.TodoItems.Where(x => x.Id == itemId).DeleteAsync();
            if (deleted > 0)
            {
                await ctx.TodoLists
                    .Where(x => x.Id == item.TodoListId)
                    .Set(x => x.UpdatedAt, DateTime.UtcNow)
                    .UpdateAsync();
            }

            return deleted > 0;
        }

        // Item creator can delete their own items
        if (item.CreatedBy == userId)
        {
            var deleted = await ctx.TodoItems.Where(x => x.Id == itemId).DeleteAsync();
            if (deleted > 0)
            {
                await ctx.TodoLists
                    .Where(x => x.Id == item.TodoListId)
                    .Set(x => x.UpdatedAt, DateTime.UtcNow)
                    .UpdateAsync();
            }

            return deleted > 0;
        }

        // Check explicit permission to delete others' items
        var hasPermission = await ctx.TodoListPermissions
            .AnyAsync(x => x.TodoListId == item.TodoListId && x.UserId == userId && x.CanDelete);

        if (hasPermission)
        {
            var deleted = await ctx.TodoItems.Where(x => x.Id == itemId).DeleteAsync();
            if (deleted > 0)
            {
                await ctx.TodoLists
                    .Where(x => x.Id == item.TodoListId)
                    .Set(x => x.UpdatedAt, DateTime.UtcNow)
                    .UpdateAsync();
            }

            return deleted > 0;
        }

        return false;
    }

    /// <summary>
    ///     Sets the due date for a todo item.
    /// </summary>
    /// <param name="itemId">The item ID</param>
    /// <param name="userId">The user setting the due date</param>
    /// <param name="dueDate">The due date to set</param>
    /// <returns>True if successful</returns>
    public async Task<bool> SetTodoItemDueDateAsync(int itemId, ulong userId, DateTime? dueDate)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        var item = await ctx.TodoItems.Where(x => x.Id == itemId).FirstOrDefaultAsync();
        if (item is null) return false;

        if (!await CanUserEditItemAsync(item.TodoListId, userId, item.CreatedBy))
            return false;

        var updated = await ctx.TodoItems
            .Where(x => x.Id == itemId)
            .Set(x => x.DueDate, dueDate)
            .UpdateAsync();

        if (updated > 0)
        {
            await ctx.TodoLists
                .Where(x => x.Id == item.TodoListId)
                .Set(x => x.UpdatedAt, DateTime.UtcNow)
                .UpdateAsync();
        }

        return updated > 0;
    }

    /// <summary>
    ///     Sets the reminder time for a todo item.
    /// </summary>
    /// <param name="itemId">The item ID</param>
    /// <param name="userId">The user setting the reminder</param>
    /// <param name="reminderTime">The reminder time to set</param>
    /// <returns>True if successful</returns>
    public async Task<bool> SetTodoItemReminderAsync(int itemId, ulong userId, DateTime? reminderTime)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        var item = await ctx.TodoItems.Where(x => x.Id == itemId).FirstOrDefaultAsync();
        if (item is null) return false;

        if (!await CanUserEditItemAsync(item.TodoListId, userId, item.CreatedBy))
            return false;

        var updated = await ctx.TodoItems
            .Where(x => x.Id == itemId)
            .Set(x => x.ReminderTime, reminderTime)
            .UpdateAsync();

        if (updated > 0)
        {
            await ctx.TodoLists
                .Where(x => x.Id == item.TodoListId)
                .Set(x => x.UpdatedAt, DateTime.UtcNow)
                .UpdateAsync();
        }

        return updated > 0;
    }

    /// <summary>
    ///     Adds a tag to a todo item.
    /// </summary>
    /// <param name="itemId">The item ID</param>
    /// <param name="userId">The user adding the tag</param>
    /// <param name="tag">The tag to add</param>
    /// <returns>True if successful</returns>
    public async Task<bool> AddTagToTodoItemAsync(int itemId, ulong userId, string tag)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        var item = await ctx.TodoItems.Where(x => x.Id == itemId).FirstOrDefaultAsync();
        if (item is null) return false;

        if (!await CanUserEditItemAsync(item.TodoListId, userId, item.CreatedBy))
            return false;

        // Add tag if not already present
        var currentTags = item.Tags?.ToList() ?? [];
        if (!currentTags.Contains(tag, StringComparer.OrdinalIgnoreCase))
        {
            currentTags.Add(tag);

            var updated = await ctx.TodoItems
                .Where(x => x.Id == itemId)
                .Set(x => x.Tags, currentTags.ToArray())
                .UpdateAsync();

            if (updated > 0)
            {
                await ctx.TodoLists
                    .Where(x => x.Id == item.TodoListId)
                    .Set(x => x.UpdatedAt, DateTime.UtcNow)
                    .UpdateAsync();
            }

            return updated > 0;
        }

        return true; // Tag already exists
    }

    /// <summary>
    ///     Removes a tag from a todo item.
    /// </summary>
    /// <param name="itemId">The item ID</param>
    /// <param name="userId">The user removing the tag</param>
    /// <param name="tag">The tag to remove</param>
    /// <returns>True if successful</returns>
    public async Task<bool> RemoveTagFromTodoItemAsync(int itemId, ulong userId, string tag)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        var item = await ctx.TodoItems.Where(x => x.Id == itemId).FirstOrDefaultAsync();
        if (item is null) return false;

        if (!await CanUserEditItemAsync(item.TodoListId, userId, item.CreatedBy))
            return false;

        // Remove tag if present
        var currentTags = item.Tags?.ToList() ?? [];
        var originalCount = currentTags.Count;
        currentTags.RemoveAll(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase));

        if (currentTags.Count != originalCount)
        {
            var updated = await ctx.TodoItems
                .Where(x => x.Id == itemId)
                .Set(x => x.Tags, currentTags.ToArray())
                .UpdateAsync();

            if (updated > 0)
            {
                await ctx.TodoLists
                    .Where(x => x.Id == item.TodoListId)
                    .Set(x => x.UpdatedAt, DateTime.UtcNow)
                    .UpdateAsync();
            }

            return updated > 0;
        }

        return true; // Tag didn't exist
    }

    /// <summary>
    ///     Edits a todo item's title, description, and optionally its priority and due date.
    /// </summary>
    /// <param name="itemId">The item ID</param>
    /// <param name="userId">The user editing the item</param>
    /// <param name="title">The new title, or null to keep the current title</param>
    /// <param name="description">The new description</param>
    /// <param name="priority">The new priority (clamped to 1-4), or null to keep the current priority</param>
    /// <param name="dueDate">The new due date, or null to keep the current due date</param>
    /// <returns>True if successful</returns>
    public async Task<bool> EditTodoItemAsync(int itemId, ulong userId, string? title, string? description = null,
        int? priority = null, DateTime? dueDate = null)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        var item = await ctx.TodoItems.Where(x => x.Id == itemId).FirstOrDefaultAsync();
        if (item is null) return false;

        if (!await CanUserEditItemAsync(item.TodoListId, userId, item.CreatedBy))
            return false;

        var newTitle = string.IsNullOrWhiteSpace(title) ? item.Title : title;
        var newPriority = priority.HasValue ? Math.Clamp(priority.Value, 1, 4) : item.Priority;
        var newDueDate = dueDate ?? item.DueDate;

        var updated = await ctx.TodoItems
            .Where(x => x.Id == itemId)
            .Set(x => x.Title, newTitle)
            .Set(x => x.Description, description)
            .Set(x => x.Priority, newPriority)
            .Set(x => x.DueDate, newDueDate)
            .UpdateAsync();

        if (updated > 0)
        {
            await ctx.TodoLists
                .Where(x => x.Id == item.TodoListId)
                .Set(x => x.UpdatedAt, DateTime.UtcNow)
                .UpdateAsync();
        }

        return updated > 0;
    }

    /// <summary>
    ///     Reorders a todo item to a new position.
    /// </summary>
    /// <param name="itemId">The item ID</param>
    /// <param name="userId">The user reordering the item</param>
    /// <param name="newPosition">The new position</param>
    /// <returns>True if successful</returns>
    public async Task<bool> ReorderTodoItemAsync(int itemId, ulong userId, int newPosition)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        var item = await ctx.TodoItems.Where(x => x.Id == itemId).FirstOrDefaultAsync();
        if (item is null) return false;

        if (!await CanUserEditItemAsync(item.TodoListId, userId, item.CreatedBy))
            return false;

        // Get all items in the list ordered by position
        var allItems = await ctx.TodoItems
            .Where(x => x.TodoListId == item.TodoListId)
            .OrderBy(x => x.Position)
            .ToListAsync();

        var currentItem = allItems.FirstOrDefault(x => x.Id == itemId);
        if (currentItem is null) return false;

        // Remove item from current position
        allItems.Remove(currentItem);

        // Insert at new position (0-indexed)
        var insertIndex = Math.Max(0, Math.Min(newPosition - 1, allItems.Count));
        allItems.Insert(insertIndex, currentItem);

        // Update all positions
        for (var i = 0; i < allItems.Count; i++)
        {
            await ctx.TodoItems
                .Where(x => x.Id == allItems[i].Id)
                .Set(x => x.Position, i + 1)
                .UpdateAsync();
        }

        // Update list timestamp
        await ctx.TodoLists
            .Where(x => x.Id == item.TodoListId)
            .Set(x => x.UpdatedAt, DateTime.UtcNow)
            .UpdateAsync();

        return true;
    }

    /// <summary>
    ///     Sets the color for a todo list.
    /// </summary>
    /// <param name="listId">The list ID</param>
    /// <param name="userId">The user setting the color</param>
    /// <param name="color">The color hex code</param>
    /// <returns>True if successful</returns>
    public async Task<bool> SetTodoListColorAsync(int listId, ulong userId, string color)
    {
        if (!await CanUserManageListAsync(listId, userId))
            return false;

        await using var ctx = await dbFactory.CreateConnectionAsync();

        var updated = await ctx.TodoLists
            .Where(x => x.Id == listId)
            .Set(x => x.Color, color)
            .Set(x => x.UpdatedAt, DateTime.UtcNow)
            .UpdateAsync();

        return updated > 0;
    }

    /// <summary>
    ///     Toggles the privacy setting for a todo list.
    /// </summary>
    /// <param name="listId">The list ID</param>
    /// <param name="userId">The user toggling privacy</param>
    /// <returns>The new privacy state, or null if failed</returns>
    public async Task<bool?> ToggleTodoListPrivacyAsync(int listId, ulong userId)
    {
        if (!await CanUserManageListAsync(listId, userId))
            return null;

        await using var ctx = await dbFactory.CreateConnectionAsync();

        var list = await ctx.TodoLists.Where(x => x.Id == listId).FirstOrDefaultAsync();
        if (list is null) return null;

        var newPrivacySetting = !list.IsPublic;

        var updated = await ctx.TodoLists
            .Where(x => x.Id == listId)
            .Set(x => x.IsPublic, newPrivacySetting)
            .Set(x => x.UpdatedAt, DateTime.UtcNow)
            .UpdateAsync();

        return updated > 0 ? newPrivacySetting : null;
    }

    /// <summary>
    ///     Gets all permissions for a todo list.
    /// </summary>
    /// <param name="listId">The list ID</param>
    /// <param name="userId">The user requesting permissions</param>
    /// <returns>List of permissions or null if no access</returns>
    public async Task<List<TodoListPermission>?> GetTodoListPermissionsAsync(int listId, ulong userId)
    {
        if (!await CanUserViewListAsync(listId, userId))
            return null;

        await using var ctx = await dbFactory.CreateConnectionAsync();

        return await ctx.TodoListPermissions
            .Where(x => x.TodoListId == listId)
            .ToListAsync();
    }

    /// <summary>
    ///     Revokes all permissions for a user on a todo list.
    /// </summary>
    /// <param name="listId">The list ID</param>
    /// <param name="targetUserId">The user to revoke permissions from</param>
    /// <param name="requestingUserId">The user requesting the revocation</param>
    /// <returns>True if successful</returns>
    public async Task<bool> RevokeTodoListPermissionsAsync(int listId, ulong targetUserId, ulong requestingUserId)
    {
        if (!await CanUserManageListAsync(listId, requestingUserId))
            return false;

        await using var ctx = await dbFactory.CreateConnectionAsync();

        var deleted = await ctx.TodoListPermissions
            .Where(x => x.TodoListId == listId && x.UserId == targetUserId)
            .DeleteAsync();

        return deleted > 0;
    }

    /// <summary>
    ///     Searches for todo items within a list.
    /// </summary>
    /// <param name="listId">The list ID</param>
    /// <param name="userId">The user performing the search</param>
    /// <param name="query">The search query</param>
    /// <param name="includeCompleted">Include completed items in search</param>
    /// <returns>List of matching items or null if no access</returns>
    public async Task<List<TodoItem>?> SearchTodoItemsAsync(int listId, ulong userId, string query,
        bool includeCompleted = true)
    {
        if (!await CanUserViewListAsync(listId, userId))
            return null;

        await using var ctx = await dbFactory.CreateConnectionAsync();

        var queryLower = query.ToLower();

        var queryBuilder = ctx.TodoItems.Where(x => x.TodoListId == listId);

        if (!includeCompleted)
            queryBuilder = queryBuilder.Where(x => !x.IsCompleted);

        return await queryBuilder
            .Where(x => x.Title.ToLower().Contains(queryLower) ||
                        (x.Description != null && x.Description.ToLower().Contains(queryLower)) ||
                        (x.Tags != null && x.Tags.Any(tag => tag.ToLower().Contains(queryLower))))
            .OrderBy(x => x.IsCompleted)
            .ThenBy(x => x.Position)
            .ThenByDescending(x => x.Priority)
            .ToListAsync();
    }

    /// <summary>
    ///     Filters todo items by criteria.
    /// </summary>
    /// <param name="listId">The list ID</param>
    /// <param name="userId">The user performing the filter</param>
    /// <param name="tag">Filter by tag (optional)</param>
    /// <param name="priority">Filter by priority (optional)</param>
    /// <param name="includeCompleted">Include completed items</param>
    /// <returns>List of filtered items or null if no access</returns>
    public async Task<List<TodoItem>?> FilterTodoItemsAsync(int listId, ulong userId, string? tag = null,
        int? priority = null, bool includeCompleted = true)
    {
        if (!await CanUserViewListAsync(listId, userId))
            return null;

        await using var ctx = await dbFactory.CreateConnectionAsync();

        var queryBuilder = ctx.TodoItems.Where(x => x.TodoListId == listId);

        if (!includeCompleted)
            queryBuilder = queryBuilder.Where(x => !x.IsCompleted);

        if (!string.IsNullOrEmpty(tag))
            queryBuilder = queryBuilder.Where(x => x.Tags != null && x.Tags.Any(t => t.ToLower() == tag.ToLower()));

        if (priority.HasValue)
            queryBuilder = queryBuilder.Where(x => x.Priority == priority.Value);

        return await queryBuilder
            .OrderBy(x => x.IsCompleted)
            .ThenBy(x => x.Position)
            .ThenByDescending(x => x.Priority)
            .ToListAsync();
    }

    /// <summary>
    ///     Checks if a user can edit a todo item.
    /// </summary>
    private async Task<bool> CanUserEditItemAsync(int listId, ulong userId, ulong itemCreatorId)
    {
        await using var ctx = await dbFactory.CreateConnectionAsync();

        var list = await ctx.TodoLists.Where(x => x.Id == listId).FirstOrDefaultAsync();
        if (list is null) return false;

        // Owner can always edit
        if (list.OwnerId == userId) return true;

        // Item creator can edit their own items
        if (itemCreatorId == userId) return true;

        // Check explicit permission to edit others' items
        return await ctx.TodoListPermissions
            .AnyAsync(x => x.TodoListId == listId && x.UserId == userId && x.CanEdit);
    }
}