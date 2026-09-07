using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Database.Enums;
using Mewdeko.Modules.Forms.Common;

namespace Mewdeko.Modules.Forms.Services;

/// <summary>
///     Saved versions of a form and the comparison between them.
/// </summary>
/// <remarks>
///     The comparison of settings is driven by reflection over <see cref="Form" /> rather than by a
///     hand written list of fields. A hand written list silently stops covering a column the moment
///     someone adds one and forgets to extend it, and a diff that quietly omits a change is worse
///     than no diff at all.
/// </remarks>
public partial class FormsService
{
    /// <summary>
    ///     How many saved versions of a form are kept. A version is written on every save, so
    ///     without a cap the history grows for as long as the form is edited and nobody ever reads
    ///     the far end of it.
    /// </summary>
    public const int VersionsKept = 20;

    private static readonly JsonSerializerOptions SnapshotOptions = new()
    {
        WriteIndented = false
    };

    /// <summary>
    ///     Settings that describe the row rather than the form, and so never belong in a comparison.
    /// </summary>
    private static readonly HashSet<string> UndiffedSettings =
    [
        nameof(Form.Id), nameof(Form.GuildId), nameof(Form.CreatedBy), nameof(Form.CreatedAt),
        nameof(Form.UpdatedAt), nameof(Form.AnnouncedAt)
    ];

    /// <summary>
    ///     Question properties that describe the row rather than the question.
    /// </summary>
    private static readonly HashSet<string> UndiffedQuestionSettings =
    [
        nameof(FormQuestion.Id), nameof(FormQuestion.FormId), nameof(FormQuestion.CreatedAt),
        nameof(FormQuestion.DisplayOrder)
    ];

    /// <summary>
    ///     Captures a form and everything belonging to it as it stands right now.
    /// </summary>
    /// <param name="formId">The form to capture.</param>
    /// <returns>The snapshot, or null when the form does not exist.</returns>
    public async Task<FormSnapshot?> CaptureSnapshotAsync(int formId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var form = await db.Forms.FirstOrDefaultAsync(f => f.Id == formId);
        if (form == null)
            return null;

        var questions = await db.FormQuestions
            .Where(q => q.FormId == formId)
            .OrderBy(q => q.DisplayOrder)
            .ToListAsync();

        var questionIds = questions.Select(q => q.Id).ToList();

        var options = await db.FormQuestionOptions
            .Where(o => questionIds.Contains(o.QuestionId))
            .OrderBy(o => o.DisplayOrder)
            .ToListAsync();

        var conditions = await db.FormQuestionConditions
            .Where(c => questionIds.Contains(c.QuestionId))
            .ToListAsync();

        return new FormSnapshot
        {
            Form = form,
            Questions = questions.Select(q => new FormQuestionSnapshot
            {
                Question = q,
                Options = options.Where(o => o.QuestionId == q.Id).ToList(),
                Conditions = conditions.Where(c => c.QuestionId == q.Id).ToList()
            }).ToList()
        };
    }

    /// <summary>
    ///     Saves the current state of a form as a new version. Called after every save, so a guild
    ///     can see what an edit did and put it back if it was a mistake.
    /// </summary>
    /// <param name="formId">The form to save a version of.</param>
    /// <param name="userId">Who made the edit.</param>
    /// <returns>The saved version, or null when the form does not exist.</returns>
    public async Task<FormVersion?> SaveVersionAsync(int formId, ulong? userId)
    {
        var snapshot = await CaptureSnapshotAsync(formId);
        if (snapshot == null)
            return null;

        await using var db = await dbFactory.CreateConnectionAsync();

        var payload = JsonSerializer.Serialize(snapshot, SnapshotOptions);

        var latest = await db.FormVersions
            .Where(v => v.FormId == formId)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync();

        // The dashboard edits a form one piece at a time and saves after each, so an identical
        // snapshot means nothing actually changed and a new version would only add noise to the
        // history somebody is trying to read.
        if (latest != null && string.Equals(latest.Snapshot, payload, StringComparison.Ordinal))
            return latest;

        var version = new FormVersion
        {
            FormId = formId,
            VersionNumber = (latest?.VersionNumber ?? 0) + 1,
            Snapshot = payload,
            QuestionCount = snapshot.Questions.Count,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        };

        version.Id = await db.InsertWithInt32IdentityAsync(version);

        await PruneVersionsAsync(db, formId);

        logger.LogInformation("Saved version {VersionNumber} of form {FormId}", version.VersionNumber, formId);

        return version;
    }

