using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Mewdeko.Extensions;
using Serilog;

namespace Mewdeko.Core.Services.Impl
{
    public class YtdlOperation
    {
        private readonly string _baseArgString;

        public YtdlOperation(string baseArgString)
        {
            _baseArgString = baseArgString;
        }

        private Process CreateProcess(string[] args)
        {
            args = args.Map(arg => arg.Replace("\"", ""));
            return new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = @"youtube-dl",
                    Arguments = string.Format(_baseArgString, args),
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    CreateNoWindow = true
                }
            };
        }

        public async Task<string> GetDataAsync(params string[] args)
        {
            using var process = CreateProcess(args);

            Log.Debug($"Executing {process.StartInfo.FileName} {process.StartInfo.Arguments}");
            process.Start();

            var str = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            var err = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
            if (!string.IsNullOrEmpty(err))
                Log.Warning("YTDL warning: {YtdlWarning}", err);

            return str;
        }

        public async IAsyncEnumerable<string> EnumerateDataAsync(params string[] args)
        {
            using var process = CreateProcess(args);

            Log.Debug($"Executing {process.StartInfo.FileName} {process.StartInfo.Arguments}");
            process.Start();

            string line;
            while ((line = await process.StandardOutput.ReadLineAsync()) != null)
                yield return line;
        }
    }
}