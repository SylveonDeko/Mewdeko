using Mewdeko.Common.Configs;
using Mewdeko.Common.PubSub;
using Mewdeko.Common.Yml;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using CultureInfoConverter = Mewdeko.Common.JsonConverters.CultureInfoConverter;
using Rgba32Converter = Mewdeko.Common.JsonConverters.Rgba32Converter;

namespace Mewdeko.Services.Settings;

/// <summary>
///     Base service for all settings services
/// </summary>
/// <typeparam name="TSettings">Type of the settings</typeparam>
public abstract class ConfigServiceBase<TSettings> : IConfigService
    where TSettings : new()
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        MaxDepth = 0,
        Converters = { new Rgba32Converter(), new CultureInfoConverter() }
    };

    private readonly TypedKey<TSettings> _changeKey;
    private readonly Dictionary<string, string> _propComments = new();
    private readonly Dictionary<string, Func<object, string>> _propPrinters = new();
    private readonly Dictionary<string, Func<object>> _propSelectors = new();

    private readonly Dictionary<string, Func<TSettings, string, bool>> _propSetters = new();
    protected readonly IPubSub _pubSub;
    protected readonly IConfigSeria _serializer;
    protected readonly string _filePath;

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
        _filePath = filePath;
        _serializer = serializer;
        _pubSub = pubSub;
        _changeKey = changeKey;

        Load();
        _pubSub.Sub(_changeKey, OnChangePublished);
    }

    public TSettings Data => CreateCopy1();

    public abstract string Name { get; }

    /// <summary>
    ///     Loads new data and publishes the new state
    /// </summary>
    public void Reload()
    {
        Load();
        _pubSub.Pub(_changeKey, data);
    }

    public IReadOnlyList<string> GetSettableProps() => _propSetters.Keys.ToList();

    public string GetSetting(string prop)
    {
        prop = prop.ToLowerInvariant();
        if (!_propSelectors.TryGetValue(prop, out var selector) ||
            !_propPrinters.TryGetValue(prop, out var printer))
        {
            return default;
        }

        return printer(selector());
    }

    public string GetComment(string prop)
    {
        if (_propComments.TryGetValue(prop, out var comment))
            return comment;

        return null;
    }

    public bool SetSetting(string prop, string newValue)
    {
        var success = true;
        ModifyConfig(bs => success = SetProperty(bs, prop, newValue));

        if (success)
            PublishChange();

        return success;
    }

    private void PublishChange() => _pubSub.Pub(_changeKey, data);

    private ValueTask OnChangePublished(TSettings newData)
    {
        data = newData;
        OnStateUpdate();
        return default;
    }

    private TSettings CreateCopy1()
    {
        var serializedData = JsonSerializer.Serialize(data, _serializerOptions);
        return JsonSerializer.Deserialize<TSettings>(serializedData, _serializerOptions);

        // var serializedData = _serializer.Serialize(_data);
        //
        // return _serializer.Deserialize<TSettings>(serializedData);
    }

    /// <summary>
    ///     Loads data from disk. If file doesn't exist, it will be created with default values
    /// </summary>
    private void Load()
    {
        // if file is deleted, regenerate it with default values
        if (!File.Exists(_filePath))
        {
            data = new TSettings();
            Save();
        }

        data = _serializer.Deserialize<TSettings>(File.ReadAllText(_filePath));
    }

    /// <summary>
    ///     Doesn't do anything by default. This method will be executed after
    ///     <see cref="data" /> is reloaded from <see cref="_filePath" /> or new data is recieved
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
        var strData = _serializer.Serialize(data);
        File.WriteAllText(_filePath, strData);
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
        _propPrinters[key] = obj => printer((TProp)obj);
        _propSelectors[key] = () => selector.Compile()(data);
        _propSetters[key] = Magic(selector, parser, checker);
        _propComments[key] = ((MemberExpression)selector.Body).Member.GetCustomAttribute<CommentAttribute>()
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
        _propSetters.TryGetValue(key.ToLowerInvariant(), out var magic)
        && magic(target, value);
}