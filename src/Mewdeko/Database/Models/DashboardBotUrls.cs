namespace Mewdeko.Database.Models;

/// <summary>
/// Class to get urls for each bot that is able to use a dashboard. Is entirely
/// </summary>
public class LocalBotInstances : DbEntity
{
    /// <summary>
    /// The instances url
    /// </summary>
    public string BotUrl { get; set; }
}