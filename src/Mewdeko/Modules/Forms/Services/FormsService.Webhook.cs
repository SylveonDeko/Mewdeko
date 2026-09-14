using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DataModel;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.Forms.Services;

/// <summary>
///     Posting a form's activity to the webhook saved with it.
/// </summary>
/// <remarks>
///     The webhook is for feeding a system outside Discord, so it is told about every submission
///     and every decision, but nothing that happens to it can be allowed to affect the submission
///     itself.
/// </remarks>
public partial class FormsService
{
    /// <summary>
    ///     Posts the submission embed to the form's notification webhook, when one is set.
    /// </summary>
    /// <param name="form">The form that was submitted.</param>
    /// <param name="content">The plain text sent above the embed, if any.</param>
    /// <param name="embed">The embed the submit channel receives.</param>
    private Task PostSubmissionToWebhookAsync(Form form, string? content, Embed embed)
    {
        return PostToWebhookAsync(form, content, embed);
    }

    /// <summary>
    ///     Posts a decision on a response to the form's notification webhook, when one is set.
    /// </summary>
    /// <param name="form">The form the response belongs to.</param>
    /// <param name="response">The response that was decided.</param>
    /// <param name="approved">Whether it was approved.</param>
    /// <param name="reviewerId">Who decided it.</param>
    /// <param name="notes">The reason they gave, if any.</param>
    private Task PostDecisionToWebhookAsync(Form form, FormResponse response, bool approved, ulong reviewerId,
        string? notes)
    {
        if (string.IsNullOrWhiteSpace(form.NotificationWebhookUrl))
            return Task.CompletedTask;

        var outcome = approved ? "Approved" : "Rejected";

        var embed = new EmbedBuilder()
            .WithTitle(form.Name)
            .WithDescription($"Response #{response.Id} was {outcome.ToLowerInvariant()}.")
            .WithColor(approved ? Mewdeko.OkColor : Mewdeko.ErrorColor)
            .WithFooter(strings.FormSubmissionFooter(form.GuildId, response.Id, form.Id))
            .WithTimestamp(DateTimeOffset.UtcNow);

        embed.AddField("Submitter", response.UserId is { } userId
            ? response.Username ?? $"<@{userId}>"
            : "Anonymous (login required)", true);

        embed.AddField(outcome, $"by <@{reviewerId}>", true);

        if (!string.IsNullOrWhiteSpace(notes))
            embed.AddField("Reason", notes.TrimTo(1024));

        return PostToWebhookAsync(form, null, embed.Build());
    }

    /// <summary>
    ///     Sends one message to the form's webhook. A failure is logged and otherwise ignored.
    /// </summary>
    /// <param name="form">The form whose webhook receives the message.</param>
    /// <param name="content">The plain text sent above the embed, if any.</param>
    /// <param name="embed">The embed to send.</param>
    private async Task PostToWebhookAsync(Form form, string? content, Embed embed)
    {
        var url = form.NotificationWebhookUrl?.Trim();

        if (string.IsNullOrWhiteSpace(url))
            return;

        if (!WebhookUrlRegex().IsMatch(url))
        {
            logger.LogWarning("Form {FormId} has a notification webhook that is not a Discord webhook URL",
                form.Id);
            return;
        }

        try
        {
            using var http = httpFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(15);

            var payload = new WebhookPayload
            {
                Content = content, Embeds = [WebhookEmbed.From(embed)]
            };

            using var result = await http.PostAsJsonAsync(url, payload).ConfigureAwait(false);

            if (!result.IsSuccessStatusCode)
            {
                logger.LogWarning("Form {FormId} notification webhook returned {Status}", form.Id,
                    (int)result.StatusCode);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to post to the notification webhook for form {FormId}", form.Id);
        }
    }

    [GeneratedRegex(@"^https://(canary\.|ptb\.)?discord(app)?\.com/api/webhooks/\d+/[A-Za-z0-9_\-]+/?$")]
    private static partial Regex WebhookUrlRegex();

    private sealed class WebhookPayload
    {
        [JsonPropertyName("content")]
        public string? Content { get; init; }

        [JsonPropertyName("embeds")]
        public WebhookEmbed[] Embeds { get; init; } = [];

        [JsonPropertyName("allowed_mentions")]
        public WebhookAllowedMentions AllowedMentions { get; init; } = new();
    }

    private sealed class WebhookAllowedMentions
    {
        [JsonPropertyName("parse")]
        public string[] Parse { get; init; } = ["roles"];
    }

    private sealed class WebhookEmbed
    {
        [JsonPropertyName("title")]
        public string? Title { get; init; }

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("color")]
        public uint? Color { get; init; }

        [JsonPropertyName("timestamp")]
        public DateTimeOffset? Timestamp { get; init; }

        [JsonPropertyName("footer")]
        public WebhookEmbedFooter? Footer { get; init; }

        [JsonPropertyName("fields")]
        public WebhookEmbedField[] Fields { get; init; } = [];

        public static WebhookEmbed From(Embed embed)
        {
            return new WebhookEmbed
            {
                Title = embed.Title,
                Description = embed.Description,
                Color = embed.Color?.RawValue,
                Timestamp = embed.Timestamp,
                Footer = embed.Footer is { } footer
                    ? new WebhookEmbedFooter
                    {
                        Text = footer.Text
                    }
                    : null,
                Fields = embed.Fields.Select(f => new WebhookEmbedField
                    {
                        Name = f.Name, Value = f.Value, Inline = f.Inline
                    })
                    .ToArray()
            };
        }
    }

    private sealed class WebhookEmbedFooter
    {
        [JsonPropertyName("text")]
        public string Text { get; init; } = "";
    }

    private sealed class WebhookEmbedField
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = "";

        [JsonPropertyName("value")]
        public string Value { get; init; } = "";

        [JsonPropertyName("inline")]
        public bool Inline { get; init; }
    }
}