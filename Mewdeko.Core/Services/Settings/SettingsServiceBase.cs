using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Mewdeko.Core.Common;
using Mewdeko.Core.Common.Configs;

namespace Mewdeko.Core.Services
{
    /// <summary>
    /// Base service for all settings services
    /// </summary>
    /// <typeparam name="TSettings">Type of the settings</typeparam>
    public abstract class SettingsServiceBase<TSettings> where TSettings : new()
    {
        protected readonly string _filePath;
        protected readonly ISettingsSeria _serializer;
        protected readonly IPubSub _pubSub;
        private readonly TypedKey<TSettings> _changeKey;

        protected TSettings _data;
        public TSettings Data => CreateCopy();

        /// <summary>
        /// Initialized an instance of <see cref="SettingsServiceBase{TSettings}"/>
        /// </summary>
        /// <param name="filePath">Path to the file where the settings are serialized/deserialized to and from</param>
        /// <param name="serializer">Serializer which will be used</param>
        /// <param name="pubSub">Pubsub implementation for signaling when settings are updated</param>
        /// <param name="changeKey">Key used to signal changed event</param>
        protected SettingsServiceBase(string filePath, ISettingsSeria serializer, IPubSub pubSub,
            TypedKey<TSettings> changeKey)
        {
            _filePath = filePath;
            _serializer = serializer;
            _pubSub = pubSub;
            _changeKey = changeKey;

            _pubSub.Sub(_changeKey, OnChangePublished);

            Load();
        }

        private void PublishChange()
        {
            _pubSub.Pub(_changeKey, _data);
        }
        
        private Task OnChangePublished(TSettings newData)
        {
            _data = newData;
            OnStateUpdate();
            return Task.CompletedTask;
        }

        private TSettings CreateCopy()
        {
            var serializedData = _serializer.Serialize(_data);
            return _serializer.Deserialize<TSettings>(serializedData);
        }

        /// <summary>
        /// Loads data from disk. If file doesn't exist, it will be created with default values
        /// </summary>
        private void Load()
        {
            // if file is deleted, regenerate it with default values
            if (!File.Exists(_filePath))
            {
                _data = new TSettings();
                Save();
            }

            _data = _serializer.Deserialize<TSettings>(File.ReadAllText(_filePath));
        }
        
        /// <summary>
        /// Loads new data and publishes the new state
        /// </summary>
        public void Reload()
        {
            Load();
            _pubSub.Pub(_changeKey, _data);
        }

        /// <summary>
        /// Doesn't do anything by default. This method will be executed after
        /// <see cref="_data"/> is reloaded from <see cref="_filePath"/> or new data is recieved
        /// from the publish event
        /// </summary>
        protected virtual void OnStateUpdate()
        {
            
        }
        
        public void ModifyConfig(Action<TSettings> action)
        {
            var copy = CreateCopy();
            action(copy);
            _data = copy;
            Save();
            PublishChange();
        }

        private void Save()
        {
            var strData = _serializer.Serialize(_data);
            File.WriteAllText(_filePath, strData);
        }
        
        private readonly Dictionary<string, Func<TSettings, string, bool>> _propSetters = new Dictionary<string, Func<TSettings, string, bool>>();
        private readonly Dictionary<string, Func<object>> _propSelectors = new Dictionary<string, Func<object>>();
        private readonly Dictionary<string, Func<object, string>> _propPrinters = new Dictionary<string, Func<object, string>>();
        
        protected void AddParsedProp<TProp>(
            string key,
            Expression<Func<TSettings, TProp>> selector,
            SettingParser<TProp> parser,
            Func<TProp, string> printer)
        {
            key = key.ToLowerInvariant();
            _propPrinters[key] = obj => printer((TProp)obj); 
            _propSelectors[key] = () => selector.Compile()(_data);
            _propSetters[key] = Magic(selector, parser);
        }

        private Func<TSettings, string, bool> Magic<TProp>(Expression<Func<TSettings, TProp>> selector,
            SettingParser<TProp> parser)
            => (target, key) =>
            {
                if (!parser(key, out var value))
                    return false;

                var expr = (MemberExpression)selector.Body;
                var prop = (PropertyInfo) expr.Member;
                object targetObject = target;

                // idk what i did but it works
                while (true)
                {
                    expr = expr.Expression as MemberExpression;
                    if (expr is null)
                        break;
                    var localProp = (PropertyInfo) expr.Member;
                    targetObject = localProp.GetValue(targetObject, null);
                }
                
                prop.SetValue(targetObject, value, null);
                return true;
            };

        public IReadOnlyList<string> GetSettableProps()
            => _propSetters.Keys.ToList();

        public string GetSetting(string key)
        {
            key = key.ToLowerInvariant();
            if (!_propSelectors.TryGetValue(key, out var selector) ||
                !_propPrinters.TryGetValue(key, out var printer))
                return default;

            return printer(selector());
        }
        
        private bool SetProperty(TSettings target, string key, string value)
            => _propSetters.TryGetValue(key.ToLowerInvariant(), out var magic) && magic(target, value);

        public bool SetSetting(string key, string newValue)
        {
            var success = true;
            ModifyConfig(bs =>
            {
                success = SetProperty(bs, key, newValue);
            });
            
            if(success)
                PublishChange();

            return success;
        }
    }
}