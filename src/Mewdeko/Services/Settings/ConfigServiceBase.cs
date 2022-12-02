using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Mewdeko.Common.Configs;
using Mewdeko.Common.PubSub;
using Mewdeko.Common.Yml;

namespace Mewdeko.Services.Settings;

/// <summary>
///     Base service for all settings services
/// </summary>
/// <typeparam name="TSettings">Type of the settings</typeparam>
public abstract class ConfigServiceBase<TSettings> : IConfigService
    where TSettings : new()
{
    private readonly TypedKey<TSettings> changeKey;
    private readonly Dictionary<string, string> propComments = new();
    private readonly Dictionary<string, Func<object, string>> propPrinters = new();
    private readonly Dictionary<string, Func<object>> propSelectors = new();

    private readonly Dictionary<string, Func<TSettings, string, bool>> propSetters = new();
    protected readonly IPubSub PubSub;
    protected readonly IConfigSeria Serializer;
    protected readonly string FilePath;

    protected TSettings data;

    /// <summary>
    ///     Initialized an instance of <see cref="ConfigServiceBase{TSettings}" />
    /// </summary>
    /// <param name="filePath">Path to the file where the settings are serialized/deserialized to and from</param>
    /// <param name="serializer">Serializer which will be used</param>
    /// <param name="pubSub">Pubsub implementation for signaling when settings are updated</param>
    /// <param name="changeKey">Key used to signal changed event</param>
    protected ConfigServiceBase(string filePath, IConfigSeria serializer, IPubSub pubSub,
        TypedKey<TSettings> changeKey)
    {
        FilePath = filePath;
        Serializer = serializer;
        PubSub = pubSub;
        this.changeKey = changeKey;

        Load();
        PubSub.Sub(this.changeKey, OnChangePublished);
    }

    public TSettings Data => CreateCopy1();

    public abstract string Name { get; }

    /// <summary>
    ///     Loads new data and publishes the new state
    /// </summary>
    public void Reload()
    {
        Load();
        PubSub.Pub(changeKey, data);
    }

    public IReadOnlyList<string> GetSettableProps() => propSetters.Keys.ToList();

    public string? GetSetting(string prop)
    {
        prop = prop.ToLowerInvariant();
        if (!propSelectors.TryGetValue(prop, out var selector) ||
            !propPrinters.TryGetValue(prop, out var printer))
        {
            return default;
        }

        return printer(selector());
    }

    public string? GetComment(string prop) => propComments.TryGetValue(prop, out var comment) ? comment : null;

    public bool SetSetting(string prop, string newValue)
    {
        var success = true;
        ModifyConfig(bs => success = SetProperty(bs, prop, newValue));

        if (success)
            PublishChange();

        return success;
    }

    private void PublishChange() => PubSub.Pub(changeKey, data);

    private ValueTask OnChangePublished(TSettings newData)
    {
        data = newData;
        OnStateUpdate();
        return default;
    }

    private TSettings CreateCopy1()
    {
        var serializedData = Serializer.Serialize(data);
        return Serializer.Deserialize<TSettings>(serializedData);
    }

    /// <summary>
    ///     Loads data from disk. If file doesn't exist, it will be created with default values
    /// </summary>
    private void Load()
    {
        // if file is deleted, regenerate it with default values
        if (!File.Exists(FilePath))
        {
            data = new TSettings();
            Save();
        }

        data = Serializer.Deserialize<TSettings>(File.ReadAllText(FilePath));
    }

    /// <summary>
    ///     Doesn't do anything by default. This method will be executed after
    ///     <see cref="data" /> is reloaded from <see cref="FilePath" /> or new data is recieved
    ///     from the publish event
    /// </summary>
    protected virtual void OnStateUpdate()
    {
    }

    public void ModifyConfig(Action<TSettings> action)
    {
        var copy = CreateCopy1();
        action(copy);
        data = copy;
        Save();
        PublishChange();
    }

    private void Save()
    {
        var strData = Serializer.Serialize(data);
        File.WriteAllText(FilePath, strData);
    }

    protected void AddParsedProp<TProp>(
        string key,
        Expression<Func<TSettings, TProp>> selector,
        SettingParser<TProp> parser,
        Func<TProp, string> printer,
        Func<TProp, bool>? checker = null)
    {
        checker ??= _ => true;
        key = key.ToLowerInvariant();
        propPrinters[key] = obj => printer((TProp)obj);
        propSelectors[key] = () => selector.Compile()(data);
        propSetters[key] = Magic(selector, parser, checker);
        propComments[key] = ((MemberExpression)selector.Body).Member.GetCustomAttribute<CommentAttribute>()
            ?.Comment;
    }

    private static Func<TSettings, string, bool> Magic<TProp>(Expression<Func<TSettings, TProp>> selector,
        SettingParser<TProp> parser, Func<TProp, bool> checker) =>
        (target, input) =>
        {
            if (!parser(input, out var value))
                return false;

            if (!checker(value))
                return false;

            object targetObject = target;
            var expr = (MemberExpression)selector.Body;
            var prop = (PropertyInfo)expr.Member;

            var expressions = new List<MemberExpression>();

            while (true)
            {
                expr = expr.Expression as MemberExpression;
                if (expr is null)
                    break;

                expressions.Add(expr);
            }

            foreach (var memberExpression in expressions.AsEnumerable().Reverse())
            {
                var localProp = (PropertyInfo)memberExpression.Member;
                targetObject = localProp.GetValue(targetObject);
            }

            prop.SetValue(targetObject, value, null);
            return true;
        };

    private bool SetProperty(TSettings target, string key, string value) =>
        propSetters.TryGetValue(key.ToLowerInvariant(), out var magic)
        && magic(target, value);
}