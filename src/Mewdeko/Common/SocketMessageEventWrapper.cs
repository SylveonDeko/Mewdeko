using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace Mewdeko.Common
{
    public sealed class ReactionEventWrapper : IDisposable
    {
        private readonly DiscordSocketClient _client;

        private bool disposing;

        public ReactionEventWrapper(DiscordSocketClient client, IUserMessage msg)
        {
            Message = msg ?? throw new ArgumentNullException(nameof(msg));
            _client = client;

            _client.ReactionAdded += Discord_ReactionAdded;
            _client.ReactionRemoved += Discord_ReactionRemoved;
            _client.ReactionsCleared += Discord_ReactionsCleared;
            _client.InteractionCreated += Discord_InteractionCreated;
        }

        public IUserMessage Message { get; }

        public void Dispose()
        {
            if (disposing)
                return;
            disposing = true;
            UnsubAll();
        }

        public event Action<SocketReaction> OnReactionAdded = delegate { };
        public event Action<SocketReaction> OnReactionRemoved = delegate { };
        public event Action OnReactionsCleared = delegate { };
        public event Action<SocketInteraction> InteractionCreated = delegate { };

        private Task Discord_ReactionsCleared(Cacheable<IUserMessage, ulong> msg,
            Cacheable<IMessageChannel, ulong> chan)
        {
            Task.Run(() =>
            {
                try
                {
                    if (msg.Value.Id == Message.Id)
                        OnReactionsCleared?.Invoke();
                }
                catch
                {
                }
            });

            return Task.CompletedTask;
        }

        private Task Discord_InteractionCreated(SocketInteraction inte)
        {
            Task.Run(() =>
            {
                try
                {
                    InteractionCreated?.Invoke(inte);
                }
                catch
                {
                }
            });

            return Task.CompletedTask;
        }

        private Task Discord_ReactionRemoved(Cacheable<IUserMessage, ulong> msg, Cacheable<IMessageChannel, ulong> chan,
            SocketReaction reaction)
        {
            Task.Run(() =>
            {
                try
                {
                    if (msg.Id == Message.Id)
                        OnReactionRemoved?.Invoke(reaction);
                }
                catch
                {
                }
            });

            return Task.CompletedTask;
        }

        private Task Discord_ReactionAdded(Cacheable<IUserMessage, ulong> msg, Cacheable<IMessageChannel, ulong> chan,
            SocketReaction reaction)
        {
            Task.Run(() =>
            {
                try
                {
                    if (msg.Id == Message.Id)
                        OnReactionAdded?.Invoke(reaction);
                }
                catch
                {
                }
            });

            return Task.CompletedTask;
        }

        public void UnsubAll()
        {
            _client.ReactionAdded -= Discord_ReactionAdded;
            _client.ReactionRemoved -= Discord_ReactionRemoved;
            _client.ReactionsCleared -= Discord_ReactionsCleared;
            OnReactionAdded = null;
            OnReactionRemoved = null;
            OnReactionsCleared = null;
        }
    }
}