using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using Mewdeko.Common.Configs;
using Mewdeko.Common.PubSub;
using Mewdeko.Common.Yml;

namespace Mewdeko.Services.Settings
{
    /// <summary>
    /// Base service for all settings services.
    /// </summary>
    /// <typeparam name="TSettings">Type of the settings.</typeparam>
    public abstract class ConfigServiceBase<TSettings> : IConfigService
        where TSettings : new()
    {
        /// <summary>
        /// The change key used for signaling when settings are updated.
        /// </summary>
        private readonly TypedKey<TSettings> changeKey;

        /// <summary>
        /// Dictionary to store property comments.
        /// </summary>
        private readonly Dictionary<string, string> propComments = new();

        /// <summary>
        /// Dictionary to store property printers.
        /// </summary>
        private readonly Dictionary<string, Func<object, string>> propPrinters = new();

        /// <summary>
        /// Dictionary to store property selectors.
        /// </summary>
        private readonly Dictionary<string, Func<object>> propSelectors = new();

        /// <summary>
        /// Dictionary to store property setters.
        /// </summary>
        private readonly Dictionary<string, Func<TSettings, string, bool>> propSetters = new();

        /// <summary>
        /// The PubSub implementation for signaling when settings are updated.
        /// </summary>
        protected readonly IPubSub PubSub;

        /// <summary>
        /// The serializer used for serialization/deserialization of settings.
        /// </summary>
        protected readonly IConfigSeria Serializer;

        /// <summary>
        /// The path to the file where the settings are serialized/deserialized.
        /// </summary>
        protected readonly string FilePath;

        /// <summary>
        /// The settings data.
        /// </summary>
        protected TSettings data;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigServiceBase{TSettings}"/> class.
        /// </summary>
        /// <param name="filePath">The path to the file where the settings are serialized/deserialized to and from.</param>
        /// <param name="serializer">The serializer which will be used.</param>
        /// <param name="pubSub">The Pubsub implementation for signaling when settings are updated.</param>
        /// <param name="changeKey">The key used to signal changed event.</param>
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

        /// <summary>
        /// Gets the current settings data.
        /// </summary>
        public TSettings Data => CreateCopy1();

        /// <summary>
        /// Gets the name of the settings service.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Reloads the settings.
        /// </summary>
        public void Reload()
        {
            Load();
            PubSub.Pub(changeKey, data);
        }

        /// <summary>
        /// Gets the list of settable properties.
        /// </summary>
        /// <returns>The list of settable properties.</returns>
        public IReadOnlyList<string> GetSettableProps() => propSetters.Keys.ToList();

        /// <summary>
        /// Gets the value of a setting.
        /// </summary>
        /// <param name="prop">The property name.</param>
        /// <returns>The value of the setting.</returns>
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

        /// <summary>
        /// Gets the comment associated with a property.
        /// </summary>
        /// <param name="prop">The property name.</param>
        /// <returns>The comment associated with the property.</returns>
        public string? GetComment(string prop) => propComments.TryGetValue(prop, out var comment) ? comment : null;

        /// <summary>
        /// Sets the value of a setting.
        /// </summary>
        /// <param name="prop">The property name.</param>
        /// <param name="newValue">The new value.</param>
        /// <returns>True if the setting was successfully set, otherwise false.</returns>
        public bool SetSetting(string prop, string newValue)
        {
            var success = true;
            ModifyConfig(bs => success = SetProperty(bs, prop, newValue));

            if (success)
                PublishChange();

            return success;
        }

        /// <summary>
        /// Publishes a change in settings.
        /// </summary>
        private void PublishChange() => PubSub.Pub(changeKey, data);

        /// <summary>
        /// Handles the event when settings are published.
        /// </summary>
        /// <param name="newData">The new settings data.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        private ValueTask OnChangePublished(TSettings newData)
        {
            data = newData;
            OnStateUpdate();
            return default;
        }

        /// <summary>
        /// Creates a copy of the settings data.
        /// </summary>
        /// <returns>A copy of the settings data.</returns>
        private TSettings CreateCopy1()
        {
            var serializedData = Serializer.Serialize(data);
            return Serializer.Deserialize<TSettings>(serializedData);
        }

        /// <summary>
        /// Loads the settings from the file.
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
        /// Method that is executed after the settings data is updated.
        /// </summary>
        protected virtual void OnStateUpdate()
        {
        }

        /// <summary>
        /// Modifies the settings configuration.
        /// </summary>
        /// <param name="action">The action to modify the settings.</param>
        public void ModifyConfig(Action<TSettings
        > action)
        {
            var copy = CreateCopy1();
            action(copy);
            data = copy;
            Save();
            PublishChange();
        }

        /// <summary>
        /// Saves the settings to the file.
        /// </summary>
        private void Save()
        {
            var strData = Serializer.Serialize(data);
            File.WriteAllText(FilePath, strData);
        }

        /// <summary>
        /// Adds a parsed property to the settings.
        /// </summary>
        /// <typeparam name="TProp">The type of the property.</typeparam>
        /// <param name="key">The key of the property.</param>
        /// <param name="selector">The selector expression for the property.</param>
        /// <param name="parser">The parser for the property.</param>
        /// <param name="printer">The printer for the property.</param>
        /// <param name="checker">An optional checker function for the property.</param>
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

        /// <summary>
        /// The magic method to set a property value using an expression tree.
        /// </summary>
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

        /// <summary>
        /// Sets a property value.
        /// </summary>
        private bool SetProperty(TSettings target, string key, string value) =>
            propSetters.TryGetValue(key.ToLowerInvariant(), out var magic)
            && magic(target, value);
    }
}