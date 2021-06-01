using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Mewdeko.Common;
using Mewdeko.Common.Collections;
using Mewdeko.Core.Services;
using Mewdeko.Modules.Administration.Services;
using Newtonsoft.Json;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp.Attributes;
using Mewdeko.Common.Attributes;
using Serilog;

namespace Mewdeko.Extensions
{
    public static class Extensions
    {
        public static Regex UrlRegex = new Regex(@"^(https?|ftp)://(?<path>[^\s/$.?#].[^\s]*)$", RegexOptions.Compiled);

        public static TOut[] Map<TIn, TOut>(this TIn[] arr, Func<TIn, TOut> f)
            => Array.ConvertAll(arr, x => f(x));

        public static Task<IUserMessage> EmbedAsync(this IMessageChannel channel, CREmbed crEmbed, bool sanitizeAll = false)
        {
            var plainText = sanitizeAll
                ? crEmbed.PlainText?.SanitizeAllMentions() ?? ""
                : crEmbed.PlainText?.SanitizeMentions() ?? "";
            
            return channel.SendMessageAsync(plainText, embed: crEmbed.IsEmbedValid ? crEmbed.ToEmbed().Build() : null);
        }

        public static List<ulong> GetGuildIds(this DiscordSocketClient client)
            => client.Guilds.Select(x => x.Id).ToList();

        /// <summary>
        /// Generates a string in the format HHH:mm if timespan is <= 2m.
        /// Generates a string in the format 00:mm:ss if timespan is less than 2m.
        /// </summary>
        /// <param name="span">Timespan to convert to string</param>
        /// <returns>Formatted duration string</returns>
        public static string ToPrettyStringHM(this TimeSpan span)
        {
            if (span < TimeSpan.FromMinutes(2))
                return $"{span:mm}m {span:ss}s";
            return $"{(int) span.TotalHours:D2}h {span:mm}m";
        }

        public static bool TryGetUrlPath(this string input, out string path)
        {
            var match = UrlRegex.Match(input);
            if (match.Success)
            {
                path = match.Groups["path"].Value;
                return true;
            }
            path = string.Empty;
            return false;
        }

        public static IEmote ToIEmote(this string emojiStr)
            => Emote.TryParse(emojiStr, out var maybeEmote)
                    ? (IEmote)maybeEmote
                    : new Emoji(emojiStr);

        // https://github.com/SixLabors/Samples/blob/master/ImageSharp/AvatarWithRoundedCorner/Program.cs
        public static IImageProcessingContext ApplyRoundedCorners(this IImageProcessingContext ctx, float cornerRadius)
        {
            Size size = ctx.GetCurrentSize();
            IPathCollection corners = BuildCorners(size.Width, size.Height, cornerRadius);

            ctx.SetGraphicsOptions(new GraphicsOptions()
            {
                Antialias = true,
                AlphaCompositionMode = PixelAlphaCompositionMode.DestOut // enforces that any part of this shape that has color is punched out of the background
            });

            foreach (var c in corners)
            {
                ctx = ctx.Fill(SixLabors.ImageSharp.Color.Red, c);
            }
            return ctx;
        }

        private static IPathCollection BuildCorners(int imageWidth, int imageHeight, float cornerRadius)
        {
            // first create a square
            var rect = new RectangularPolygon(-0.5f, -0.5f, cornerRadius, cornerRadius);

            // then cut out of the square a circle so we are left with a corner
            IPath cornerTopLeft = rect.Clip(new EllipsePolygon(cornerRadius - 0.5f, cornerRadius - 0.5f, cornerRadius));

            // corner is now a corner shape positions top left
            //lets make 3 more positioned correctly, we can do that by translating the original around the center of the image

            float rightPos = imageWidth - cornerTopLeft.Bounds.Width + 1;
            float bottomPos = imageHeight - cornerTopLeft.Bounds.Height + 1;

            // move it across the width of the image - the width of the shape
            IPath cornerTopRight = cornerTopLeft.RotateDegree(90).Translate(rightPos, 0);
            IPath cornerBottomLeft = cornerTopLeft.RotateDegree(-90).Translate(0, bottomPos);
            IPath cornerBottomRight = cornerTopLeft.RotateDegree(180).Translate(rightPos, bottomPos);

            return new PathCollection(cornerTopLeft, cornerBottomLeft, cornerTopRight, cornerBottomRight);
        }

        /// <summary>
        /// First 10 characters of teh bot token.
        /// </summary>
        public static string RedisKey(this IBotCredentials bc)
        {
            return bc.Token.Substring(0, 10);
        }

        public static async Task<string> ReplaceAsync(this Regex regex, string input, Func<Match, Task<string>> replacementFn)
        {
            var sb = new StringBuilder();
            var lastIndex = 0;

            foreach (Match match in regex.Matches(input))
            {
                sb.Append(input, lastIndex, match.Index - lastIndex)
                  .Append(await replacementFn(match).ConfigureAwait(false));

                lastIndex = match.Index + match.Length;
            }

            sb.Append(input, lastIndex, input.Length - lastIndex);
            return sb.ToString();
        }

