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
    private readonly IDashboardAuditContext auditContext;
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
    /// <param name="auditContext">Records before/after state for the dashboard audit log.</param>
    public FormsController(
        FormsService service,
        DiscordShardedClient client,
        BotCredentials creds,
        HttpClient httpClient,
        ILogger<FormsController> logger,
        IDashboardAuditContext auditContext)
    {
        this.service = service;
        this.client = client;
        this.creds = creds;
        this.httpClient = httpClient;
        this.logger = logger;
        this.auditContext = auditContext;
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

    /// <summary>
    ///     Saves a whole form in one call: its settings, questions, options and conditions.
    /// </summary>
    /// <remarks>
    ///     Replaces the per-question and per-option calls the builder used to make, which for a
    ///     long form meant dozens of sequential requests and could leave a form half saved.
    /// </remarks>
    /// <param name="guildId">The guild the form belongs to.</param>
    /// <param name="request">The form and its questions.</param>
    /// <returns>The saved form, or the validation errors that stopped it.</returns>
    [HttpPost("guild/{guildId:ulong}/save")]
    public async Task<IActionResult> SaveForm(ulong guildId, [FromBody] FormSaveRequest request)
    {
        try
        {
            var (form, errors) = await service.SaveFormAsync(guildId, request);

            if (errors.Count > 0)
                return BadRequest(new
                {
                    message = "This form could not be saved", errors
                });

            return Ok(form);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save form for guild {GuildId}", guildId);
            return StatusCode(500, new
            {
                message = "Failed to save form"
            });
        }
    }

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
            var counts = await service.GetFormCountsAsync(guildId);

            var formsWithCounts = new List<object>();

            foreach (var form in forms)
            {
                var formCounts = counts.GetValueOrDefault(form.Id, new FormsService.FormCounts(0, 0, 0));

                // Every form setting is carried, rather than a hand written field list, because a
                // list like that silently stops covering a column the moment somebody adds one and
                // the dashboard then loses that setting on its next save.
                var summary = DescribeForm(form);
                summary["questionCount"] = formCounts.QuestionCount;
                summary["responseCount"] = formCounts.ResponseCount;

                // A form that does not review its responses has nothing pending to show.
                summary["pendingCount"] = FormsService.ReviewsResponses(form) ? formCounts.PendingCount : 0;

                formsWithCounts.Add(summary);
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
            auditContext.RecordAfter(createdForm);
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
            auditContext.RecordBefore(await service.GetFormAsync(formId));
            var success = await service.UpdateFormAsync(form);

            if (!success)
                return NotFound(new
                {
                    message = "Form not found"
                });

            auditContext.RecordAfter(form);
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
            auditContext.RecordBefore(await service.GetFormAsync(formId));
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
            var form = await service.GetFormAsync(formId);
            if (form == null)
                return NotFound(new
                {
                    message = "Form not found"
                });

            // Publishing is the point at which the form has to make sense as a whole, so the
            // checks that span more than one question run here rather than on each edit, where an
            // intermediate state is legitimately half finished.
            var questions = await service.GetFormQuestionsAsync(formId);
            var errors = FormValidator.ValidateForm(form);

            errors.AddRange(FormValidator.ValidatePiping(questions));

            foreach (var question in questions)
            {
                var options = await service.GetQuestionOptionsAsync(question.Id);
                errors.AddRange(FormValidator.ValidateQuestionOptions(question.QuestionType, options));
            }

            if (errors.Count > 0)
                return BadRequest(new
                {
                    message = "This form cannot be published yet", errors
                });

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
                    question.ImageUrl,
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
    ///     Updates an existing question option.
    /// </summary>
    /// <param name="optionId">The option ID.</param>
    /// <param name="option">The updated option.</param>
    /// <returns>Success status.</returns>
    [HttpPut("questions/options/{optionId:int}")]
    public async Task<IActionResult> UpdateQuestionOption(int optionId, [FromBody] FormQuestionOption option)
    {
        try
        {
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

            option.Id = optionId; // Ensure option ID matches route
            var success = await service.UpdateQuestionOptionAsync(option);

            if (!success)
                return NotFound(new
                {
                    message = "Option not found"
                });

            return Ok(new
            {
                message = "Option updated successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update option {OptionId}", optionId);
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    /// <summary>
    ///     Deletes a single question option.
    /// </summary>
    /// <param name="optionId">The option ID to delete.</param>
    /// <returns>Success status.</returns>
    [HttpDelete("questions/options/{optionId:int}")]
    public async Task<IActionResult> DeleteQuestionOption(int optionId)
    {
        try
        {
            var success = await service.DeleteQuestionOptionAsync(optionId);

            if (!success)
                return NotFound(new
                {
                    message = "Option not found"
                });

            return Ok(new
            {
                message = "Option deleted successfully"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete option {OptionId}", optionId);
            return StatusCode(500, new
            {
                message = "Failed to delete option"
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

            // One gate covers who may submit, when, and how often, so nothing depends on which
            // endpoint the submission arrived through.
            var eligibility = await service.CheckFormEligibilityAsync(form, request.UserId);
            if (!eligibility.IsEligible)
                return BadRequest(new
                {
                    message = eligibility.Reason, retryAt = eligibility.RetryAt
                });

            // Submit response. The answers themselves are validated inside, against the questions
            // the form's own conditions actually showed this person.
            var response = await service.SubmitResponseAsync(
                formId,
                request.UserId,
                request.Username,
                request.Answers,
                request.IpAddress
            );

            // Create workflow entry for the response
            var workflow = await service.CreateWorkflowForResponseAsync(response.Id);

            await service.ApplySubmitRolesAsync(form, request.UserId);

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
        catch (FormSubmissionException ex)
        {
            // Reported per question so the page can mark the answers that need fixing where they
            // sit, rather than showing one message at the foot of a long form.
            return BadRequest(new
            {
                message = ex.Message, errors = ex.Errors.ToDictionary(e => e.Key.ToString(), e => e.Value)
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
    ///     Gets a page of a form's responses, each with its review state and answers.
    /// </summary>
    /// <param name="formId">The form ID.</param>
    /// <param name="page">Page number (1-indexed).</param>
    /// <param name="pageSize">Number of responses per page.</param>
    /// <param name="status">Only responses in this review state, or omitted for all of them.</param>
    /// <returns>The page of responses, with the count in each review state.</returns>
    [HttpGet("{formId:int}/responses")]
    public async Task<IActionResult> GetFormResponses(
        int formId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] ResponseStatus? status = null)
    {
        try
        {
            // The queue carries each response's review state and answers with it, so the page a
            // reviewer works through is one request rather than one per row.
            var queue = await service.GetResponseQueueAsync(formId, status, page, pageSize);

            return Ok(new
            {
                responses = queue.Responses.Select(r => new
                {
                    r.Response, r.Workflow, r.Answers, r.RevisionCount
                }),
                totalCount = queue.TotalCount,
                page = queue.Page,
                pageSize = queue.PageSize,
                totalPages = queue.TotalPages,
                statusCounts = queue.StatusCounts
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

    private static string? GetInstanceIdentifier(JsonElement request)
    {
        return request.ValueKind switch
        {
            JsonValueKind.String => request.GetString(),
            JsonValueKind.Object when request.TryGetProperty("instanceIdentifier", out var identifier) &&
                                      identifier.ValueKind == JsonValueKind.String => identifier.GetString(),
            JsonValueKind.Object when request.TryGetProperty("InstanceIdentifier", out var identifier) &&
                                      identifier.ValueKind == JsonValueKind.String => identifier.GetString(),
            _ => null
        };
    }

    /// <summary>
    ///     Generates a share link for a form.
    /// </summary>
    /// <param name="formId">The form ID.</param>
    /// <param name="request">The share link request body.</param>
    /// <returns>The share code.</returns>
    [HttpPost("{formId:int}/share-link")]
    public async Task<IActionResult> GenerateShareLink(int formId, [FromBody] JsonElement request)
    {
        try
        {
            var instanceIdentifier = GetInstanceIdentifier(request)?.Trim();
            if (string.IsNullOrWhiteSpace(instanceIdentifier))
                return BadRequest(new
                {
                    message = "Instance identifier is required"
                });

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
            var eligibility = await service.CheckFormEligibilityAsync(formId, request.UserId);

            // RetryAt lets the page count down to the moment a temporary refusal lifts, instead of
            // telling someone they cannot submit and leaving them to guess when they can.
            return Ok(new
            {
                eligibility.IsEligible, eligibility.Reason, eligibility.RetryAt
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

            // The status page is the only thing a submitter reliably still has a link to, so it is
            // also where they need to be able to correct an answer a reviewer asked about.
            var response = await service.GetResponseDetailsAsync(workflow.ResponseId);
            var (canEdit, editReason) = await service.CanEditResponseAsync(workflow.ResponseId);

            var form = response == null ? null : await service.GetFormAsync(response.FormId);
            var guild = form == null ? null : client.GetGuild(form.GuildId);

            return Ok(new
            {
                status = ((ResponseStatus)workflow.Status).ToString(),
                reviewedAt = workflow.ReviewedAt,
                reviewNotes = workflow.ReviewNotes,
                inviteCode = workflow.InviteCode,
                inviteExpiresAt = workflow.InviteExpiresAt,
                actionTaken = ((WorkflowAction)workflow.ActionTaken).ToString(),
                dmFailed = workflow.DmFailed,
                responseId = workflow.ResponseId,
                formId = response?.FormId,
                formName = form?.Name,
                shareCode = response == null ? null : await service.GetShareCodeAsync(response.FormId),

                // A status link is often the only thing somebody still has, so the page it opens
                // has to say on its own which server the response was sent to.
                guildId = form?.GuildId,
                guildName = guild?.Name,
                guildIconUrl = guild?.IconUrl,
                submittedAt = response?.SubmittedAt,
                editedAt = response?.EditedAt,
                canEdit,
                editReason
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

    #region Guild Defaults

    /// <summary>
    ///     Gets the guild's default review button emotes, which every form falls back to.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The configured emotes, null where the guild has not set one.</returns>
    [HttpGet("guild/{guildId:ulong}/review-emotes")]
    public async Task<IActionResult> GetReviewEmotes(ulong guildId)
    {
        try
        {
            var (approve, reject) = await service.GetGuildReviewEmotesAsync(guildId);

            return Ok(new
            {
                approveEmote = approve, rejectEmote = reject
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read review emotes for guild {GuildId}", guildId);
            return StatusCode(500, new
            {
                message = "Failed to retrieve review emotes"
            });
        }
    }

    /// <summary>
    ///     Sets the guild's default review button emotes. An empty value clears one, restoring the
    ///     built-in tick or cross.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The emotes to store.</param>
    /// <returns>Success status.</returns>
    [HttpPost("guild/{guildId:ulong}/review-emotes")]
    public async Task<IActionResult> SetReviewEmotes(ulong guildId, [FromBody] FormReviewEmotesRequest request)
    {
        try
        {
            var error = await service.SetGuildReviewEmotesAsync(guildId, request.ApproveEmote, request.RejectEmote);

            if (error != null)
                return BadRequest(new
                {
                    message = error
                });

            return Ok(new
            {
                message = "Review emotes updated"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set review emotes for guild {GuildId}", guildId);
            return StatusCode(500, new
            {
                message = "Failed to update review emotes"
            });
        }
    }

    #endregion

    #region Versions

    /// <summary>
    ///     Lists the saved versions of a form, newest first.
    /// </summary>
    /// <param name="formId">The form ID.</param>
    /// <returns>The versions, without their snapshots.</returns>
    [HttpGet("{formId:int}/versions")]
    public async Task<IActionResult> GetFormVersions(int formId)
    {
        try
        {
            var versions = await service.GetFormVersionsAsync(formId);

            return Ok(new
            {
                versionsKept = FormsService.VersionsKept,
                versions = versions.Select(v => new
                {
                    v.Id,
                    v.VersionNumber,
                    v.QuestionCount,
                    v.CreatedBy,
                    v.CreatedAt
                })
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list versions for form {FormId}", formId);
            return StatusCode(500, new
            {
                message = "Failed to retrieve versions"
            });
        }
    }

    /// <summary>
    ///     Describes what a saved version changed, compared against the version before it.
    /// </summary>
    /// <param name="formId">The form ID.</param>
    /// <param name="versionNumber">The version to describe.</param>
    /// <returns>The changes, grouped by section.</returns>
    [HttpGet("{formId:int}/versions/{versionNumber:int}/diff")]
    public async Task<IActionResult> GetVersionDiff(int formId, int versionNumber)
    {
        try
        {
            var form = await service.GetFormAsync(formId);
            if (form == null)
                return NotFound(new
                {
                    message = "Form not found"
                });

            var changes = await service.DescribeVersionAsync(formId, versionNumber, NamesFor(form.GuildId));

            return Ok(changes.Select(c => new
            {
                kind = c.Kind.ToString(),
                c.Section,
                c.Label,
                c.Before,
                c.After
            }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to diff version {VersionNumber} of form {FormId}", versionNumber, formId);
            return StatusCode(500, new
            {
                message = "Failed to compare versions"
            });
        }
    }

    /// <summary>
    ///     Saves the current state of a form as a new version. A save that changes nothing reuses
    ///     the existing version rather than adding a duplicate to the history.
    /// </summary>
    /// <param name="formId">The form ID.</param>
    /// <param name="userId">Who made the edit.</param>
    /// <returns>The saved version.</returns>
    [HttpPost("{formId:int}/versions")]
    public async Task<IActionResult> SaveFormVersion(int formId, [FromBody] ulong userId)
    {
        try
        {
            var version = await service.SaveVersionAsync(formId, userId);

            if (version == null)
                return NotFound(new
                {
                    message = "Form not found"
                });

            return Ok(new
            {
                version.Id, version.VersionNumber, version.QuestionCount, version.CreatedAt
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save a version of form {FormId}", formId);
            return StatusCode(500, new
            {
                message = "Failed to save version"
            });
        }
    }

    /// <summary>
    ///     Puts a form back to the way it was in a saved version.
    /// </summary>
    /// <param name="formId">The form ID.</param>
    /// <param name="versionNumber">The version to restore.</param>
    /// <param name="userId">Who performed the restore.</param>
    /// <returns>Success status.</returns>
    [HttpPost("{formId:int}/versions/{versionNumber:int}/restore")]
    public async Task<IActionResult> RestoreFormVersion(int formId, int versionNumber, [FromBody] ulong userId)
    {
        try
        {
            var restored = await service.RestoreVersionAsync(formId, versionNumber, userId);

            if (!restored)
                return NotFound(new
                {
                    message = "That version could not be restored"
                });

            return Ok(new
            {
                message = "Form restored"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to restore form {FormId} to version {VersionNumber}", formId, versionNumber);
            return StatusCode(500, new
            {
                message = "Failed to restore version"
            });
        }
    }

    /// <summary>
    ///     Writes out every setting of a form, keyed the way the dashboard expects to read it.
    /// </summary>
    /// <remarks>
    ///     Driven by reflection rather than by a written out field list, so a column added to the
    ///     form later reaches the dashboard without anything here being touched. A field list that
    ///     falls behind does not fail loudly, it just drops a setting on the next save.
    /// </remarks>
    /// <param name="form">The form to describe.</param>
    /// <returns>The form's settings, keyed by their camel cased names.</returns>
    private static Dictionary<string, object?> DescribeForm(Form form)
    {
        var described = new Dictionary<string, object?>();

        foreach (var property in typeof(Form).GetProperties())
        {
            var name = char.ToLowerInvariant(property.Name[0]) + property.Name[1..];
            described[name] = property.GetValue(form);
        }

        return described;
    }

    /// <summary>
    ///     Names the roles and channels of a guild, so a version comparison reads as names rather
    ///     than as snowflakes.
    /// </summary>
    private Dictionary<ulong, string> NamesFor(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        var names = new Dictionary<ulong, string>();

        if (guild == null)
            return names;

        foreach (var role in guild.Roles)
            names[role.Id] = role.Name;

        foreach (var channel in guild.Channels)
            names[channel.Id] = channel.Name;

        return names;
    }

    #endregion

    #region Drafts

    /// <summary>
    ///     Reads a submitter's partly filled copy of a form, so they resume where they left off.
    /// </summary>
    /// <param name="formId">The form ID.</param>
    /// <param name="userId">The submitter.</param>
    /// <returns>The draft, or a null draft when there is none.</returns>
    [HttpGet("{formId:int}/draft/{userId:ulong}")]
    public async Task<IActionResult> GetDraft(int formId, ulong userId)
    {
        try
        {
            var draft = await service.GetDraftAsync(formId, userId);

            if (draft == null)
                return Ok(new
                {
                    hasDraft = false
                });

            return Ok(new
            {
                hasDraft = true,
                answers = FormsService.ReadDraftAnswers(draft).ToDictionary(a => a.Key.ToString(), a => a.Value),
                draft.Page,
                draft.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read the draft of form {FormId} for user {UserId}", formId, userId);
            return StatusCode(500, new
            {
                message = "Failed to read draft"
            });
        }
    }

    /// <summary>
    ///     Saves what a submitter has filled in so far.
    /// </summary>
    /// <param name="formId">The form ID.</param>
    /// <param name="request">The answers so far and the page they had reached.</param>
    /// <returns>When the draft was saved.</returns>
    [HttpPost("{formId:int}/draft")]
    public async Task<IActionResult> SaveDraft(int formId, [FromBody] FormDraftRequest request)
    {
        try
        {
            var savedAt = await service.SaveDraftAsync(formId, request.UserId, request.Answers, request.Page);

            return Ok(new
            {
                savedAt
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save the draft of form {FormId}", formId);
            return StatusCode(500, new
            {
                message = "Failed to save draft"
            });
        }
    }

    /// <summary>
    ///     Discards a submitter's draft, for when they want to start over.
    /// </summary>
    /// <param name="formId">The form ID.</param>
    /// <param name="userId">The submitter.</param>
    /// <returns>Success status.</returns>
    [HttpDelete("{formId:int}/draft/{userId:ulong}")]
    public async Task<IActionResult> DeleteDraft(int formId, ulong userId)
    {
        try
        {
            await service.DeleteDraftAsync(formId, userId);

            return Ok(new
            {
                message = "Draft discarded"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to discard the draft of form {FormId} for user {UserId}", formId, userId);
            return StatusCode(500, new
            {
                message = "Failed to discard draft"
            });
        }
    }

    #endregion

    #region Response Editing

    /// <summary>
    ///     Lists everything a person has submitted, newest first.
    /// </summary>
    /// <param name="userId">The submitter.</param>
    /// <param name="guildId">A guild to narrow the list to, or null for everything.</param>
    /// <returns>Their submissions.</returns>
    [HttpGet("submissions/{userId:ulong}")]
    public async Task<IActionResult> GetUserSubmissions(ulong userId, [FromQuery] ulong? guildId = null)
    {
        try
        {
            var submissions = await service.GetUserSubmissionsAsync(userId, guildId);

            return Ok(submissions.Select(s => new
            {
                s.ResponseId,
                s.FormId,
                s.FormName,
                s.GuildId,
                s.GuildName,
                s.GuildIconUrl,
                status = s.Status.ToString(),
                s.SubmittedAt,
                s.EditedAt,
                s.ReviewedAt,
                s.ReviewNotes,
                s.StatusToken,
                s.CanEdit
            }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list submissions for user {UserId}", userId);
            return StatusCode(500, new
            {
                message = "Failed to retrieve submissions"
            });
        }
    }

    /// <summary>
    ///     Replaces the answers of a response with a corrected set, keeping the original as a revision.
    /// </summary>
    /// <param name="responseId">The response to change.</param>
    /// <param name="request">Who is making the change and the corrected answers.</param>
    /// <returns>Success status.</returns>
    [HttpPut("responses/{responseId:int}")]
    public async Task<IActionResult> EditResponse(int responseId, [FromBody] FormSubmissionRequest request)
    {
        try
        {
            var response = await service.EditResponseAsync(responseId, request.UserId, request.Answers);

            return Ok(new
            {
                message = "Response updated", response.Id, response.EditedAt
            });
        }
        catch (FormSubmissionException ex)
        {
            return BadRequest(new
            {
                message = ex.Message, errors = ex.Errors.ToDictionary(e => e.Key.ToString(), e => e.Value)
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to edit response {ResponseId}", responseId);
            return StatusCode(500, new
            {
                message = "Failed to update response"
            });
        }
    }

    /// <summary>
    ///     Lists the earlier versions of a response's answers, newest first, so a reviewer can see
    ///     what an edit replaced.
    /// </summary>
    /// <param name="responseId">The response.</param>
    /// <returns>The revisions and the answers each one held.</returns>
    [HttpGet("responses/{responseId:int}/revisions")]
    public async Task<IActionResult> GetResponseRevisions(int responseId)
    {
        try
        {
            var revisions = await service.GetResponseRevisionsAsync(responseId);

            return Ok(revisions.Select(r => new
            {
                r.Id,
                r.EditedBy,
                r.CreatedAt,
                answers = FormsService.ReadRevisionAnswers(r).Select(a => new
                {
                    a.QuestionId,
                    a.QuestionText,
                    a.QuestionType,
                    a.AnswerText,
                    a.AnswerValues,
                    a.AnswerDisplay
                })
            }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list revisions of response {ResponseId}", responseId);
            return StatusCode(500, new
            {
                message = "Failed to retrieve revisions"
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