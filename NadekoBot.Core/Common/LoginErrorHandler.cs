using System;
using System.Net;
using System.Runtime.CompilerServices;
using Discord.Net;
using NLog;

namespace NadekoBot.Core.Common
{
    public class LoginErrorHandler
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Handle(Logger log, Exception ex)
        {
            log.Warn("A fatal error has occurred while attempting to connect to Discord.");
            log.Fatal(ex.ToString());
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Handle(Logger log, HttpException ex)
        {
            switch (ex.HttpCode)
            {
                case HttpStatusCode.Unauthorized:
                    log.Error("Your bot token is wrong.\n" +
                        "You can find the bot token under the Bot tab in the developer page.\n" +
                        "Fix your token in the credentials file and restart the bot.");
                    break;

                case HttpStatusCode.BadRequest:
                    log.Error("Something has been incorrectly formatted in your credentials file.\n" +
                        "Use the JSON Guide as reference to fix it and restart the bot.");
                    log.Error("If you are on Linux, make sure Redis is installed and running.");
                    break;

                case HttpStatusCode.RequestTimeout:
                    log.Error("The request timed out. Make sure you have no external program blocking the bot " +
                        "from connecting to the internet.");
                    break;

                case HttpStatusCode.ServiceUnavailable:
                case HttpStatusCode.InternalServerError:
                    log.Error("Discord is having internal issues. Please, try again later.");
                    break;

                case HttpStatusCode.TooManyRequests:
                    log.Error("Your bot has been ratelimited by Discord. Please, try again later.\n" +
                        "Global ratelimits usually last for an hour.");
                    break;

                default:
                    log.Warn("An error occurred while attempting to connect to Discord.");
                    break;
            }

            log.Fatal(ex.ToString());
        }
    }
}