        public static void ThrowIfNull<T>(this T o, string name) where T : class
        {
            if (o == null)
                throw new ArgumentNullException(nameof(name));
        }

        public static bool IsAuthor(this IMessage msg, IDiscordClient client)
            => msg.Author?.Id == client.CurrentUser.Id;

        public static string RealSummary(this CommandInfo cmd, IBotStrings strings, ulong? guildId, string prefix)
            => string.Format(strings.GetCommandStrings(cmd.Name, guildId).Desc, prefix);

        public static string[] RealRemarksArr(this CommandInfo cmd, IBotStrings strings, ulong? guildId, string prefix)
            => Array.ConvertAll(strings.GetCommandStrings(cmd.MethodName(), guildId).Args,
                arg => GetFullUsage(cmd.Name, arg, prefix));

        public static string MethodName(this CommandInfo cmd)
            => ((MewdekoCommandAttribute) cmd.Attributes.FirstOrDefault(x => x is MewdekoCommandAttribute))?.MethodName
               ?? cmd.Name;
        // public static string RealRemarks(this CommandInfo cmd, IBotStrings strings, string prefix)
        //     => string.Join('\n', cmd.RealRemarksArr(strings, prefix));

        public static string GetFullUsage(string commandName, string args, string prefix)
            => $"{prefix}{commandName} {string.Format(args, prefix)}";

        public static EmbedBuilder AddPaginatedFooter(this EmbedBuilder embed, int curPage, int? lastPage)
        {
            if (lastPage != null)
                return embed.WithFooter(efb => efb.WithText($"{curPage + 1} / {lastPage + 1}"));
            else
                return embed.WithFooter(efb => efb.WithText(curPage.ToString()));
        }

        public static EmbedBuilder WithOkColor(this EmbedBuilder eb) =>
            eb.WithColor(Mewdeko.OkColor);

        public static EmbedBuilder WithErrorColor(this EmbedBuilder eb) =>
            eb.WithColor(Mewdeko.ErrorColor);

        public static ReactionEventWrapper OnReaction(this IUserMessage msg, DiscordSocketClient client, Func<SocketReaction, Task> reactionAdded, Func<SocketReaction, Task> reactionRemoved = null)
        {
            if (reactionRemoved == null)
                reactionRemoved = _ => Task.CompletedTask;

            var wrap = new ReactionEventWrapper(client, msg);
            wrap.OnReactionAdded += (r) => { var _ = Task.Run(() => reactionAdded(r)); };
            wrap.OnReactionRemoved += (r) => { var _ = Task.Run(() => reactionRemoved(r)); };
            return wrap;
        }

        public static HttpClient AddFakeHeaders(this HttpClient http)
        {
            AddFakeHeaders(http.DefaultRequestHeaders);
            return http;
        }

        public static void AddFakeHeaders(this HttpHeaders dict)
        {
            dict.Clear();
            dict.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            dict.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/535.1 (KHTML, like Gecko) Chrome/14.0.835.202 Safari/535.1");
        }

        public static IMessage DeleteAfter(this IUserMessage msg, int seconds, LogCommandService logService = null)
        {
            if (msg is null)
                return null;
            
            Task.Run(async () =>
            {
                await Task.Delay(seconds * 1000).ConfigureAwait(false);
                if(logService != null)
                {
                    logService.AddDeleteIgnore(msg.Id);
                }
                try { await msg.DeleteAsync().ConfigureAwait(false); }
                catch { }
            });
            return msg;
        }

        public static ModuleInfo GetTopLevelModule(this ModuleInfo module)
        {
            while (module.Parent != null)
            {
                module = module.Parent;
            }
            return module;
        }

        public static void AddRange<T>(this HashSet<T> target, IEnumerable<T> elements) where T : class
        {
            foreach (var item in elements)
            {
                target.Add(item);
            }
        }

        public static void AddRange<T>(this ConcurrentHashSet<T> target, IEnumerable<T> elements) where T : class
        {
            foreach (var item in elements)
            {
                target.Add(item);
            }
        }

        public static double UnixTimestamp(this DateTime dt) 
            => dt.ToUniversalTime().Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;

        public static async Task<IEnumerable<IGuildUser>> GetMembersAsync(this IRole role) =>
            (await role.Guild.GetUsersAsync(CacheMode.CacheOnly).ConfigureAwait(false)).Where(u => u.RoleIds.Contains(role.Id)) ?? Enumerable.Empty<IGuildUser>();

        public static string ToJson<T>(this T any, Formatting formatting = Formatting.Indented) =>
            JsonConvert.SerializeObject(any, formatting);

