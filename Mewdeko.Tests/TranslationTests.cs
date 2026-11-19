using System.IO;
using Mewdeko.Services.strings;
using Mewdeko.Services.strings.impl;
using Mewdeko.Services.Translations;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Mewdeko.Tests;

/// <summary>
///     Covers the translation catalog, the overrides and the registration of the bot catalog.
/// </summary>
[TestFixture]
public class TranslationTests
{
    private static ResponseFile File(string locale, string group, params (string Key, string Text)[] strings)
    {
        return new ResponseFile(locale, group, strings.ToDictionary(x => x.Key, x => x.Text));
    }

    private static List<ResponseFile> SampleFiles()
    {
        return
        [
            File("en-US", "xp", ("xp_level", "Level {0}"), ("xp_reset", "Reset")),
            File("en-US", "_common", ("ok", "Done")),
            File("de-DE", "xp", ("xp_level", "Stufe {0}")),
            File("fr-FR", "xp", ("xp_level", "Niveau {0}"), ("gone", "Parti"))
        ];
    }

    /// <summary>
    ///     The same files give the same hash, whatever order they are listed in.
    /// </summary>
    [Test]
    public void Hash_IsStable_AndIgnoresFileOrder()
    {
        var first = TranslationCatalog.Build(SampleFiles());
        var second = TranslationCatalog.Build(SampleFiles().AsEnumerable().Reverse());

        Assert.Multiple(() =>
        {
            Assert.That(first.Hash, Has.Length.EqualTo(64));
            Assert.That(first.Hash, Is.EqualTo(second.Hash));
        });
    }

    /// <summary>
    ///     Changing a source text changes the hash.
    /// </summary>
    [Test]
    public void Hash_ChangesWhenSourceTextChanges()
    {
        var files = SampleFiles();
        var before = TranslationCatalog.Build(files).Hash;
        files[0] = File("en-US", "xp", ("xp_level", "Level {0}!"), ("xp_reset", "Reset"));

        Assert.That(TranslationCatalog.Build(files).Hash, Is.Not.EqualTo(before));
    }

    /// <summary>
    ///     Changing a baseline changes the hash.
    /// </summary>
    [Test]
    public void Hash_ChangesWhenBaselineChanges()
    {
        var files = SampleFiles();
        var before = TranslationCatalog.Build(files).Hash;
        files[2] = File("de-DE", "xp", ("xp_level", "Level {0}"));

        Assert.That(TranslationCatalog.Build(files).Hash, Is.Not.EqualTo(before));
    }

    /// <summary>
    ///     Moving a key to another file changes the hash, because its group changes.
    /// </summary>
    [Test]
    public void Hash_ChangesWhenGroupChanges()
    {
        var before = TranslationCatalog.Build([File("en-US", "a", ("k", "v"))]).Hash;
        var after = TranslationCatalog.Build([File("en-US", "b", ("k", "v"))]).Hash;

        Assert.That(after, Is.Not.EqualTo(before));
    }

    /// <summary>
    ///     Field boundaries are part of the hash, so shifting text between fields changes it.
    /// </summary>
    [Test]
    public void Hash_DistinguishesFieldBoundaries()
    {
        var a = TranslationCatalog.Build([File("en-US", "g", ("ab", "c"))]).Hash;
        var b = TranslationCatalog.Build([File("en-US", "g", ("a", "bc"))]).Hash;

        Assert.That(a, Is.Not.EqualTo(b));
    }

    /// <summary>
    ///     Keys and groups come from the source language files and are ordered by key.
    /// </summary>
    [Test]
    public void Build_MapsKeysToGroups()
    {
        var snapshot = TranslationCatalog.Build(SampleFiles());

        Assert.That(snapshot.Strings, Is.EqualTo(new[]
        {
            new CatalogString("ok", "_common", "Done"),
            new CatalogString("xp_level", "xp", "Level {0}"),
            new CatalogString("xp_reset", "xp", "Reset")
        }));
    }

    /// <summary>
    ///     Baselines cover the other locales only, and only for keys the source language has.
    /// </summary>
    [Test]
    public void Build_BaselinesSkipSourceLocaleAndUnknownKeys()
    {
        var snapshot = TranslationCatalog.Build(SampleFiles());

        Assert.That(snapshot.Baselines, Is.EqualTo(new[]
        {
            new CatalogBaseline("xp_level", "de-DE", "Stufe {0}"),
            new CatalogBaseline("xp_level", "fr-FR", "Niveau {0}")
        }));
    }

