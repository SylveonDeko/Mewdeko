using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Database.Enums;

namespace Mewdeko.Modules.Forms.Services;

/// <summary>
///     Service for managing custom forms with conditional logic and Discord integration.
/// </summary>
public class FormsService : INService
{
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<FormsService> logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FormsService" /> class.
    /// </summary>
    /// <param name="client">The Discord client instance.</param>
    /// <param name="dbFactory">Provider for database connections.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public FormsService(
        DiscordShardedClient client,
        IDataConnectionFactory dbFactory,
        ILogger<FormsService> logger)
    {
        this.client = client;
        this.dbFactory = dbFactory;
        this.logger = logger;
    }

    #region Discord Integration

    /// <summary>
    ///     Logs a form submission to the configured Discord channel.
    /// </summary>
    /// <param name="form">The form that was submitted.</param>
    /// <param name="response">The response data.</param>
    /// <param name="answers">The list of answers.</param>
    /// <returns>The Discord message ID if successful.</returns>
    public async Task<ulong?> LogSubmissionToChannelAsync(Form form, FormResponse response, List<FormAnswer> answers)
    {
        if (!form.SubmitChannelId.HasValue)
            return null;

        try
        {
            var guild = client.GetGuild(form.GuildId);
            if (guild == null)
            {
                logger.LogWarning("Guild {GuildId} not found for form submission logging", form.GuildId);
                return null;
            }

            var channel = guild.GetTextChannel(form.SubmitChannelId.Value);
            if (channel == null)
            {
                logger.LogWarning("Channel {ChannelId} not found for form submission logging",
                    form.SubmitChannelId.Value);
                return null;
            }

            var embed = new EmbedBuilder()
                .WithTitle($"📝 New Form Submission: {form.Name}")
                .WithDescription(form.Description)
                .WithColor(Color.Blue)
                .WithFooter($"Response ID: #{response.Id} | Form ID: #{form.Id}")
                .WithTimestamp(response.SubmittedAt);

            // Only add user field if NOT anonymous
            if (response.UserId.HasValue)
            {
                var user = guild.GetUser(response.UserId.Value);
                var username = user?.ToString() ?? response.Username ?? $"<@{response.UserId.Value}>";
                embed.AddField("User", username, true);
            }
            else
            {
                embed.AddField("User", "Anonymous (login required)", true);
            }

            embed.AddField("Submitted", $"<t:{((DateTimeOffset)response.SubmittedAt).ToUnixTimeSeconds()}:R>", true);

            // Get questions for context
            var questions = await GetFormQuestionsAsync(form.Id);
            var questionDict = questions.ToDictionary(q => q.Id, q => q);

            // Add answers
            foreach (var answer in answers.Take(20)) // Limit to 20 fields (Discord limit is 25)
            {
                if (questionDict.TryGetValue(answer.QuestionId, out var question))
                {
                    var answerValue = answer.AnswerValues != null && answer.AnswerValues.Length > 0
                        ? string.Join(", ", answer.AnswerValues)
                        : answer.AnswerText ?? "*(No answer)*";

                    // Truncate long answers
                    if (answerValue.Length > 1024)
                        answerValue = answerValue[..1021] + "...";

                    embed.AddField(
                        $"❓ {question.QuestionText}",
                        $"└─ {answerValue}"
                    );
                }
            }

            if (answers.Count > 20)
            {
                embed.AddField("⚠️ Note",
                    $"Showing 20 of {answers.Count} answers. View full response in the dashboard.");
            }

            var msg = await channel.SendMessageAsync(embed: embed.Build());

            // Update response with message ID
            await using var db = await dbFactory.CreateConnectionAsync();
            await db.FormResponses
                .Where(r => r.Id == response.Id)
                .Set(r => r.MessageId, msg.Id)
                .UpdateAsync();

            logger.LogInformation("Logged form submission {ResponseId} to channel {ChannelId}",
                response.Id, channel.Id);

            return msg.Id;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to log form submission {ResponseId} to Discord", response.Id);
            return null;
        }
    }

    #endregion

    #region Form Management

    /// <summary>
    ///     Creates a new form for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID where the form will be created.</param>
    /// <param name="form">The form to create.</param>
    /// <returns>The created form with assigned ID.</returns>
    public async Task<Form> CreateFormAsync(ulong guildId, Form form)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        form.GuildId = guildId;
        form.CreatedAt = DateTime.UtcNow;
        form.UpdatedAt = DateTime.UtcNow;
        form.IsActive = true;

        var id = await db.InsertWithInt32IdentityAsync(form);
        form.Id = id;

        logger.LogInformation("Created form {FormId} '{FormName}' for guild {GuildId}", form.Id, form.Name, guildId);
        return form;
    }

    /// <summary>
    ///     Gets a form by its ID.
    /// </summary>
    /// <param name="formId">The form ID.</param>
    /// <param name="includeDrafts">Whether to include draft forms.</param>
    /// <returns>The form, or null if not found.</returns>
    public async Task<Form?> GetFormAsync(int formId, bool includeDrafts = true)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var query = db.Forms.Where(f => f.Id == formId);

        if (!includeDrafts)
            query = query.Where(f => !f.IsDraft);

        return await query.FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Gets all forms for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="activeOnly">If true, only return active forms.</param>
    /// <returns>List of forms.</returns>
    public async Task<List<Form>> GetGuildFormsAsync(ulong guildId, bool activeOnly = false)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var query = db.Forms.Where(f => f.GuildId == guildId);

        if (activeOnly)
            query = query.Where(f => f.IsActive);

        return await query
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    ///     Updates an existing form.
    /// </summary>
    /// <param name="form">The form to update.</param>
    /// <returns>True if successful.</returns>
    public async Task<bool> UpdateFormAsync(Form form)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        form.UpdatedAt = DateTime.UtcNow;
        var updated = await db.UpdateAsync(form);

        logger.LogInformation("Updated form {FormId} '{FormName}'", form.Id, form.Name);
        return updated > 0;
    }

    /// <summary>
    ///     Deletes a form and all associated data.
    /// </summary>
    /// <param name="formId">The form ID to delete.</param>
    /// <returns>True if successful.</returns>
    public async Task<bool> DeleteFormAsync(int formId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // linq2db will handle cascade deletes based on foreign keys
        var deleted = await db.Forms
            .Where(f => f.Id == formId)
            .DeleteAsync();

        logger.LogInformation("Deleted form {FormId} and all associated data", formId);
        return deleted > 0;
    }

    /// <summary>
    ///     Toggles a form's active status.
    /// </summary>
    /// <param name="formId">The form ID.</param>
    /// <param name="isActive">The new active status.</param>
    /// <returns>True if successful.</returns>
    public async Task<bool> SetFormActiveStatusAsync(int formId, bool isActive)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var updated = await db.Forms
            .Where(f => f.Id == formId)
            .Set(f => f.IsActive, isActive)
            .Set(f => f.UpdatedAt, DateTime.UtcNow)
            .UpdateAsync();

        logger.LogInformation("Set form {FormId} active status to {IsActive}", formId, isActive);
        return updated > 0;
    }

    /// <summary>
    ///     Publishes a draft form (makes it publicly accessible).
    /// </summary>
    /// <param name="formId">The form ID.</param>
    /// <returns>True if successful.</returns>
    public async Task<bool> PublishFormAsync(int formId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var updated = await db.Forms
            .Where(f => f.Id == formId)
            .Set(f => f.IsDraft, false)
            .Set(f => f.UpdatedAt, DateTime.UtcNow)
            .UpdateAsync();

        logger.LogInformation("Published form {FormId}", formId);
        return updated > 0;
    }

    /// <summary>
    ///     Duplicates a form with all its questions and options.
    /// </summary>
    /// <param name="formId">The form ID to duplicate.</param>
    /// <param name="userId">The user ID creating the duplicate.</param>
    /// <returns>The duplicated form.</returns>
    public async Task<Form> DuplicateFormAsync(int formId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get original form
        var originalForm = await GetFormAsync(formId);
        if (originalForm == null)
            throw new InvalidOperationException("Form not found");

        // Get original questions and options
        var originalQuestions = await GetFormQuestionsAsync(formId);

        // Create duplicate form
        var duplicateForm = new Form
        {
            GuildId = originalForm.GuildId,
            Name = $"{originalForm.Name} (Copy)",
            Description = originalForm.Description,
            SubmitChannelId = originalForm.SubmitChannelId,
            AllowMultipleSubmissions = originalForm.AllowMultipleSubmissions,
            MaxResponses = originalForm.MaxResponses,
            RequireCaptcha = originalForm.RequireCaptcha,
            IsActive = false, // Start as inactive
            IsDraft = true, // Start as draft
            AllowAnonymous = originalForm.AllowAnonymous,
            ExpiresAt = originalForm.ExpiresAt,
            RequiredRoleId = originalForm.RequiredRoleId,
            SuccessMessage = originalForm.SuccessMessage,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var newFormId = await db.InsertWithInt32IdentityAsync(duplicateForm);
        duplicateForm.Id = newFormId;

        // Duplicate questions
        var questionIdMapping = new Dictionary<int, int>();

        foreach (var originalQuestion in originalQuestions)
        {
            var duplicateQuestion = new FormQuestion
            {
                FormId = newFormId,
                QuestionText = originalQuestion.QuestionText,
                QuestionType = originalQuestion.QuestionType,
                IsRequired = originalQuestion.IsRequired,
                DisplayOrder = originalQuestion.DisplayOrder,
                Placeholder = originalQuestion.Placeholder,
                MinValue = originalQuestion.MinValue,
                MaxValue = originalQuestion.MaxValue,
                MinLength = originalQuestion.MinLength,
                MaxLength = originalQuestion.MaxLength,
                // Note: Conditional logic will be updated after all questions are created
                ConditionalParentQuestionId = null,
                ConditionalOperator = originalQuestion.ConditionalOperator,
                ConditionalExpectedValue = originalQuestion.ConditionalExpectedValue,
                CreatedAt = DateTime.UtcNow
            };

            var newQuestionId = await db.InsertWithInt32IdentityAsync(duplicateQuestion);
            questionIdMapping[originalQuestion.Id] = newQuestionId;

            // Duplicate options
            var originalOptions = await GetQuestionOptionsAsync(originalQuestion.Id);
            foreach (var originalOption in originalOptions)
            {
                var duplicateOption = new FormQuestionOption
                {
                    QuestionId = newQuestionId,
                    OptionText = originalOption.OptionText,
                    OptionValue = originalOption.OptionValue,
                    DisplayOrder = originalOption.DisplayOrder
                };

                await db.InsertAsync(duplicateOption);
            }
        }

        // Update conditional logic references
        foreach (var originalQuestion in originalQuestions)
        {
            if (originalQuestion.ConditionalParentQuestionId.HasValue &&
                questionIdMapping.TryGetValue(originalQuestion.ConditionalParentQuestionId.Value, out var newParentId))
            {
                var newQuestionId = questionIdMapping[originalQuestion.Id];
                await db.FormQuestions
                    .Where(q => q.Id == newQuestionId)
                    .Set(q => q.ConditionalParentQuestionId, newParentId)
                    .UpdateAsync();
            }
        }

        logger.LogInformation("Duplicated form {OriginalFormId} to new form {NewFormId}", formId, newFormId);
        return duplicateForm;
    }

    #endregion

    #region Question Management

    /// <summary>
    ///     Adds a question to a form.
    /// </summary>
    /// <param name="question">The question to add.</param>
    /// <returns>The created question with assigned ID.</returns>
    public async Task<FormQuestion> AddQuestionAsync(FormQuestion question)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        question.CreatedAt = DateTime.UtcNow;
        var id = await db.InsertWithInt32IdentityAsync(question);
        question.Id = id;

        logger.LogInformation("Added question {QuestionId} to form {FormId}", question.Id, question.FormId);
        return question;
    }

    /// <summary>
    ///     Updates an existing question.
    /// </summary>
    /// <param name="question">The question to update.</param>
    /// <returns>True if successful.</returns>
    public async Task<bool> UpdateQuestionAsync(FormQuestion question)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var updated = await db.UpdateAsync(question);

        logger.LogInformation("Updated question {QuestionId}", question.Id);
        return updated > 0;
    }

    /// <summary>
    ///     Deletes a question from a form.
    /// </summary>
    /// <param name="questionId">The question ID to delete.</param>
    /// <returns>True if successful.</returns>
    public async Task<bool> DeleteQuestionAsync(int questionId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var deleted = await db.FormQuestions
            .Where(q => q.Id == questionId)
            .DeleteAsync();

        logger.LogInformation("Deleted question {QuestionId}", questionId);
        return deleted > 0;
    }

    /// <summary>
    ///     Gets all questions for a form, ordered by display order.
    /// </summary>
    /// <param name="formId">The form ID.</param>
    /// <returns>List of questions.</returns>
    public async Task<List<FormQuestion>> GetFormQuestionsAsync(int formId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.FormQuestions
            .Where(q => q.FormId == formId)
            .OrderBy(q => q.DisplayOrder)
            .ToListAsync();
    }

    /// <summary>
    ///     Gets options for a specific question.
    /// </summary>
    /// <param name="questionId">The question ID.</param>
    /// <returns>List of options.</returns>
    public async Task<List<FormQuestionOption>> GetQuestionOptionsAsync(int questionId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.FormQuestionOptions
            .Where(o => o.QuestionId == questionId)
            .OrderBy(o => o.DisplayOrder)
            .ToListAsync();
    }

    /// <summary>
    ///     Adds an option to a question.
    /// </summary>
    /// <param name="option">The option to add.</param>
    /// <returns>The created option with assigned ID.</returns>
    public async Task<FormQuestionOption> AddQuestionOptionAsync(FormQuestionOption option)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var id = await db.InsertWithInt32IdentityAsync(option);
        option.Id = id;

        return option;
    }

    /// <summary>
    ///     Deletes all options for a question.
    /// </summary>
    /// <param name="questionId">The question ID.</param>
    /// <returns>Number of options deleted.</returns>
    public async Task<int> DeleteQuestionOptionsAsync(int questionId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.FormQuestionOptions
            .Where(o => o.QuestionId == questionId)
            .DeleteAsync();
    }

    #endregion

    #region Response Management

    /// <summary>
    ///     Checks if a user can submit a response to a form.
    /// </summary>
    /// <param name="formId">The form ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>True if the user can submit.</returns>
    public async Task<(bool CanSubmit, string? Reason)> CanUserSubmitAsync(int formId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var form = await GetFormAsync(formId);
        if (form == null)
            return (false, "Form not found");

        if (!form.IsActive)
            return (false, "This form is no longer accepting responses");

        // Check expiration
        if (form.ExpiresAt.HasValue && DateTime.UtcNow > form.ExpiresAt.Value)
            return (false, "This form has expired and is no longer accepting responses");

        // Check required role
        if (form.RequiredRoleId.HasValue)
        {
            var guild = client.GetGuild(form.GuildId);
            if (guild?.GetUser(userId) is not IGuildUser guildUser)
                return (false, "You must be a member of this server to submit this form");

            if (!guildUser.RoleIds.Contains(form.RequiredRoleId.Value))
            {
                var role = guild?.GetRole(form.RequiredRoleId.Value);
                var roleName = role?.Name ?? "the required role";
                return (false, $"You must have the {roleName} role to submit this form");
            }
        }

        // Check max responses
        if (form.MaxResponses.HasValue)
        {
            var responseCount = await db.FormResponses
                .Where(r => r.FormId == formId)
                .CountAsync();

            if (responseCount >= form.MaxResponses.Value)
                return (false, "This form has reached its maximum number of responses");
        }

        // Check multiple submissions
        if (!form.AllowMultipleSubmissions)
        {
            var hasSubmitted = await db.FormResponses
                .Where(r => r.FormId == formId && r.UserId == userId)
                .AnyAsync();

            if (hasSubmitted)
                return (false, "You have already submitted a response to this form");
        }

        return (true, null);
    }

    /// <summary>
    ///     Submits a response to a form.
    /// </summary>
    /// <param name="formId">The form ID.</param>
    /// <param name="userId">The user ID (used for verification, may not be stored if anonymous).</param>
    /// <param name="username">The username (may not be stored if anonymous).</param>
    /// <param name="answers">Dictionary of questionId -> answer.</param>
    /// <param name="ipAddress">Optional IP address for spam prevention.</param>
    /// <returns>The created response.</returns>
    public async Task<FormResponse> SubmitResponseAsync(
        int formId,
        ulong userId,
        string username,
        Dictionary<int, object> answers,
        string? ipAddress = null)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get form to check if anonymous submissions are allowed
        var form = await GetFormAsync(formId);
        if (form == null)
            throw new InvalidOperationException("Form not found");

        // Create response
        var response = new FormResponse
        {
            FormId = formId,
            // Only store user data if NOT anonymous
            UserId = form.AllowAnonymous ? null : userId,
            Username = form.AllowAnonymous ? null : username,
            SubmittedAt = DateTime.UtcNow,
            // Never store IP for anonymous submissions
            IpAddress = form.AllowAnonymous ? null : ipAddress
        };

        var responseId = await db.InsertWithInt32IdentityAsync(response);
        response.Id = responseId;

        // Create answers
        foreach (var (questionId, answer) in answers)
        {
            var formAnswer = new FormAnswer
            {
                ResponseId = responseId, QuestionId = questionId, CreatedAt = DateTime.UtcNow
            };

            // Handle different answer types
            if (answer is string[] arrayAnswer)
            {
                formAnswer.AnswerValues = arrayAnswer;
            }
            else
            {
                formAnswer.AnswerText = answer?.ToString();
            }

            await db.InsertAsync(formAnswer);
        }

        logger.LogInformation("User {UserId} submitted response {ResponseId} to form {FormId}",
            userId, responseId, formId);

        return response;
    }

    /// <summary>
    ///     Gets all responses for a form with pagination.
    /// </summary>
    /// <param name="formId">The form ID.</param>
    /// <param name="page">Page number (1-indexed).</param>
    /// <param name="pageSize">Number of responses per page.</param>
    /// <returns>List of responses.</returns>
    public async Task<List<FormResponse>> GetFormResponsesAsync(int formId, int page = 1, int pageSize = 50)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await db.FormResponses
            .Where(r => r.FormId == formId)
            .OrderByDescending(r => r.SubmittedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    /// <summary>
    ///     Gets a specific response with all its answers.
    /// </summary>
    /// <param name="responseId">The response ID.</param>
    /// <returns>The response, or null if not found.</returns>
    public async Task<FormResponse?> GetResponseDetailsAsync(int responseId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.FormResponses
            .Where(r => r.Id == responseId)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Gets all answers for a response.
    /// </summary>
    /// <param name="responseId">The response ID.</param>
    /// <returns>List of answers.</returns>
    public async Task<List<FormAnswer>> GetResponseAnswersAsync(int responseId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.FormAnswers
            .Where(a => a.ResponseId == responseId)
            .ToListAsync();
    }

    /// <summary>
    ///     Gets the total number of responses for a form.
    /// </summary>
    /// <param name="formId">The form ID.</param>
    /// <returns>The response count.</returns>
    public async Task<int> GetResponseCountAsync(int formId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.FormResponses
            .Where(r => r.FormId == formId)
            .CountAsync();
    }

    /// <summary>
    ///     Deletes a specific response.
    /// </summary>
    /// <param name="responseId">The response ID to delete.</param>
    /// <returns>True if successful.</returns>
    public async Task<bool> DeleteResponseAsync(int responseId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var deleted = await db.FormResponses
            .Where(r => r.Id == responseId)
            .DeleteAsync();

        logger.LogInformation("Deleted response {ResponseId}", responseId);
        return deleted > 0;
    }

    #endregion

    #region Conditional Logic

    /// <summary>
    ///     Evaluates whether a question should be shown based on conditional logic.
    /// </summary>
    /// <param name="question">The question to evaluate.</param>
    /// <param name="currentAnswers">Dictionary of questionId -> current answer values.</param>
    /// <returns>True if the question should be shown.</returns>
    public bool ShouldShowQuestion(FormQuestion question, Dictionary<int, object> currentAnswers)
    {
        // If no conditional logic, always show
        if (!question.ConditionalParentQuestionId.HasValue)
            return true;

        // Check if parent question has been answered
        if (!currentAnswers.TryGetValue(question.ConditionalParentQuestionId.Value, out var parentAnswer))
            return false;

        // Evaluate condition
        return EvaluateCondition(
            parentAnswer,
            question.ConditionalOperator ?? "equals",
            question.ConditionalExpectedValue ?? string.Empty
        );
    }

    /// <summary>
    ///     Evaluates a conditional expression.
    /// </summary>
    /// <param name="actualValue">The actual value from the parent question.</param>
    /// <param name="operator">The comparison operator.</param>
    /// <param name="expectedValue">The expected value to compare against.</param>
    /// <returns>True if the condition is met.</returns>
    private bool EvaluateCondition(object actualValue, string @operator, string expectedValue)
    {
        var actualStr = actualValue?.ToString() ?? string.Empty;

        return @operator.ToLower() switch
        {
            "equals" => actualStr.Equals(expectedValue, StringComparison.OrdinalIgnoreCase),
            "not_equals" => !actualStr.Equals(expectedValue, StringComparison.OrdinalIgnoreCase),
            "contains" => actualStr.Contains(expectedValue, StringComparison.OrdinalIgnoreCase),
            "greater_than" => double.TryParse(actualStr, out var a1) &&
                              double.TryParse(expectedValue, out var e1) && a1 > e1,
            "less_than" => double.TryParse(actualStr, out var a2) &&
                           double.TryParse(expectedValue, out var e2) && a2 < e2,
            _ => true
        };
    }

    #endregion

    #region Share Links

    /// <summary>
    ///     Generates or retrieves a share link for a form.
    /// </summary>
    /// <param name="formId">The form ID.</param>
    /// <param name="instanceIdentifier">The instance identifier (port or name).</param>
    /// <returns>The share code.</returns>
    public async Task<string> GenerateShareLinkAsync(int formId, string instanceIdentifier)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Check if an active share link already exists
        var existing = await db.FormShareLinks
            .Where(l => l.FormId == formId && l.InstanceIdentifier == instanceIdentifier && l.IsActive)
            .FirstOrDefaultAsync();

        if (existing != null)
            return existing.ShareCode;

        // Generate new share code (8 characters, alphanumeric, URL-safe)
        var shareCode = GenerateRandomCode(12);

        // Ensure uniqueness
        while (await db.FormShareLinks.Where(l => l.ShareCode == shareCode).AnyAsync())
        {
            shareCode = GenerateRandomCode(12);
        }

        var shareLink = new FormShareLink
        {
            ShareCode = shareCode,
            FormId = formId,
            InstanceIdentifier = instanceIdentifier,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        await db.InsertAsync(shareLink);

        logger.LogInformation("Generated share link {ShareCode} for form {FormId}", shareCode, formId);
        return shareCode;
    }

    /// <summary>
    ///     Resolves a share code to get form ID and instance information.
    /// </summary>
    /// <param name="shareCode">The share code.</param>
    /// <returns>Tuple of (FormId, InstanceIdentifier), or null if not found.</returns>
    public async Task<(int FormId, string InstanceIdentifier)?> ResolveShareLinkAsync(string shareCode)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var shareLink = await db.FormShareLinks
            .Where(l => l.ShareCode == shareCode && l.IsActive)
            .FirstOrDefaultAsync();

        if (shareLink == null)
            return null;

        // Check if expired
        if (shareLink.ExpiresAt.HasValue && DateTime.UtcNow > shareLink.ExpiresAt.Value)
        {
            // Mark as inactive
            await db.FormShareLinks
                .Where(l => l.Id == shareLink.Id)
                .Set(l => l.IsActive, false)
                .UpdateAsync();

            return null;
        }

        return (shareLink.FormId, shareLink.InstanceIdentifier);
    }

    /// <summary>
    ///     Generates a random alphanumeric code.
    /// </summary>
    /// <param name="length">The length of the code.</param>
    /// <returns>Random code string.</returns>
    private static string GenerateRandomCode(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)])
            .ToArray());
    }

    #endregion

    #region Workflow Management

    /// <summary>
    ///     Creates a workflow entry for a newly submitted response.
    /// </summary>
    /// <param name="responseId">The response ID.</param>
    /// <returns>The created workflow.</returns>
    public async Task<FormResponseWorkflow> CreateWorkflowForResponseAsync(int responseId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Check if workflow already exists for this response
        var existingWorkflow = await db.FormResponseWorkflows
            .Where(w => w.ResponseId == responseId)
            .FirstOrDefaultAsync();

        if (existingWorkflow != null)
        {
            logger.LogInformation("Workflow already exists for response {ResponseId}, returning existing", responseId);
            return existingWorkflow;
        }

        // Generate unique status check token (base62 for URL-safe)
        var statusCheckToken = GenerateRandomCode(32);

        // Ensure uniqueness
        while (await db.FormResponseWorkflows.Where(w => w.StatusCheckToken == statusCheckToken).AnyAsync())
        {
            statusCheckToken = GenerateRandomCode(32);
        }

        var workflow = new FormResponseWorkflow
        {
            ResponseId = responseId,
            Status = (int)ResponseStatus.Pending,
            ActionTaken = (int)WorkflowAction.None,
            StatusCheckToken = statusCheckToken,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var id = await db.InsertWithInt32IdentityAsync(workflow);
        workflow.Id = id;

        logger.LogInformation("Created workflow {WorkflowId} for response {ResponseId} with token {Token}",
            workflow.Id, responseId, statusCheckToken);
        return workflow;
    }

    /// <summary>
    ///     Gets a workflow by response ID.
    /// </summary>
    /// <param name="responseId">The response ID.</param>
    /// <returns>The workflow, or null if not found.</returns>
    public async Task<FormResponseWorkflow?> GetWorkflowByResponseIdAsync(int responseId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.FormResponseWorkflows
            .Where(w => w.ResponseId == responseId)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Gets a workflow by status check token.
    /// </summary>
    /// <param name="token">The status check token.</param>
    /// <returns>The workflow, or null if not found.</returns>
    public async Task<FormResponseWorkflow?> GetWorkflowByTokenAsync(string token)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.FormResponseWorkflows
            .Where(w => w.StatusCheckToken == token)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Gets all pending responses for a form.
    /// </summary>
    /// <param name="formId">The form ID.</param>
    /// <param name="filterStatus">Optional status filter.</param>
    /// <returns>List of responses with pending/filtered status.</returns>
    public async Task<List<FormResponse>> GetPendingResponsesAsync(int formId, ResponseStatus? filterStatus = null)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var query = from response in db.FormResponses
            join workflow in db.FormResponseWorkflows on response.Id equals workflow.ResponseId
            where response.FormId == formId
            select new
            {
                Response = response, Workflow = workflow
            };

        if (filterStatus.HasValue)
        {
            query = query.Where(x => x.Workflow.Status == (int)filterStatus.Value);
        }
        else
        {
            // Default to pending and under review
            query = query.Where(x => x.Workflow.Status == (int)ResponseStatus.Pending ||
                                     x.Workflow.Status == (int)ResponseStatus.UnderReview);
        }

        var results = await query
            .OrderBy(x => x.Response.SubmittedAt)
            .Select(x => x.Response)
            .ToListAsync();

        return results;
    }

    /// <summary>
    ///     Approves a response and takes appropriate action based on form type.
    /// </summary>
    /// <param name="responseId">The response ID to approve.</param>
    /// <param name="reviewerId">The Discord user ID of the reviewer.</param>
    /// <param name="notes">Optional review notes.</param>
    /// <returns>Tuple of success and invite code (if applicable).</returns>
    public async Task<(bool Success, string? InviteCode)> ApproveResponseAsync(int responseId, ulong reviewerId,
        string? notes)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        // Get the response and form
        var response = await db.FormResponses
            .Where(r => r.Id == responseId)
            .FirstOrDefaultAsync();

        if (response == null)
            return (false, null);

        var form = await GetFormAsync(response.FormId);
        if (form == null)
            return (false, null);

        var workflow = await db.FormResponseWorkflows
            .Where(w => w.ResponseId == responseId)
            .FirstOrDefaultAsync();

        if (workflow == null)
            return (false, null);

        // Update workflow status
        workflow.Status = (int)ResponseStatus.Approved;
        workflow.ReviewedBy = reviewerId;
        workflow.ReviewedAt = DateTime.UtcNow;
        workflow.ReviewNotes = notes;
        workflow.UpdatedAt = DateTime.UtcNow;

        // Take action based on form type
        string? inviteCode = null;
        var actionTaken = WorkflowAction.None;

        switch ((FormType)form.FormType)
        {
            case FormType.BanAppeal:
                if (response.UserId.HasValue && await UnbanUserAsync(form, response.UserId.Value))
                {
                    actionTaken = WorkflowAction.Unbanned;
                }

                break;

            case FormType.JoinApplication:
                if (response.UserId.HasValue)
                {
                    var inviteResult = await GenerateInviteAndPreassignRolesAsync(form, response.UserId.Value);
                    if (inviteResult != null)
                    {
                        inviteCode = inviteResult.Value.InviteCode;
                        workflow.InviteCode = inviteCode;
                        workflow.InviteExpiresAt = inviteResult.Value.ExpiresAt;
                        actionTaken = WorkflowAction.InviteSent | WorkflowAction.RolesPreassigned;
                    }
                }

                break;
        }

        workflow.ActionTaken = (int)actionTaken;
        await db.UpdateAsync(workflow);

        logger.LogInformation("Approved response {ResponseId} by reviewer {ReviewerId}. Action: {Action}",
            responseId, reviewerId, actionTaken);

        return (true, inviteCode);
    }

    /// <summary>
    ///     Rejects a response.
    /// </summary>
    /// <param name="responseId">The response ID to reject.</param>
    /// <param name="reviewerId">The Discord user ID of the reviewer.</param>
    /// <param name="notes">Optional review notes.</param>
    /// <returns>True if successful.</returns>
    public async Task<bool> RejectResponseAsync(int responseId, ulong reviewerId, string? notes)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var workflow = await db.FormResponseWorkflows
            .Where(w => w.ResponseId == responseId)
            .FirstOrDefaultAsync();

        if (workflow == null)
            return false;

        workflow.Status = (int)ResponseStatus.Rejected;
        workflow.ReviewedBy = reviewerId;
        workflow.ReviewedAt = DateTime.UtcNow;
        workflow.ReviewNotes = notes;
        workflow.UpdatedAt = DateTime.UtcNow;

        await db.UpdateAsync(workflow);

        logger.LogInformation("Rejected response {ResponseId} by reviewer {ReviewerId}",
            responseId, reviewerId);

        return true;
    }

    /// <summary>
    ///     Checks if a user is eligible to submit a specific form type.
    /// </summary>
    /// <param name="formId">The form ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>Tuple of eligibility and reason if not eligible.</returns>
    public async Task<(bool IsEligible, string? Reason)> CheckFormEligibilityAsync(int formId, ulong userId)
    {
        var form = await GetFormAsync(formId);
        if (form == null)
            return (false, "Form not found");

        var guild = client.GetGuild(form.GuildId);
        if (guild == null)
            return (false, "Guild not found");

        switch ((FormType)form.FormType)
        {
            case FormType.BanAppeal:
                // Check if user is actually banned
                try
                {
                    var ban = await guild.GetBanAsync(userId);
                    if (ban == null)
                        return (false, "You are not banned from this server");
                }
                catch
                {
                    return (false, "You are not banned from this server");
                }

                break;

            case FormType.JoinApplication:
                // Check if user is already in the guild
                var member = guild.GetUser(userId);
                if (member != null)
                    return (false, "You are already a member of this server");
                break;

            case FormType.Regular:
                // Regular forms require guild membership unless explicitly allowed
                if (!form.AllowExternalUsers)
                {
                    var regularMember = guild.GetUser(userId);
                    if (regularMember == null)
                        return (false, "You must be a member of this server to submit this form");
                }

                break;
        }

        return (true, null);
    }

    /// <summary>
    ///     Unbans a user from a guild (for approved ban appeals).
    /// </summary>
    /// <param name="form">The form.</param>
    /// <param name="userId">The user ID to unban.</param>
    /// <returns>True if successful.</returns>
    private async Task<bool> UnbanUserAsync(Form form, ulong userId)
    {
        var guild = client.GetGuild(form.GuildId);
        if (guild == null)
            return false;

        try
        {
            await guild.RemoveBanAsync(userId);
            logger.LogInformation("Unbanned user {UserId} from guild {GuildId} via form {FormId}",
                userId, form.GuildId, form.Id);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to unban user {UserId} from guild {GuildId}",
                userId, form.GuildId);
            return false;
        }
    }

    /// <summary>
    ///     Generates an invite link and pre-assigns roles for approved join applications.
    /// </summary>
    /// <param name="form">The form.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>Tuple of invite code and expiration date, or null if failed.</returns>
    private async Task<(string InviteCode, DateTime ExpiresAt)?> GenerateInviteAndPreassignRolesAsync(Form form,
        ulong userId)
    {
        var guild = client.GetGuild(form.GuildId);
        if (guild == null)
            return null;

        try
        {
            // Find a suitable channel for invite creation
            var channel = guild.SystemChannel
                          ?? guild.TextChannels.FirstOrDefault(c =>
                              c.GetPermissionOverwrite(guild.EveryoneRole)?.ViewChannel != PermValue.Deny)
                          ?? guild.TextChannels.FirstOrDefault();

            if (channel == null)
            {
                logger.LogWarning("No suitable channel found for invite creation in guild {GuildId}", form.GuildId);
                return null;
            }

            // Create invite
            var maxAge = form.InviteMaxAge ?? 86400; // 24 hours default
            var maxUses = form.InviteMaxUses ?? 1; // 1 use default

            var invite = await channel.CreateInviteAsync(
                maxAge,
                maxUses,
                isUnique: true
            );

            // Pre-assign roles using RoleStates if configured
            if (!string.IsNullOrWhiteSpace(form.AutoApproveRoleIds))
            {
                try
                {
                    var roleIds = form.AutoApproveRoleIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => ulong.Parse(x.Trim()))
                        .ToList();

                    if (roleIds.Any())
                    {
                        // Get user object for RoleStates
                        var user = await client.GetUserAsync(userId, CacheMode.AllowDownload, RequestOptions.Default);
                        if (user != null)
                        {
                            // Use RoleStates service to pre-assign roles
                            // Note: This requires injecting RoleStatesService
                            // For now, we'll create the role state directly
                            await using var db = await dbFactory.CreateConnectionAsync();

                            var existingState = await db.UserRoleStates
                                .FirstOrDefaultAsync(x => x.GuildId == form.GuildId && x.UserId == userId);

                            if (existingState == null)
                            {
                                var newRoleState = new UserRoleState
                                {
                                    GuildId = form.GuildId,
                                    UserId = userId,
                                    UserName = user.ToString(),
                                    SavedRoles = string.Join(",", roleIds)
                                };
                                await db.InsertAsync(newRoleState);
                            }
                            else
                            {
                                // Merge with existing roles
                                var existingRoles = existingState.SavedRoles
                                    ?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                    .Select(x => ulong.Parse(x.Trim()))
                                    .ToList() ?? new List<ulong>();

                                var mergedRoles = existingRoles.Union(roleIds).Distinct();
                                existingState.SavedRoles = string.Join(",", mergedRoles);
                                await db.UpdateAsync(existingState);
                            }

                            logger.LogInformation("Pre-assigned {RoleCount} roles for user {UserId} in guild {GuildId}",
                                roleIds.Count, userId, form.GuildId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to pre-assign roles for user {UserId} in guild {GuildId}",
                        userId, form.GuildId);
                    // Continue anyway - invite is still valid
                }
            }

            var expiresAt = DateTime.UtcNow.AddSeconds(maxAge);

            logger.LogInformation("Generated invite {InviteCode} for user {UserId} to guild {GuildId}",
                invite.Code, userId, form.GuildId);

            return (invite.Code, expiresAt);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate invite for user {UserId} in guild {GuildId}",
                userId, form.GuildId);
            return null;
        }
    }

    #endregion
}