        /// <summary>
        /// Adds fallback fonts to <see cref="TextOptions"/>
        /// </summary>
        /// <param name="opts"><see cref="TextOptions"/> to which fallback fonts will be added to</param>
        /// <param name="fallback">List of fallback Font Families to add</param>
        /// <returns>The same <see cref="TextOptions"/> to allow chaining</returns>
        public static TextOptions WithFallbackFonts(this TextOptions opts, List<FontFamily> fallback)
        {
            foreach (var ff in fallback)
            {
                opts.FallbackFonts.Add(ff);
            }
            return opts;
        }

        /// <summary>
        /// Adds fallback fonts to <see cref="TextGraphicsOptions"/>
        /// </summary>
        /// <param name="opts"><see cref="TextGraphicsOptions"/> to which fallback fonts will be added to</param>
        /// <param name="fallback">List of fallback Font Families to add</param>
        /// <returns>The same <see cref="TextGraphicsOptions"/> to allow chaining</returns>
        public static TextGraphicsOptions WithFallbackFonts(this TextGraphicsOptions opts, List<FontFamily> fallback)
        {
            opts.TextOptions.WithFallbackFonts(fallback);
            return opts;
        }

        public static MemoryStream ToStream(this Image<Rgba32> img, IImageFormat format = null)
        {
            var imageStream = new MemoryStream();
            if (format?.Name == "GIF")
            {
                img.SaveAsGif(imageStream);
            }
            else
            {
                img.SaveAsPng(imageStream, new PngEncoder()
                {
                    ColorType = PngColorType.RgbWithAlpha,
                    CompressionLevel = PngCompressionLevel.BestCompression
                });
            }
            imageStream.Position = 0;
            return imageStream;
        }

        public static Stream ToStream(this IEnumerable<byte> bytes, bool canWrite = false)
        {
            var ms = new MemoryStream(bytes as byte[] ?? bytes.ToArray(), canWrite);
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }

        public static IEnumerable<IRole> GetRoles(this IGuildUser user) =>
            user.RoleIds.Select(r => user.Guild.GetRole(r)).Where(r => r != null);

        public static async Task<IMessage> SendMessageToOwnerAsync(this IGuild guild, string message)
        {
            var ownerPrivate = await (await guild.GetOwnerAsync().ConfigureAwait(false)).GetOrCreateDMChannelAsync()
                                .ConfigureAwait(false);

            return await ownerPrivate.SendMessageAsync(message).ConfigureAwait(false);
        }

        public static bool IsImage(this HttpResponseMessage msg) => IsImage(msg, out _);

        public static bool IsImage(this HttpResponseMessage msg, out string mimeType)
        {
            mimeType = msg.Content.Headers.ContentType.MediaType;
            if (mimeType == "image/png"
                    || mimeType == "image/jpeg"
                    || mimeType == "image/gif")
            {
                return true;
            }
            return false;
        }

        public static long? GetImageSize(this HttpResponseMessage msg)
        {
            if (msg.Content.Headers.ContentLength == null)
            {
                return null;
            }

            return msg.Content.Headers.ContentLength / 1.MB();
        }



        public static IEnumerable<Type> LoadFrom(this IServiceCollection collection, Assembly assembly)
        {
            // list of all the types which are added with this method
            List<Type> addedTypes = new List<Type>();

            Type[] allTypes;
            try
            {
                // first, get all types in te assembly
                allTypes = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                Log.Error(ex, "Error loading assembly types");
                return Enumerable.Empty<Type>();
            }
            // all types which have INService implementation are services
            // which are supposed to be loaded with this method
            // ignore all interfaces and abstract classes
            var services = new Queue<Type>(allTypes
                    .Where(x => x.GetInterfaces().Contains(typeof(INService))
                        && !x.GetTypeInfo().IsInterface && !x.GetTypeInfo().IsAbstract
#if GLOBAL_Mewdeko
                        && x.GetTypeInfo().GetCustomAttribute<NoPublicBotAttribute>() == null
#endif
                            )
                    .ToArray());

            // we will just return those types when we're done instantiating them
            addedTypes.AddRange(services);

            // get all interfaces which inherit from INService
            // as we need to also add a service for each one of interfaces
            // so that DI works for them too
            var interfaces = new HashSet<Type>(allTypes
                    .Where(x => x.GetInterfaces().Contains(typeof(INService))
                        && x.GetTypeInfo().IsInterface));

            // keep instantiating until we've instantiated them all
            while (services.Count > 0)
            {
                var serviceType = services.Dequeue(); //get a type i need to add

                if (collection.FirstOrDefault(x => x.ServiceType == serviceType) != null) // if that type is already added, skip
                    continue;

                //also add the same type
                var interfaceType = interfaces.FirstOrDefault(x => serviceType.GetInterfaces().Contains(x));
                if (interfaceType != null)
                {
                    addedTypes.Add(interfaceType);
                    collection.AddSingleton(interfaceType, serviceType);
                }
                else
                {
                    collection.AddSingleton(serviceType, serviceType);
                }
            }

            return addedTypes;
        }
    }
}
