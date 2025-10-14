using System.Net.Http;
using System.Text;
using System.Text.Json;
using DataModel;
using Mewdeko.Controllers.Common.Forms;
using Mewdeko.Database.Enums;
using Mewdeko.Modules.Forms.Common;
using Mewdeko.Modules.Forms.Services;
using Mewdeko.Services.Impl;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     API Controller for managing custom forms with conditional logic.
/// </summary>
[ApiController]
[Route("botapi/[controller]")]
[Authorize("ApiKeyPolicy")]
public class FormsController : Controller
{
    private readonly DiscordShardedClient client;
    private readonly BotCredentials creds;
    private readonly HttpClient httpClient;
    private readonly ILogger<FormsController> logger;
    private readonly FormsService service;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FormsController" /> class.
    /// </summary>
    /// <param name="service">The forms service instance.</param>
    /// <param name="client">The Discord client instance.</param>
    /// <param name="creds">The bot credentials instance.</param>
    /// <param name="httpClient">The HTTP client instance for Turnstile verification.</param>
    /// <param name="logger">Logger for this class.</param>
    public FormsController(
        FormsService service,
        DiscordShardedClient client,
        BotCredentials creds,
        HttpClient httpClient,
        ILogger<FormsController> logger)
    {
        this.service = service;
        this.client = client;
        this.creds = creds;
        this.httpClient = httpClient;
        this.logger = logger;
    }

    #region Turnstile Verification

