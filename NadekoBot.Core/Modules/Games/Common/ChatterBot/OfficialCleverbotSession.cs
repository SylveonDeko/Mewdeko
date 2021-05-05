using NadekoBot.Common;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace NadekoBot.Modules.Games.Common.ChatterBot
{
    public class OfficialCleverbotSession : IChatterBotSession
    {
        private readonly string _apiKey;
        private readonly IHttpClientFactory _httpFactory;
        private readonly Logger _log;
        private string _cs = null;

        private string QueryString => $"https://www.cleverbot.com/getreply?key={_apiKey}" +
            "&wrapper=nadekobot" +
            "&input={0}" +
            "&cs={1}";

        public OfficialCleverbotSession(string apiKey, IHttpClientFactory factory)
        {
            this._apiKey = apiKey;
            this._httpFactory = factory;
            this._log = LogManager.GetCurrentClassLogger();
        }

        public async Task<string> Think(string input)
        {
            using (var http = _httpFactory.CreateClient())
            {
                var dataString = await http.GetStringAsync(string.Format(QueryString, input, _cs ?? "")).ConfigureAwait(false);
                try
                {
                    var data = JsonConvert.DeserializeObject<CleverbotResponse>(dataString);

                    _cs = data?.Cs;
                    return data?.Output;
                }
                catch
                {
                    _log.Warn("Unexpected cleverbot response received: ");
                    _log.Warn(dataString);
                    return null;
                }
            }
        }
    }

    public class CleverbotIOSession : IChatterBotSession
    {
        private readonly string _key;
        private readonly string _user;
        private readonly IHttpClientFactory _httpFactory;
        private readonly AsyncLazy<string> _nick;

        private readonly string _createEndpoint = $"https://cleverbot.io/1.0/create";
        private readonly string _askEndpoint = $"https://cleverbot.io/1.0/ask";

        public CleverbotIOSession(string user, string key, IHttpClientFactory factory)
        {
            this._key = key;
            this._user = user;
            this._httpFactory = factory;

            _nick = new AsyncLazy<string>((Func<Task<string>>)GetNick);
        }

        private async Task<string> GetNick()
        {
            using (var _http = _httpFactory.CreateClient())
            using (var msg = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("user", _user),
                new KeyValuePair<string, string>("key", _key),
            }))
            using (var data = await _http.PostAsync(_createEndpoint, msg).ConfigureAwait(false))
            {
                var str = await data.Content.ReadAsStringAsync().ConfigureAwait(false);
                var obj = JsonConvert.DeserializeObject<CleverbotIOCreateResponse>(str);
                if (obj.Status != "success")
                    throw new OperationCanceledException(obj.Status);

                return obj.Nick;
            }
        }

        public async Task<string> Think(string input)
        {
            using (var _http = _httpFactory.CreateClient())
            using (var msg = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("user", _user),
                new KeyValuePair<string, string>("key", _key),
                new KeyValuePair<string, string>("nick", await _nick),
                new KeyValuePair<string, string>("text", input),
            }))
            using (var data = await _http.PostAsync(_askEndpoint, msg).ConfigureAwait(false))
            {
                var str = await data.Content.ReadAsStringAsync().ConfigureAwait(false);
                var obj = JsonConvert.DeserializeObject<CleverbotIOAskResponse>(str);
                if (obj.Status != "success")
                    throw new OperationCanceledException(obj.Status);

                return obj.Response;
            }
        }
    }
}
