using System.Threading.Tasks;
using Mewdeko.Database.Common;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Gambling.Services.Impl;

public class ShopService : IShopService
{
    private readonly DbService db;


    public ShopService(DbService db) => this.db = db;

    public async Task<bool> ChangeEntryPriceAsync(ulong guildId, int index, int newPrice)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (newPrice <= 0)
            throw new ArgumentOutOfRangeException(nameof(newPrice));

        await using var uow = db.GetDbContext();
        var entries = await GetEntriesInternal(uow, guildId);

        if (index >= entries.Count)
            return false;

        entries[index].Price = newPrice;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        return true;
    }

    public async Task<bool> ChangeEntryNameAsync(ulong guildId, int index, string newName)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentNullException(nameof(newName));

        await using var uow = db.GetDbContext();
        var entries = await GetEntriesInternal(uow, guildId);

        if (index >= entries.Count)
            return false;

        entries[index].Name = newName.TrimTo(100);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        return true;
    }

    public async Task<bool> SwapEntriesAsync(ulong guildId, int index1, int index2)
    {
        if (index1 < 0)
            throw new ArgumentOutOfRangeException(nameof(index1));
        if (index2 < 0)
            throw new ArgumentOutOfRangeException(nameof(index2));

        await using var uow = db.GetDbContext();
        var entries = await GetEntriesInternal(uow, guildId);

        if (index1 >= entries.Count || index2 >= entries.Count || index1 == index2)
            return false;

        entries[index1].Index = index2;
        entries[index2].Index = index1;

        await uow.SaveChangesAsync().ConfigureAwait(false);
        return true;
    }

    public async Task<bool> MoveEntryAsync(ulong guildId, int fromIndex, int toIndex)
    {
        if (fromIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(fromIndex));
        if (toIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(toIndex));

        await using var uow = db.GetDbContext();
        var entries = await GetEntriesInternal(uow, guildId);

        if (fromIndex >= entries.Count || toIndex >= entries.Count || fromIndex == toIndex)
            return false;

        var entry = entries[fromIndex];
        entries.RemoveAt(fromIndex);
        entries.Insert(toIndex, entry);

        await uow.SaveChangesAsync().ConfigureAwait(false);
        return true;
    }

    private static async Task<IndexedCollection<ShopEntry>> GetEntriesInternal(MewdekoContext uow, ulong guildId) =>
        (await uow.ForGuildId(
            guildId,
            set => set.Include(x => x.ShopEntries)
                .ThenInclude(x => x.Items)
        ))
        .ShopEntries
        .ToIndexed();
}