    /// <summary>
    ///     Verifies a Cloudflare Turnstile token.
    /// </summary>
    /// <param name="token">The Turnstile token from the client.</param>
    /// <returns>The verification response.</returns>
    private async Task<TurnstileVerificationResponse> VerifyTurnstileToken(string token)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            {
                "secret", creds.TurnstileKey
            },
            {
                "response", token
            }
        });

        try
        {
            var response =
                await httpClient.PostAsync("https://challenges.cloudflare.com/turnstile/v0/siteverify", content);
            response.EnsureSuccessStatusCode();
            var responseStream = await response.Content.ReadAsStreamAsync();
            var verificationData = await JsonSerializer.DeserializeAsync<TurnstileVerificationResponse>(responseStream);
            return verificationData ?? new TurnstileVerificationResponse
            {
                Success = false
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error verifying Turnstile token");
            return new TurnstileVerificationResponse
            {
                Success = false
            };
        }
    }

    #endregion

    #region Form Management

    /// <summary>
    ///     Gets all forms for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="activeOnly">If true, only return active forms.</param>
    /// <returns>List of forms.</returns>
    [HttpGet("guild/{guildId:ulong}")]
    public async Task<IActionResult> GetGuildForms(ulong guildId, [FromQuery] bool activeOnly = false)
    {
        try
        {
            var forms = await service.GetGuildFormsAsync(guildId, activeOnly);

            // Include response counts and pending counts
            var formsWithCounts = new List<object>();
            foreach (var form in forms)
            {
                var responseCount = await service.GetResponseCountAsync(form.Id);

                // Get pending response count for workflow forms and Regular forms with approval
                var pendingCount = 0;
                if (form.FormType != (int)FormType.Regular || form.RequireApproval)
                {
                    var pendingResponses = await service.GetPendingResponsesAsync(form.Id);
                    pendingCount = pendingResponses.Count;
                }

                formsWithCounts.Add(new
                {
                    form.Id,
                    form.GuildId,
                    form.Name,
                    form.Description,
                    form.SubmitChannelId,
                    form.AllowMultipleSubmissions,
                    form.MaxResponses,
                    form.RequireCaptcha,
                    form.IsActive,
                    form.IsDraft,
                    form.AllowAnonymous,
                    form.ExpiresAt,
                    form.RequiredRoleId,
                    form.SuccessMessage,
                    form.FormType,
                    form.AllowExternalUsers,
                    form.AutoApproveRoleIds,
                    form.InviteMaxUses,
                    form.InviteMaxAge,
                    form.NotificationWebhookUrl,
                    form.RequireApproval,
                    form.ApprovalActionType,
                    form.ApprovalRoleIds,
                    form.RejectionActionType,
                    form.RejectionRoleIds,
                    form.CreatedBy,
                    form.CreatedAt,
                    form.UpdatedAt,
                    ResponseCount = responseCount,
                    PendingCount = pendingCount
                });
            }

            return Ok(formsWithCounts);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get forms for guild {GuildId}", guildId);
            return StatusCode(500, new
            {
                message = "Failed to retrieve forms"
            });
        }
    }

    /// <summary>
    ///     Gets a specific form by ID.
    /// </summary>
    /// <param name="formId">The form ID.</param>
    /// <returns>The form details.</returns>
    [HttpGet("{formId:int}")]
    public async Task<IActionResult> GetForm(int formId)
    {
        try
        {
            var form = await service.GetFormAsync(formId);
            if (form == null)
                return NotFound(new
                {
                    message = "Form not found"
                });

            return Ok(form);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get form {FormId}", formId);
            return StatusCode(500, new
            {
                message = "Failed to retrieve form"
            });
        }
    }

    /// <summary>
    ///     Creates a new form for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="form">The form to create.</param>
    /// <returns>The created form.</returns>
    [HttpPost("guild/{guildId:ulong}")]
    public async Task<IActionResult> CreateForm(ulong guildId, [FromBody] Form form)
    {
        try
        {
            // Validate form
            var validationErrors = FormValidator.ValidateForm(form);
            if (validationErrors.Any())
            {
                return BadRequest(new
                {
                    message = string.Join("; ", validationErrors)
                });
            }

            var createdForm = await service.CreateFormAsync(guildId, form);
            return Ok(createdForm);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create form for guild {GuildId}", guildId);
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    /// <summary>
    ///     Updates an existing form.
    /// </summary>
    /// <param name="formId">The form ID to update.</param>
    /// <param name="form">The updated form data.</param>
    /// <returns>Success status.</returns>
    [HttpPut("{formId:int}")]
    public async Task<IActionResult> UpdateForm(int formId, [FromBody] Form form)
    {
        try
        {
            // Validate form
            var validationErrors = FormValidator.ValidateForm(form);
            if (validationErrors.Any())
            {
                return BadRequest(new
                {
                    message = string.Join("; ", validationErrors)
                });
            }

            form.Id = formId; // Ensure ID matches route
            var success = await service.UpdateFormAsync(form);

            if (!success)
                return NotFound(new
                {
                    message = "Form not found"
                });

            return Ok(new
            {
                message = "Form updated successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update form {FormId}", formId);
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    /// <summary>
    ///     Deletes a form and all associated data.
    /// </summary>
    /// <param name="formId">The form ID to delete.</param>
    /// <returns>Success status.</returns>
    [HttpDelete("{formId:int}")]
    public async Task<IActionResult> DeleteForm(int formId)
    {
        try
        {
            var success = await service.DeleteFormAsync(formId);

            if (!success)
                return NotFound(new
                {
                    message = "Form not found"
                });

            return Ok(new
            {
                message = "Form deleted successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete form {FormId}", formId);
            return StatusCode(500, new
            {
                message = "Failed to delete form"
            });
        }
    }

    /// <summary>
    ///     Toggles a form's active status.
    /// </summary>
    /// <param name="formId">The form ID.</param>
    /// <param name="isActive">The new active status.</param>
    /// <returns>Success status.</returns>
    [HttpPatch("{formId:int}/active")]
    public async Task<IActionResult> SetFormActiveStatus(int formId, [FromBody] bool isActive)
    {
        try
        {
            var success = await service.SetFormActiveStatusAsync(formId, isActive);

            if (!success)
                return NotFound(new
                {
                    message = "Form not found"
                });

            return Ok(new
            {
                message = $"Form {(isActive ? "activated" : "deactivated")} successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set form {FormId} active status", formId);
            return StatusCode(500, new
            {
                message = "Failed to update form status"
            });
        }
    }

    /// <summary>
    ///     Duplicates a form with all its questions and options.
    /// </summary>
    /// <param name="formId">The form ID to duplicate.</param>
    /// <param name="userId">The user ID creating the duplicate.</param>
    /// <returns>The duplicated form.</returns>
    [HttpPost("{formId:int}/duplicate")]
    public async Task<IActionResult> DuplicateForm(int formId, [FromBody] ulong userId)
    {
        try
        {
            var duplicatedForm = await service.DuplicateFormAsync(formId, userId);
            return Ok(duplicatedForm);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new
            {
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to duplicate form {FormId}", formId);
            return StatusCode(500, new
            {
                message = "Failed to duplicate form"
            });
        }
    }

    /// <summary>
    ///     Publishes a draft form.
    /// </summary>
    /// <param name="formId">The form ID to publish.</param>
    /// <returns>Success status.</returns>
    [HttpPost("{formId:int}/publish")]
    public async Task<IActionResult> PublishForm(int formId)
    {
        try
        {
            var success = await service.PublishFormAsync(formId);

            if (!success)
                return NotFound(new
                {
                    message = "Form not found"
                });

            return Ok(new
            {
                message = "Form published successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish form {FormId}", formId);
            return StatusCode(500, new
            {
                message = "Failed to publish form"
            });
        }
    }

    #endregion

    #region Question Management

    /// <summary>
    ///     Gets all questions for a form, including options.
    /// </summary>
    /// <param name="formId">The form ID.</param>
    /// <returns>List of questions with their options.</returns>
    [HttpGet("{formId:int}/questions")]
    public async Task<IActionResult> GetFormQuestions(int formId)
    {
        try
        {
            var questions = await service.GetFormQuestionsAsync(formId);

            // Include options and conditions for each question
            var questionsWithOptions = new List<object>();
            foreach (var question in questions)
            {
                var options = await service.GetQuestionOptionsAsync(question.Id);
                var conditions = await service.GetQuestionConditionsAsync(question.Id);
                questionsWithOptions.Add(new
                {
                    question.Id,
                    question.FormId,
                    question.QuestionText,
                    question.QuestionType,
                    question.IsRequired,
                    question.DisplayOrder,
                    question.Placeholder,
                    question.MinValue,
                    question.MaxValue,
                    question.MinLength,
                    question.MaxLength,
                    question.ConditionalParentQuestionId,
                    question.ConditionalOperator,
                    question.ConditionalExpectedValue,
                    question.ConditionalType,
                    question.ConditionalRoleIds,
                    question.ConditionalRoleLogic,
                    question.ConditionalDaysInServer,
                    question.ConditionalAccountAgeDays,
                    question.ConditionalRequiresBoost,
                    question.ConditionalRequiresNitro,
                    question.ConditionalPermissionFlags,
                    question.RequiredWhenParentQuestionId,
                    question.RequiredWhenOperator,
                    question.RequiredWhenValue,
                    question.EnableAnswerPiping,
                    question.CreatedAt,
                    Options = options,
                    Conditions = conditions
                });
            }

            return Ok(questionsWithOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get questions for form {FormId}", formId);
            return StatusCode(500, new
            {
                message = "Failed to retrieve questions"
            });
        }
    }

    /// <summary>
    ///     Adds a question to a form.
    /// </summary>
    /// <param name="formId">The form ID.</param>
    /// <param name="question">The question to add.</param>
    /// <returns>The created question.</returns>
    [HttpPost("{formId:int}/questions")]
    public async Task<IActionResult> AddQuestion(int formId, [FromBody] FormQuestion question)
    {
        try
        {
            // Validate question
            var validationErrors = FormValidator.ValidateQuestion(question);
            if (validationErrors.Any())
            {
                return BadRequest(new
                {
                    message = string.Join("; ", validationErrors)
                });
            }

            question.FormId = formId; // Ensure form ID matches route
            var createdQuestion = await service.AddQuestionAsync(question);
            return Ok(createdQuestion);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add question to form {FormId}", formId);
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    /// <summary>
    ///     Updates a question.
    /// </summary>
    /// <param name="questionId">The question ID to update.</param>
    /// <param name="question">The updated question data.</param>
    /// <returns>Success status.</returns>
    [HttpPut("questions/{questionId:int}")]
    public async Task<IActionResult> UpdateQuestion(int questionId, [FromBody] FormQuestion question)
    {
        try
        {
            // Validate question
            var validationErrors = FormValidator.ValidateQuestion(question);
            if (validationErrors.Any())
            {
                return BadRequest(new
                {
                    message = string.Join("; ", validationErrors)
                });
            }

            question.Id = questionId; // Ensure ID matches route
            var success = await service.UpdateQuestionAsync(question);

            if (!success)
                return NotFound(new
                {
                    message = "Question not found"
                });

            return Ok(new
            {
                message = "Question updated successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update question {QuestionId}", questionId);
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    /// <summary>
    ///     Deletes a question from a form.
    /// </summary>
    /// <param name="questionId">The question ID to delete.</param>
    /// <returns>Success status.</returns>
    [HttpDelete("questions/{questionId:int}")]
    public async Task<IActionResult> DeleteQuestion(int questionId)
    {
        try
        {
            var success = await service.DeleteQuestionAsync(questionId);

            if (!success)
                return NotFound(new
                {
                    message = "Question not found"
                });

            return Ok(new
            {
                message = "Question deleted successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete question {QuestionId}", questionId);
            return StatusCode(500, new
            {
                message = "Failed to delete question"
            });
        }
    }

    /// <summary>
    ///     Adds an option to a question.
    /// </summary>
    /// <param name="questionId">The question ID.</param>
    /// <param name="option">The option to add.</param>
    /// <returns>The created option.</returns>
    [HttpPost("questions/{questionId:int}/options")]
    public async Task<IActionResult> AddQuestionOption(int questionId, [FromBody] FormQuestionOption option)
    {
        try
        {
            // Validate option
            if (string.IsNullOrWhiteSpace(option.OptionText))
            {
                return BadRequest(new
                {
                    message = "Option text is required"
                });
            }

            if (option.OptionText.Length > 500)
            {
                return BadRequest(new
                {
                    message = "Option text cannot exceed 500 characters"
                });
            }

            option.QuestionId = questionId; // Ensure question ID matches route
            var createdOption = await service.AddQuestionOptionAsync(option);
            return Ok(createdOption);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add option to question {QuestionId}", questionId);
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    /// <summary>
    ///     Gets all conditions for a question (for multi-condition logic).
    /// </summary>
    /// <param name="questionId">The question ID.</param>
    /// <returns>List of conditions.</returns>
    [HttpGet("questions/{questionId:int}/conditions")]
    public async Task<IActionResult> GetQuestionConditions(int questionId)
    {
        try
        {
            var conditions = await service.GetQuestionConditionsAsync(questionId);
            return Ok(conditions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get conditions for question {QuestionId}", questionId);
            return StatusCode(500, new
            {
                message = "Failed to retrieve conditions"
            });
        }
    }

    /// <summary>
    ///     Adds a condition to a question.
    /// </summary>
    /// <param name="questionId">The question ID.</param>
    /// <param name="condition">The condition to add.</param>
    /// <returns>The created condition.</returns>
    [HttpPost("questions/{questionId:int}/conditions")]
    public async Task<IActionResult> AddQuestionCondition(int questionId, [FromBody] FormQuestionCondition condition)
    {
        try
        {
            condition.QuestionId = questionId; // Ensure question ID matches route
            var createdCondition = await service.AddQuestionConditionAsync(condition);
            return Ok(createdCondition);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add condition to question {QuestionId}", questionId);
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    /// <summary>
    ///     Deletes a condition.
    /// </summary>
    /// <param name="conditionId">The condition ID to delete.</param>
    /// <returns>Success status.</returns>
    [HttpDelete("conditions/{conditionId:int}")]
    public async Task<IActionResult> DeleteCondition(int conditionId)
    {
        try
        {
            var success = await service.DeleteConditionAsync(conditionId);

            if (!success)
                return NotFound(new
                {
                    message = "Condition not found"
                });

            return Ok(new
            {
                message = "Condition deleted successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete condition {ConditionId}", conditionId);
            return StatusCode(500, new
            {
                message = "Failed to delete condition"
            });
        }
    }

    #endregion

    #region Response Management

    /// <summary>
    ///     Submits a response to a form.
    /// </summary>
    /// <param name="formId">The form ID.</param>
    /// <param name="request">The submission request.</param>
    /// <returns>Success status with response ID.</returns>
    [HttpPost("{formId:int}/submit")]
    public async Task<IActionResult> SubmitForm(int formId, [FromBody] FormSubmissionRequest request)
    {
        try
        {
            // Get form to check captcha requirement
            var form = await service.GetFormAsync(formId);
            if (form == null)
                return NotFound(new
                {
                    message = "Form not found"
                });

            // Verify user is a member of the guild (unless external users are allowed)
            var guild = client.GetGuild(form.GuildId);
            if (guild == null)
                return BadRequest(new
                {
                    message = "Guild not found"
                });

            // Skip guild membership check for external form types
            if (!form.AllowExternalUsers && form.FormType == (int)FormType.Regular)
            {
                var guildUser = guild.GetUser(request.UserId);
                if (guildUser == null)
                    return BadRequest(new
                    {
                        message = "You must be a member of this server to submit this form"
                    });
            }

            // For ban appeals and join applications, check eligibility
            if (form.FormType != (int)FormType.Regular)
            {
                var (isEligible, eligibilityReason) = await service.CheckFormEligibilityAsync(formId, request.UserId);
                if (!isEligible)
                    return BadRequest(new
                    {
                        message = eligibilityReason
                    });
            }

            // Verify captcha if required
            if (form.RequireCaptcha)
            {
                if (string.IsNullOrWhiteSpace(request.TurnstileToken))
                    return BadRequest(new
                    {
                        message = "Captcha verification required"
                    });

                var verificationResponse = await VerifyTurnstileToken(request.TurnstileToken);
                if (!verificationResponse.Success)
                    return BadRequest(new
                    {
                        message = "Captcha verification failed"
                    });
            }

            // Check if user can submit
            var (canSubmit, submitReason) = await service.CanUserSubmitAsync(formId, request.UserId);
            if (!canSubmit)
                return BadRequest(new
                {
                    message = submitReason
                });

            // Submit response
            var response = await service.SubmitResponseAsync(
                formId,
                request.UserId,
                request.Username,
                request.Answers,
                request.IpAddress
            );

            // Create workflow entry for the response
            var workflow = await service.CreateWorkflowForResponseAsync(response.Id);

            // Log to Discord if configured
            var answers = await service.GetResponseAnswersAsync(response.Id);
            _ = service.LogSubmissionToChannelAsync(form, response, answers); // Fire and forget

            return Ok(new
            {
                message = "Form submitted successfully",
                responseId = response.Id,
                statusCheckToken = workflow.StatusCheckToken,
                statusCheckUrl = $"/forms/status/{workflow.StatusCheckToken}"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to submit form {FormId}", formId);
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    /// <summary>
    ///     Gets all responses for a form with pagination.
    /// </summary>
    /// <param name="formId">The form ID.</param>
    /// <param name="page">Page number (1-indexed).</param>
    /// <param name="pageSize">Number of responses per page.</param>
    /// <returns>List of responses.</returns>
    [HttpGet("{formId:int}/responses")]
    public async Task<IActionResult> GetFormResponses(
        int formId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var responses = await service.GetFormResponsesAsync(formId, page, pageSize);
            var totalCount = await service.GetResponseCountAsync(formId);

            return Ok(new
            {
                responses,
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get responses for form {FormId}", formId);
            return StatusCode(500, new
            {
                message = "Failed to retrieve responses"
            });
        }
    }

    /// <summary>
    ///     Gets a specific response with all its answers.
    /// </summary>
    /// <param name="responseId">The response ID.</param>
    /// <returns>The response details with answers.</returns>
    [HttpGet("responses/{responseId:int}")]
    public async Task<IActionResult> GetResponseDetails(int responseId)
    {
        try
        {
            var response = await service.GetResponseDetailsAsync(responseId);
            if (response == null)
                return NotFound(new
                {
                    message = "Response not found"
                });

            var answers = await service.GetResponseAnswersAsync(responseId);

            return Ok(new
            {
                response, answers
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get response {ResponseId}", responseId);
            return StatusCode(500, new
            {
                message = "Failed to retrieve response"
            });
        }
    }

    /// <summary>
    ///     Deletes a response.
    /// </summary>
    /// <param name="responseId">The response ID to delete.</param>
    /// <returns>Success status.</returns>
    [HttpDelete("responses/{responseId:int}")]
    public async Task<IActionResult> DeleteResponse(int responseId)
    {
        try
        {
            var success = await service.DeleteResponseAsync(responseId);

            if (!success)
                return NotFound(new
                {
                    message = "Response not found"
                });

            return Ok(new
            {
                message = "Response deleted successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete response {ResponseId}", responseId);
            return StatusCode(500, new
            {
                message = "Failed to delete response"
            });
        }
    }

    /// <summary>
    ///     Exports form responses as CSV.
    /// </summary>
    /// <param name="formId">The form ID.</param>
    /// <returns>CSV file download.</returns>
    [HttpGet("{formId:int}/responses/export")]
    public async Task<IActionResult> ExportResponses(int formId)
    {
        try
        {
            var form = await service.GetFormAsync(formId);
            if (form == null)
                return NotFound(new
                {
                    message = "Form not found"
                });

            var questions = await service.GetFormQuestionsAsync(formId);
            var responses = await service.GetFormResponsesAsync(formId, 1, int.MaxValue); // Get all

            // Build CSV
            var csv = new StringBuilder();

            // Header row
            csv.Append("Response ID,User ID,Username,Submitted At");
            foreach (var question in questions)
            {
                csv.Append($",\"{question.QuestionText.Replace("\"", "\"\"")}\"");
            }

            csv.AppendLine();

            // Data rows
            foreach (var response in responses)
            {
                var answers = await service.GetResponseAnswersAsync(response.Id);
                var answerDict = answers.ToDictionary(a => a.QuestionId, a => a);

                csv.Append(
                    $"{response.Id},{response.UserId},\"{response.Username}\",{response.SubmittedAt:yyyy-MM-dd HH:mm:ss}");

                foreach (var question in questions)
                {
                    if (answerDict.TryGetValue(question.Id, out var answer))
                    {
                        var answerText = answer.AnswerValues != null && answer.AnswerValues.Length > 0
                            ? string.Join("; ", answer.AnswerValues)
                            : answer.AnswerText ?? "";

                        csv.Append($",\"{answerText.Replace("\"", "\"\"")}\"");
                    }
                    else
                    {
                        csv.Append(",");
                    }
                }

                csv.AppendLine();
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            var fileName = $"form_{formId}_{form.Name.Replace(" ", "_")}_{DateTime.UtcNow:yyyyMMdd}.csv";

            return File(bytes, "text/csv", fileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to export responses for form {FormId}", formId);
            return StatusCode(500, new
            {
                message = "Failed to export responses"
            });
        }
    }

    #endregion

    #region Share Links

    /// <summary>
    ///     Generates a share link for a form.
    /// </summary>
    /// <param name="formId">The form ID.</param>
    /// <param name="instanceIdentifier">The instance identifier (port or name).</param>
    /// <returns>The share code.</returns>
    [HttpPost("{formId:int}/share-link")]
    public async Task<IActionResult> GenerateShareLink(int formId, [FromBody] string instanceIdentifier)
    {
        try
        {
            var shareCode = await service.GenerateShareLinkAsync(formId, instanceIdentifier);
            return Ok(new
            {
                shareCode
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate share link for form {FormId}", formId);
            return StatusCode(500, new
            {
                message = "Failed to generate share link"
            });
        }
    }

    /// <summary>
    ///     Resolves a share code to get form and instance information.
    /// </summary>
    /// <param name="shareCode">The share code.</param>
    /// <returns>Form ID and instance identifier.</returns>
    [HttpGet("share/{shareCode}")]
    public async Task<IActionResult> ResolveShareLink(string shareCode)
    {
        try
        {
            var result = await service.ResolveShareLinkAsync(shareCode);

            if (result == null)
                return NotFound(new
                {
                    message = "Share link not found or expired"
                });

            return Ok(new
            {
                formId = result.Value.FormId, instanceIdentifier = result.Value.InstanceIdentifier
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resolve share code {ShareCode}", shareCode);
            return StatusCode(500, new
            {
                message = "Failed to resolve share link"
            });
        }
    }

    #endregion

    #region Workflow Management

    /// <summary>
    ///     Checks if a user is eligible to submit a form.
    /// </summary>
    /// <param name="formId">The form ID.</param>
    /// <param name="request">The eligibility check request.</param>
    /// <returns>Eligibility status and reason if not eligible.</returns>
    [HttpPost("{formId:int}/check-eligibility")]
    public async Task<IActionResult> CheckEligibility(int formId, [FromBody] EligibilityCheckRequest request)
    {
        try
        {
            var (isEligible, reason) = await service.CheckFormEligibilityAsync(formId, request.UserId);
            return Ok(new
            {
                isEligible, reason
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check eligibility for form {FormId} and user {UserId}", formId,
                request.UserId);
            return StatusCode(500, new
            {
                message = "Failed to check eligibility"
            });
        }
    }

    /// <summary>
    ///     Gets all pending responses for a form.
    /// </summary>
    /// <param name="formId">The form ID.</param>
    /// <param name="status">Optional status filter.</param>
    /// <returns>List of pending responses with workflow information.</returns>
    [HttpGet("{formId:int}/responses/pending")]
    public async Task<IActionResult> GetPendingResponses(
        int formId,
        [FromQuery] ResponseStatus? status = null)
    {
        try
        {
            var responses = await service.GetPendingResponsesAsync(formId, status);

            // Include workflow details for each response
            var responsesWithWorkflow = new List<object>();
            foreach (var response in responses)
            {
                var workflow = await service.GetWorkflowByResponseIdAsync(response.Id);
                responsesWithWorkflow.Add(new
                {
                    Response = response, Workflow = workflow
                });
            }

            return Ok(responsesWithWorkflow);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get pending responses for form {FormId}", formId);
            return StatusCode(500, new
            {
                message = "Failed to retrieve pending responses"
            });
        }
    }

    /// <summary>
    ///     Approves a form response.
    /// </summary>
    /// <param name="responseId">The response ID to approve.</param>
    /// <param name="request">The approval request with reviewer info and notes.</param>
    /// <returns>Success status and invite code if applicable.</returns>
    [HttpPost("responses/{responseId:int}/approve")]
    public async Task<IActionResult> ApproveResponse(int responseId, [FromBody] ApprovalRequest request)
    {
        try
        {
            var (success, inviteCode) = await service.ApproveResponseAsync(
                responseId,
                request.ReviewerId,
                request.Notes
            );

            if (!success)
                return BadRequest(new
                {
                    message = "Failed to approve response"
                });

            return Ok(new
            {
                message = "Response approved successfully", inviteCode
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to approve response {ResponseId}", responseId);
            return StatusCode(500, new
            {
                message = "Failed to approve response"
            });
        }
    }

    /// <summary>
    ///     Rejects a form response.
    /// </summary>
    /// <param name="responseId">The response ID to reject.</param>
    /// <param name="request">The rejection request with reviewer info and notes.</param>
    /// <returns>Success status.</returns>
    [HttpPost("responses/{responseId:int}/reject")]
    public async Task<IActionResult> RejectResponse(int responseId, [FromBody] RejectionRequest request)
    {
        try
        {
            var success = await service.RejectResponseAsync(
                responseId,
                request.ReviewerId,
                request.Notes
            );

            if (!success)
                return BadRequest(new
                {
                    message = "Failed to reject response"
                });

            return Ok(new
            {
                message = "Response rejected successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reject response {ResponseId}", responseId);
            return StatusCode(500, new
            {
                message = "Failed to reject response"
            });
        }
    }

    /// <summary>
    ///     Gets the workflow status for a response using status check token.
    /// </summary>
    /// <param name="token">The status check token.</param>
    /// <returns>Workflow status information including invite code if available.</returns>
    [HttpGet("status/{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetResponseStatus(string token)
    {
        try
        {
            var workflow = await service.GetWorkflowByTokenAsync(token);
            if (workflow == null)
                return NotFound(new
                {
                    message = "Response not found"
                });

            return Ok(new
            {
                status = ((ResponseStatus)workflow.Status).ToString(),
                reviewedAt = workflow.ReviewedAt,
                reviewNotes = workflow.ReviewNotes,
                inviteCode = workflow.InviteCode,
                inviteExpiresAt = workflow.InviteExpiresAt,
                actionTaken = ((WorkflowAction)workflow.ActionTaken).ToString()
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get status for token {Token}", token);
            return StatusCode(500, new
            {
                message = "Failed to retrieve response status"
            });
        }
    }

    #endregion
}

/// <summary>
///     Response from Cloudflare Turnstile verification.
/// </summary>
public class TurnstileVerificationResponse
{
    /// <summary>
    ///     Whether the verification was successful.
    /// </summary>
    public bool Success { get; set; }
}