    /// <summary>
    ///     Drops the oldest versions of a form once there are more than <see cref="VersionsKept" />.
    /// </summary>
    /// <remarks>
    ///     A snapshot holds the whole form, so an actively edited form would otherwise accumulate
    ///     rows indefinitely. Version numbers are never reused, so pruning leaves gaps at the old
    ///     end of the history rather than renumbering what is left.
    /// </remarks>
    private async Task PruneVersionsAsync(MewdekoDb db, int formId)
    {
        var cutoff = await db.FormVersions
            .Where(v => v.FormId == formId)
            .OrderByDescending(v => v.VersionNumber)
            .Skip(VersionsKept)
            .Select(v => (int?)v.VersionNumber)
            .FirstOrDefaultAsync();

        if (cutoff is not { } oldestKept)
            return;

        var removed = await db.FormVersions
            .Where(v => v.FormId == formId && v.VersionNumber <= oldestKept)
            .DeleteAsync();

        if (removed > 0)
            logger.LogInformation("Pruned {Count} old versions of form {FormId}", removed, formId);
    }

    /// <summary>
    ///     Lists the saved versions of a form, newest first, without their snapshots.
    /// </summary>
    /// <param name="formId">The form to list versions of.</param>
    /// <returns>The versions, newest first.</returns>
    public async Task<List<FormVersion>> GetFormVersionsAsync(int formId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await db.FormVersions
            .Where(v => v.FormId == formId)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync();
    }

