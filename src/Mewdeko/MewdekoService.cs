using Microsoft.Extensions.Hosting;
using System.Threading;

namespace Mewdeko;

/// <summary>
/// A hosted service that manages the lifecycle of the Mewdeko bot.
/// </summary>
/// <remarks>
/// This class implements <see cref="IHostedService"/> to integrate with the .NET Core hosting model.
/// It's responsible for starting and stopping the Mewdeko bot as part of the application's lifecycle.
/// </remarks>
public class MewdekoService : IHostedService
{
    private readonly Mewdeko mewdeko;

    /// <summary>
    /// Initializes a new instance of the <see cref="MewdekoService"/> class.
    /// </summary>
    /// <param name="mewdeko">The Mewdeko bot instance to be managed by this service.</param>
    public MewdekoService(Mewdeko mewdeko)
    {
        this.mewdeko = mewdeko;
    }

    /// <summary>
    /// Starts the Mewdeko bot.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the start operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation of starting the bot.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return mewdeko.RunAsync();
    }

    /// <summary>
    /// Stops the Mewdeko bot.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the stop operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation of stopping the bot.</returns>
    /// <remarks>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        Environment.Exit(0);
        return Task.CompletedTask;
    }
}