    /// <summary>
    ///     A key in two files of the source language takes its group and text from the file whose name sorts last.
    /// </summary>
    [Test]
    public void Build_DuplicateKey_LastFileByNameWins()
    {
        var snapshot = TranslationCatalog.Build(
        [
            File("en-US", "zeta", ("k", "from zeta")),
            File("en-US", "alpha", ("k", "from alpha")),
            File("de-DE", "zeta", ("k", "de zeta")),
            File("de-DE", "alpha", ("k", "de alpha"))
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Strings, Is.EqualTo(new[] { new CatalogString("k", "zeta", "from zeta") }));
            Assert.That(snapshot.Baselines, Is.EqualTo(new[] { new CatalogBaseline("k", "de-DE", "de zeta") }));
        });
    }

    /// <summary>
    ///     The catalog agrees with the merge the loader does for the bot's own lookups.
    /// </summary>
    [Test]
    public void Build_DuplicateKey_AgreesWithLoaderMerge()
    {
        var files = new List<ResponseFile>
        {
            File("en-US", "alpha", ("k", "from alpha")),
            File("en-US", "zeta", ("k", "from zeta"))
        };

        var merged = LocalFileStringsSource.MergeResponseFiles(files);
        var snapshot = TranslationCatalog.Build(files);

        Assert.That(snapshot.Strings.Single().SourceText, Is.EqualTo(merged["en-US"]["k"]));
    }

    /// <summary>
    ///     Without source language files there is nothing to register.
    /// </summary>
    [Test]
    public void Build_WithoutSourceLocale_IsEmpty()
    {
        var snapshot = TranslationCatalog.Build([File("de-DE", "xp", ("k", "v"))]);

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Strings, Is.Empty);
            Assert.That(snapshot.Baselines, Is.Empty);
        });
    }

    /// <summary>
    ///     The file reader orders files by name and names the group after the file.
    /// </summary>
    [Test]
    public void LocalFileStringsSource_ReadsGroupsInOrdinalOrder()
    {
        var root = Path.Combine(Path.GetTempPath(), "mewdeko-tr-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "en-US"));
            Directory.CreateDirectory(Path.Combine(root, "de-DE"));
            System.IO.File.WriteAllText(Path.Combine(root, "en-US", "b.json"), """{"k": "from b", "only_b": "x"}""");
            System.IO.File.WriteAllText(Path.Combine(root, "en-US", "a.json"), """{"k": "from a", "n": 0}""");
            System.IO.File.WriteAllText(Path.Combine(root, "de-DE", "a.json"), """{"k": "de a"}""");

            var source = new LocalFileStringsSource(root, root);
            var files = source.GetResponseFiles();
            var merged = source.GetResponseStrings();

            Assert.Multiple(() =>
            {
                Assert.That(files.Select(x => $"{x.Locale}/{x.Group}"),
                    Is.EqualTo(new[] { "de-DE/a", "en-US/a", "en-US/b" }));
                Assert.That(files[1].Strings.ContainsKey("n"), Is.False);
                Assert.That(merged["en-US"]["k"], Is.EqualTo("from b"));
                Assert.That(merged["de-DE"]["k"], Is.EqualTo("de a"));
            });
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static Dictionary<string, Dictionary<string, string>> Loaded()
    {
        return new Dictionary<string, Dictionary<string, string>>
        {
            ["en-US"] = new() { ["a"] = "A", ["b"] = "B" },
            ["de-DE"] = new() { ["a"] = "A de" },
            ["fr-FR"] = new()
        };
    }

    /// <summary>
    ///     An override replaces the file text, and also fills a key the locale's files lack.
    /// </summary>
    [Test]
    public void Apply_ReplacesAndFillsMissingKeys()
    {
        var loaded = Loaded();

        var count = TranslationOverrides.Apply(loaded,
        [
            new TranslationOverride("de-DE", "a", "A neu"),
            new TranslationOverride("de-DE", "b", "B neu")
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(count, Is.EqualTo(2));
            Assert.That(loaded["de-DE"]["a"], Is.EqualTo("A neu"));
            Assert.That(loaded["de-DE"]["b"], Is.EqualTo("B neu"));
            Assert.That(loaded["en-US"]["a"], Is.EqualTo("A"));
        });
    }

    /// <summary>
    ///     Rows for the source language, unknown locales, removed keys and blank text are ignored.
    /// </summary>
    [Test]
    public void Resolve_IgnoresRowsThatCannotApply()
    {
        var resolved = TranslationOverrides.Resolve(Loaded(),
        [
            new TranslationOverride("en-US", "a", "changed source"),
            new TranslationOverride("xx-XX", "a", "unknown locale"),
            new TranslationOverride("de-DE", "removed", "key gone from the files"),
            new TranslationOverride("de-DE", "a", "   "),
            new TranslationOverride("fr-FR", "a", "A fr")
        ]);

        Assert.That(resolved.Keys, Is.EqualTo(new[] { "fr-FR" }));
        Assert.That(resolved["fr-FR"]["a"], Is.EqualTo("A fr"));
    }

    /// <summary>
    ///     With no source language loaded nothing is applied.
    /// </summary>
    [Test]
    public void Resolve_WithoutSourceLocale_IsEmpty()
    {
        var loaded = new Dictionary<string, Dictionary<string, string>> { ["de-DE"] = new() { ["a"] = "A" } };

        Assert.That(TranslationOverrides.Resolve(loaded, [new TranslationOverride("de-DE", "a", "x")]), Is.Empty);
    }

    /// <summary>
    ///     When two rows name the same string the later one wins, and applying twice gives the same result.
    /// </summary>
    [Test]
    public void Apply_IsRepeatable()
    {
        var rows = new[] { new TranslationOverride("de-DE", "a", "one"), new TranslationOverride("de-DE", "a", "two") };
        var loaded = Loaded();

        TranslationOverrides.Apply(loaded, rows);
        TranslationOverrides.Apply(loaded, rows);

        Assert.That(loaded["de-DE"]["a"], Is.EqualTo("two"));
    }

    /// <summary>
    ///     Decides when the overrides have to be applied again.
    /// </summary>
    [TestCase(null, null, false)]
    [TestCase(null, 0L, true)]
    [TestCase(3L, 3L, false)]
    [TestCase(3L, 4L, true)]
    [TestCase(4L, 3L, true)]
    [TestCase(3L, null, false)]
    public void VersionChanged_Decision(long? applied, long? current, bool expected)
    {
        Assert.That(TranslationOverrides.VersionChanged(applied, current), Is.EqualTo(expected));
    }

    /// <summary>
    ///     The statements carry text as parameters, never inline.
    /// </summary>
    [Test]
    public void StoreSql_UsesParametersOnly()
    {
        var strings = new[] { new CatalogString("key'; DROP TABLE x;--", "g", "text with 'quotes'") };
        var baselines = new[] { new CatalogBaseline("k", "de-DE", "t'") };

        var upsert = PostgresTranslationStore.BuildStringsUpsert("bot", strings);
        var delete = PostgresTranslationStore.BuildStringsDelete("bot", ["old'key"]);
        var insert = PostgresTranslationStore.BuildBaselinesInsert("bot", baselines);

        Assert.Multiple(() =>
        {
            Assert.That(upsert.Sql, Does.Not.Contain("DROP").And.Not.Contain("quotes"));
            Assert.That(upsert.Parameters.Select(x => x.Name), Is.EqualTo(new[] { "catalog", "k0", "g0", "s0" }));
            Assert.That(delete.Sql, Does.Not.Contain("old"));
            Assert.That(insert.Sql, Does.Not.Contain("de-DE"));
            Assert.That(insert.Parameters, Has.Length.EqualTo(4));
        });
    }

    /// <summary>
    ///     Stored keys that are no longer in the catalog are the ones to delete.
    /// </summary>
    [Test]
    public void RemovedKeys_ListsStoredKeysNotInCatalog()
    {
        var removed = PostgresTranslationStore.RemovedKeys(["a", "b", "c"],
            [new CatalogString("b", "g", "x"), new CatalogString("d", "g", "y")]);

        Assert.That(removed, Is.EqualTo(new[] { "a", "c" }));
    }

    private static PostgresException MissingTable()
    {
        return new PostgresException("relation does not exist", "ERROR", "ERROR", PostgresErrorCodes.UndefinedTable);
    }

    /// <summary>
    ///     A missing table is recognised, also when wrapped, and other errors are not.
    /// </summary>
    [Test]
    public void IsMissingTables_RecognisesUndefinedTable()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TranslationCatalogRegistrar.IsMissingTables(MissingTable()), Is.True);
            Assert.That(TranslationCatalogRegistrar.IsMissingTables(new InvalidOperationException("x", MissingTable())),
                Is.True);
            Assert.That(TranslationCatalogRegistrar.IsMissingTables(new InvalidOperationException("x")), Is.False);
            Assert.That(TranslationCatalogRegistrar.IsMissingTables(null), Is.False);
        });
    }

    /// <summary>
    ///     With the tables missing, registration reports it, writes nothing and does not throw.
    /// </summary>
    [Test]
    public async Task Register_MissingTables_ReturnsWithoutWriting()
    {
        var store = new FakeStore { HashFailure = MissingTable() };
        var registrar = new TranslationCatalogRegistrar(store, new CapturingLogger());

        var result = await registrar.RegisterAsync(SampleFiles());

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(RegistrationResult.MissingTables));
            Assert.That(store.Replaced, Is.Empty);
        });
    }

    /// <summary>
    ///     A matching stored hash means nothing is written.
    /// </summary>
    [Test]
    public async Task Register_SameHash_Skips()
    {
        var store = new FakeStore { Hash = TranslationCatalog.Build(SampleFiles()).Hash };
        var registrar = new TranslationCatalogRegistrar(store, new CapturingLogger());

        var result = await registrar.RegisterAsync(SampleFiles());

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(RegistrationResult.Skipped));
            Assert.That(store.Replaced, Is.Empty);
        });
    }

    /// <summary>
    ///     A different or missing stored hash means the catalog is written once with the new hash.
    /// </summary>
    [TestCase(null)]
    [TestCase("stale")]
    public async Task Register_DifferentHash_Writes(string? stored)
    {
        var store = new FakeStore { Hash = stored };
        var registrar = new TranslationCatalogRegistrar(store, new CapturingLogger());

        var result = await registrar.RegisterAsync(SampleFiles());

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(RegistrationResult.Registered));
            Assert.That(store.Replaced, Has.Count.EqualTo(1));
            Assert.That(store.Replaced[0].Hash, Is.EqualTo(TranslationCatalog.Build(SampleFiles()).Hash));
        });
    }

    /// <summary>
    ///     A catalog without source strings is never written, so a broken data folder cannot wipe the tables.
    /// </summary>
    [Test]
    public async Task Register_NoSourceStrings_Skips()
    {
        var store = new FakeStore();
        var registrar = new TranslationCatalogRegistrar(store, new CapturingLogger());

        var result = await registrar.RegisterAsync([File("de-DE", "xp", ("k", "v"))]);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(RegistrationResult.Skipped));
            Assert.That(store.Replaced, Is.Empty);
        });
    }

    /// <summary>
    ///     Other failures are logged and reported, not thrown.
    /// </summary>
    [Test]
    public async Task Register_OtherFailure_IsLoggedNotThrown()
    {
        var logger = new CapturingLogger();
        var store = new FakeStore { HashFailure = new InvalidOperationException("boom") };
        var registrar = new TranslationCatalogRegistrar(store, logger);

        var result = await registrar.RegisterAsync(SampleFiles());

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(RegistrationResult.Failed));
            Assert.That(logger.Entries.Select(x => x.Level), Is.EqualTo(new[] { LogLevel.Warning }));
        });
    }

    /// <summary>
    ///     The missing-tables notice is logged once at information level.
    /// </summary>
    [Test]
    public void MissingTablesNotice_LogsOnce()
    {
        var logger = new CapturingLogger();
        var notice = new MissingTablesNotice(logger);

        notice.Report();
        notice.Report();
        notice.Report();

        Assert.That(logger.Entries.Select(x => x.Level), Is.EqualTo(new[] { LogLevel.Information }));
    }

    private sealed class FakeStore : ITranslationStore
    {
        public string? Hash { get; init; }
        public Exception? HashFailure { get; init; }
        public List<CatalogSnapshot> Replaced { get; } = [];

        public Task<string?> GetBuildHashAsync(string catalog, System.Threading.CancellationToken cancellationToken = default)
        {
            return HashFailure is null ? Task.FromResult(Hash) : Task.FromException<string?>(HashFailure);
        }

        public Task ReplaceCatalogAsync(string catalog, CatalogSnapshot snapshot,
            System.Threading.CancellationToken cancellationToken = default)
        {
            Replaced.Add(snapshot);
            return Task.CompletedTask;
        }

        public Task<long?> GetVersionAsync(string catalog, System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.FromResult<long?>(null);
        }

        public Task<IReadOnlyList<TranslationOverride>> GetOverridesAsync(string catalog,
            System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<TranslationOverride>>([]);
        }
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
