using System.Collections.Generic;
using System.Threading.Tasks;
using Mewdeko.Core.Services.Database.Models;

#nullable enable
namespace Mewdeko.Core.Modules.Searches.Common.StreamNotifications.Providers
{
    /// <summary>
    /// Abstract class implemented by providers of all supported platforms
    /// </summary>
    public abstract class Provider
    {
        /// <summary>
        /// Type of the platform.
        /// </summary>
        public abstract FollowedStream.FType Platform { get; }

        /// <summary>
        /// Checks whether the specified url is a valid stream url for this platform.
        /// </summary>
        /// <param name="url">Url to check</param>
        /// <returns>True if valid, otherwise false</returns>
        public abstract Task<bool> IsValidUrl(string url);
        
        /// <summary>
        /// Gets stream data of the stream on the specified url on this <see cref="Platform"/>
        /// </summary>
        /// <param name="url">Url of the stream</param>
        /// <returns><see cref="StreamData"/> of the specified stream. Null if none found</returns>
        public abstract Task<StreamData?> GetStreamDataByUrlAsync(string url);
            
        /// <summary>
        /// Gets stream data of the specified id/username on this <see cref="Platform"/>
        /// </summary>
        /// <param name="id">Name (or id where applicable) of the user on the platform</param>
        /// <returns><see cref="StreamData"/> of the user. Null if none found</returns>
        public abstract Task<StreamData?> GetStreamDataAsync(string id);

        /// <summary>
        /// Gets stream data of all specified ids/usernames on this <see cref="Platform"/>
        /// </summary>
        /// <param name="usernames">List of ids/usernames</param>
        /// <returns><see cref="StreamData"/> of all users, in the same order. Null for every id/user not found.</returns>
        public abstract Task<List<StreamData>> GetStreamDataAsync(List<string> usernames);
    }
}