    /// <summary>
    ///     Reads the form stored inside a saved version.
    /// </summary>
    /// <param name="version">The version to read.</param>
    /// <returns>The form as it stood, or null when the snapshot cannot be read.</returns>
    public static FormSnapshot? ReadSnapshot(FormVersion version)
    {
        try
        {
            return JsonSerializer.Deserialize<FormSnapshot>(version.Snapshot, SnapshotOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Puts a form back to the way it was in a saved version.
    /// </summary>
    /// <remarks>
    ///     Questions are recreated rather than resurrected under their old identifiers, because the
    ///     rows they had were deleted and their identifiers may since have been handed out again.
    ///     Everything pointing at a question by identifier, which means the conditions, the
    ///     conditionally required flags and the answer piping placeholders, is rewritten to the new
    ///     identifiers as part of the restore.
    /// </remarks>
    /// <param name="formId">The form to restore.</param>
    /// <param name="versionNumber">The version to restore it to.</param>
    /// <param name="userId">Who performed the restore, recorded against the version it creates.</param>
    /// <returns>True when the form was restored.</returns>
    public async Task<bool> RestoreVersionAsync(int formId, int versionNumber, ulong? userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var version = await db.FormVersions
            .FirstOrDefaultAsync(v => v.FormId == formId && v.VersionNumber == versionNumber);

        if (version == null)
            return false;

        var snapshot = ReadSnapshot(version);
        if (snapshot == null)
        {
            logger.LogWarning("Version {VersionNumber} of form {FormId} could not be read", versionNumber, formId);
            return false;
        }

        var existing = await db.Forms.FirstOrDefaultAsync(f => f.Id == formId);
        if (existing == null)
            return false;

        // The form's identity and its place in the guild stay put. Everything a guild can edit
        // comes back from the snapshot.
        var restored = snapshot.Form;
        restored.Id = existing.Id;
        restored.GuildId = existing.GuildId;
        restored.CreatedBy = existing.CreatedBy;
        restored.CreatedAt = existing.CreatedAt;
        restored.UpdatedAt = DateTime.UtcNow;

        // A restore never re-announces a launch that already went out.
        restored.AnnouncedAt = existing.AnnouncedAt;

        await db.UpdateAsync(restored);

        var oldQuestionIds = await db.FormQuestions
            .Where(q => q.FormId == formId)
            .Select(q => q.Id)
            .ToListAsync();

        await db.FormQuestionOptions.Where(o => oldQuestionIds.Contains(o.QuestionId)).DeleteAsync();
        await db.FormQuestionConditions.Where(c => oldQuestionIds.Contains(c.QuestionId)).DeleteAsync();
        await db.FormQuestions.Where(q => q.FormId == formId).DeleteAsync();

        var idMap = new Dictionary<int, int>();

        foreach (var captured in snapshot.Questions.OrderBy(q => q.Question.DisplayOrder))
        {
            var originalId = captured.Question.Id;

            captured.Question.Id = 0;
            captured.Question.FormId = formId;

            var newId = await db.InsertWithInt32IdentityAsync(captured.Question);
            idMap[originalId] = newId;
            captured.Question.Id = newId;
        }

        foreach (var captured in snapshot.Questions)
        {
            var question = captured.Question;

            question.ConditionalParentQuestionId = Remap(question.ConditionalParentQuestionId, idMap);
            question.RequiredWhenParentQuestionId = Remap(question.RequiredWhenParentQuestionId, idMap);
            question.QuestionText = RemapPiping(question.QuestionText, idMap);
            question.Placeholder = RemapPiping(question.Placeholder, idMap);

            await db.UpdateAsync(question);

            foreach (var option in captured.Options)
            {
                option.Id = 0;
                option.QuestionId = question.Id;
                await db.InsertAsync(option);
            }

            foreach (var condition in captured.Conditions)
            {
                condition.Id = 0;
                condition.QuestionId = question.Id;
                condition.TargetQuestionId = Remap(condition.TargetQuestionId, idMap);
                await db.InsertAsync(condition);
            }
        }

        logger.LogInformation("Restored form {FormId} to version {VersionNumber}", formId, versionNumber);

        // The restore is itself an edit, so it gets a version of its own and can be undone in turn.
        await SaveVersionAsync(formId, userId);

        return true;
    }

    /// <summary>
    ///     Points an identifier at the question that replaced it, dropping it when the question it
    ///     named is not part of the restored version.
    /// </summary>
    private static int? Remap(int? original, IReadOnlyDictionary<int, int> idMap)
    {
        if (original is not { } id)
            return null;

        return idMap.TryGetValue(id, out var replacement) ? replacement : null;
    }

    /// <summary>
    ///     Rewrites the answer piping placeholders in a piece of text to the identifiers the
    ///     restored questions were given.
    /// </summary>
    private static string? RemapPiping(string? text, IReadOnlyDictionary<int, int> idMap)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        return FormValidator.PipingPlaceholderRegex().Replace(text, match =>
        {
            if (int.TryParse(match.Groups[1].Value, out var id) && idMap.TryGetValue(id, out var replacement))
                return "{{Q" + replacement + "}}";

            return match.Value;
        });
    }

    /// <summary>
    ///     Describes what a saved version changed, compared against the version before it.
    /// </summary>
    /// <param name="formId">The form the version belongs to.</param>
    /// <param name="versionNumber">The version to describe.</param>
    /// <param name="names">
    ///     Names for the roles and channels the form points at, so a change reads as a name rather
    ///     than a snowflake. Anything missing, such as a deleted role, falls back to its identifier.
    /// </param>
    /// <returns>One entry per change, or an empty list when nothing changed.</returns>
    public async Task<List<FormVersionChange>> DescribeVersionAsync(
        int formId,
        int versionNumber,
        IReadOnlyDictionary<ulong, string>? names = null)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var current = await db.FormVersions
            .FirstOrDefaultAsync(v => v.FormId == formId && v.VersionNumber == versionNumber);

        if (current == null)
            return [];

        var previous = await db.FormVersions
            .Where(v => v.FormId == formId && v.VersionNumber < versionNumber)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync();

        var currentSnapshot = ReadSnapshot(current);
        if (currentSnapshot == null)
            return [];

        return DiffVersions(previous == null ? null : ReadSnapshot(previous), currentSnapshot, names);
    }

    /// <summary>
    ///     Describes what changed between two versions of a form.
    /// </summary>
    /// <param name="previous">The earlier version, or null when this is the first version saved.</param>
    /// <param name="current">The version being described.</param>
    /// <param name="names">Names for the roles and channels the form points at.</param>
    /// <returns>One entry per change, settings first and then questions.</returns>
    public static List<FormVersionChange> DiffVersions(
        FormSnapshot? previous,
        FormSnapshot current,
        IReadOnlyDictionary<ulong, string>? names = null)
    {
        var lookup = names ?? new Dictionary<ulong, string>();
        var changes = new List<FormVersionChange>();

        if (previous == null)
        {
            changes.Add(new FormVersionChange(FormVersionChangeKind.Added, "Form", "Created", null,
                current.Form.Name));

            foreach (var question in current.Questions)
            {
                changes.Add(new FormVersionChange(FormVersionChangeKind.Added,
                    SectionFor(question, current.Questions.IndexOf(question) + 1), "Question", null,
                    Shorten(question.Question.QuestionText)));
            }

            return changes;
        }

        CompareSettings(previous.Form, current.Form, changes, lookup);
        CompareQuestions(previous.Questions, current.Questions, changes);

        return changes;
    }

    /// <summary>
    ///     Adds an entry for each form setting that differs between two versions. Every readable and
    ///     writable property of <see cref="Form" /> is compared except the bookkeeping ones, so a
    ///     column added later is covered without anything here being touched.
    /// </summary>
    private static void CompareSettings(
        Form previous,
        Form current,
        List<FormVersionChange> changes,
        IReadOnlyDictionary<ulong, string> names)
    {
        foreach (var property in DiffableProperties(typeof(Form), UndiffedSettings))
        {
            var before = Describe(property, property.GetValue(previous), names);
            var after = Describe(property, property.GetValue(current), names);

            if (!string.Equals(before, after, StringComparison.Ordinal))
            {
                changes.Add(new FormVersionChange(FormVersionChangeKind.Changed, "Settings",
                    Humanize(property.Name), before, after));
            }
        }
    }

    /// <summary>
    ///     Adds an entry for each question added, removed or edited between two versions. Questions
    ///     are matched by identifier, so a question that merely moved reads as a move rather than as
    ///     one deletion and one addition.
    /// </summary>
    private static void CompareQuestions(
        List<FormQuestionSnapshot> previous,
        List<FormQuestionSnapshot> current,
        List<FormVersionChange> changes)
    {
        var previousById = previous
            .Where(q => q.Question.Id > 0)
            .ToDictionary(q => q.Question.Id);

        var matched = new HashSet<int>();

        for (var index = 0; index < current.Count; index++)
        {
            var question = current[index];
            var position = index + 1;
            var section = SectionFor(question, position);

            if (question.Question.Id <= 0 || !previousById.TryGetValue(question.Question.Id, out var before))
            {
                changes.Add(new FormVersionChange(FormVersionChangeKind.Added, section, "Question", null,
                    Shorten(question.Question.QuestionText)));
                continue;
            }

            matched.Add(question.Question.Id);

            foreach (var property in DiffableProperties(typeof(FormQuestion), UndiffedQuestionSettings))
            {
                var beforeValue = Describe(property, property.GetValue(before.Question), null);
                var afterValue = Describe(property, property.GetValue(question.Question), null);

                if (!string.Equals(beforeValue, afterValue, StringComparison.Ordinal))
                {
                    changes.Add(new FormVersionChange(FormVersionChangeKind.Changed, section,
                        Humanize(property.Name), beforeValue, afterValue));
                }
            }

            var previousPosition = previous.FindIndex(q => q.Question.Id == question.Question.Id) + 1;
            if (previousPosition != position)
            {
                changes.Add(new FormVersionChange(FormVersionChangeKind.Changed, section, "Position",
                    previousPosition.ToString(), position.ToString()));
            }

            var beforeOptions = string.Join(", ", before.Options.Select(o => o.OptionText));
            var afterOptions = string.Join(", ", question.Options.Select(o => o.OptionText));

            if (!string.Equals(beforeOptions, afterOptions, StringComparison.Ordinal))
            {
                changes.Add(new FormVersionChange(FormVersionChangeKind.Changed, section, "Options",
                    beforeOptions, afterOptions));
            }

            if (before.Conditions.Count != question.Conditions.Count)
            {
                changes.Add(new FormVersionChange(FormVersionChangeKind.Changed, section, "Conditions",
                    before.Conditions.Count.ToString(), question.Conditions.Count.ToString()));
            }
        }

        foreach (var removed in previous.Where(q => q.Question.Id > 0 && !matched.Contains(q.Question.Id)))
        {
            var section = SectionFor(removed, previous.IndexOf(removed) + 1);

            changes.Add(new FormVersionChange(FormVersionChangeKind.Removed, section, "Question",
                Shorten(removed.Question.QuestionText), null));
        }
    }

    /// <summary>
    ///     The properties of a model that are worth comparing, in declaration order.
    /// </summary>
    private static IEnumerable<PropertyInfo> DiffableProperties(Type type, HashSet<string> skip)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && !skip.Contains(p.Name));
    }

    /// <summary>
    ///     Renders a setting the way it should read in a comparison. Snowflake and role list
    ///     properties are recognised by their names, which is what lets a newly added column read
    ///     correctly without being registered anywhere.
    /// </summary>
    private static string Describe(PropertyInfo property, object? value, IReadOnlyDictionary<ulong, string>? names)
    {
        if (value == null)
            return "not set";

        if (names != null)
        {
            if (property.Name.EndsWith("RoleIds", StringComparison.Ordinal) && value is string roleList)
                return DescribeIdList(roleList, names, "@");

            if (property.Name.EndsWith("ChannelId", StringComparison.Ordinal) && value is ulong channelId)
                return DescribeMention(channelId, names, "#");

            if (property.Name.EndsWith("RoleId", StringComparison.Ordinal) && value is ulong roleId)
                return DescribeMention(roleId, names, "@");
        }

        if (property.Name == nameof(Form.FormType) && value is int formType)
            return ((FormType)formType).ToString();

        if (property.Name is nameof(Form.ApprovalActionType) or nameof(Form.RejectionActionType)
            && value is int action)
            return ((RoleActionType)action).ToString();

        return value switch
        {
            bool flag => flag ? "Enabled" : "Disabled",
            DateTime moment => moment.ToString("yyyy-MM-dd HH:mm") + " UTC",
            string text => string.IsNullOrWhiteSpace(text) ? "not set" : text,
            _ => value.ToString() ?? "not set"
        };
    }

    /// <summary>
    ///     Names the role or channel an identifier points at, falling back to the identifier when it
    ///     no longer exists to be named.
    /// </summary>
    private static string DescribeMention(ulong id, IReadOnlyDictionary<ulong, string> names, string prefix)
    {
        if (id == 0)
            return "not set";

        return names.TryGetValue(id, out var name) ? prefix + name : id.ToString();
    }

    /// <summary>
    ///     Names each role in a stored comma separated list.
    /// </summary>
    private static string DescribeIdList(string? stored, IReadOnlyDictionary<ulong, string> names, string prefix)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return "not set";

        var described = stored
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(entry => ulong.TryParse(entry, out var id) && names.TryGetValue(id, out var name)
                ? prefix + name
                : entry);

        return string.Join(", ", described);
    }

    /// <summary>
    ///     Turns a property name into something a reader recognises, so "MinAccountAgeDays" reads as
    ///     "Min account age days".
    /// </summary>
    private static string Humanize(string name)
    {
        var spaced = HumanizeRegex().Replace(name, " $1").Trim();
        return char.ToUpperInvariant(spaced[0]) + spaced[1..].ToLowerInvariant();
    }

    /// <summary>
    ///     The heading a question's changes are grouped under.
    /// </summary>
    private static string SectionFor(FormQuestionSnapshot question, int position)
    {
        return $"Question {position}: {Shorten(question.Question.QuestionText)}";
    }

    /// <summary>
    ///     Trims a question to something short enough to head a group of changes.
    /// </summary>
    private static string Shorten(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "Untitled";

        return text.Length <= 40 ? text : text[..37] + "...";
    }

    [GeneratedRegex("(?<!^)([A-Z])")]
    private static partial Regex HumanizeRegex();
}