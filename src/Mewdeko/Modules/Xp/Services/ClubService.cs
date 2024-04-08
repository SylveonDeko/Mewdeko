using System.Net.Http;
using Mewdeko.Modules.Xp.Common;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Xp.Services;

/// <summary>
/// Provides services related to club management within the experience (XP) module.
/// </summary>
public class ClubService(DbService db, IHttpClientFactory httpFactory) : INService
{
    /// <summary>
    /// Attempts to create a new club with the specified name for the given user.
    /// </summary>
    /// <param name="user">The user creating the club.</param>
    /// <param name="clubName">The desired name for the club.</param>
    /// <returns>A tuple indicating success status and the created <see cref="ClubInfo"/>, if successful.</returns>
    public async Task<(bool, ClubInfo)> CreateClub(IUser user, string clubName)
    {
        //must be lvl 5 and must not be in a club already

        await using var uow = db.GetDbContext();
        var du = await uow.GetOrCreateUser(user).ConfigureAwait(false);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        var xp = new LevelStats(du.TotalXp);

        if (xp.Level >= 5 && du.Club == null)
        {
            du.IsClubAdmin = 1;
            du.Club = new ClubInfo
            {
                Name = clubName, Discrim = await uow.Clubs.GetNextDiscrim(clubName).ConfigureAwait(false), Owner = du
            };
            uow.Clubs.Add(du.Club);
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }
        else
        {
            return (false, null);
        }

        uow.Set<ClubApplicants>()
            .RemoveRange(uow.Set<ClubApplicants>()
                .AsQueryable()
                .Where(x => x.UserId == du.Id));
        await uow.SaveChangesAsync().ConfigureAwait(false);

        return (true, du.Club);
    }

    /// <summary>
    /// Transfers club ownership from one user to another.
    /// </summary>
    /// <param name="from">The current owner of the club.</param>
    /// <param name="newOwner">The new owner to transfer the club to.</param>
    /// <returns>The updated <see cref="ClubInfo"/> with the new owner, or null if the transfer was unsuccessful.</returns>
    public async Task<ClubInfo?> TransferClub(IUser from, IUser newOwner)
    {
        await using var uow = db.GetDbContext();
        var club = await uow.Clubs.GetByOwner(from.Id).ConfigureAwait(false);
        var newOwnerUser = await uow.GetOrCreateUser(newOwner).ConfigureAwait(false);

        if (club == null ||
            club.Owner.UserId != from.Id ||
            !club.Users.Contains(newOwnerUser))
        {
            return null;
        }

        club.Owner.IsClubAdmin = 1; // old owner will stay as admin
        newOwnerUser.IsClubAdmin = 1;
        club.Owner = newOwnerUser;
        await uow.SaveChangesAsync().ConfigureAwait(false);

        return club;
    }

    /// <summary>
    /// Toggles the admin status of a user within a club.
    /// </summary>
    /// <param name="owner">The owner of the club.</param>
    /// <param name="toAdmin">The user to toggle admin status for.</param>
    /// <returns>True if the operation was successful, false otherwise.</returns>
    public async Task<bool> ToggleAdmin(IUser owner, IUser toAdmin)
    {
        await using var uow = db.GetDbContext();
        var club = await uow.Clubs.GetByOwner(owner.Id).ConfigureAwait(false);
        var adminUser = await uow.GetOrCreateUser(toAdmin).ConfigureAwait(false);

        if (club == null || club.Owner.UserId != owner.Id ||
            !club.Users.Contains(adminUser))
        {
            throw new InvalidOperationException();
        }

        if (club.OwnerId == adminUser.Id)
            return true;

        var newState = adminUser.IsClubAdmin = !false.ParseBoth(adminUser.IsClubAdmin) ? 1 : 0;
        await uow.SaveChangesAsync().ConfigureAwait(false);

        return false.ParseBoth(newState);
    }

    /// <summary>
    /// Gets the club information by a member of the club.
    /// </summary>
    /// <param name="user">The user whose club information is to be retrieved.</param>
    /// <returns>The <see cref="ClubInfo"/> of the club the user is a member of, or null if the user is not in a club.</returns>
    public async Task<ClubInfo?> GetClubByMember(IUser user)
    {
        await using var uow = db.GetDbContext();
        return await uow.Clubs.GetByMember(user.Id).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the icon for a club owned by the specified user.
    /// </summary>
    /// <param name="ownerUserId">The user ID of the club owner.</param>
    /// <param name="url">The URL of the new club icon.</param>
    /// <returns>True if the operation was successful, false otherwise.</returns>
    public async Task<bool> SetClubIcon(ulong ownerUserId, Uri? url)
    {
        if (url != null)
        {
            using var http = httpFactory.CreateClient();
            using var temp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);
            if (!temp.IsImage() || temp.GetImageSize() > 11)
                return false;
        }

        await using var uow = db.GetDbContext();
        var club = await uow.Clubs.GetByOwner(ownerUserId).ConfigureAwait(false);

        if (club == null)
            return false;

        club.ImageUrl = url.ToString();
        await uow.SaveChangesAsync().ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Retrieves a club by its name.
    /// </summary>
    /// <param name="clubName">The name of the club to retrieve.</param>
    /// <param name="club">Out parameter that contains the retrieved club, if found.</param>
    /// <returns>True if the club was found, false otherwise.</returns>
    public bool GetClubByName(string clubName, out ClubInfo club)
    {
        club = null;
        var arr = clubName.Split('#');
        if (arr.Length < 2 || !int.TryParse(arr[^1], out var discrim))
            return false;

        //incase club has # in it
        var name = string.Concat(arr.Except(new[]
        {
            arr[^1]
        }));

        if (string.IsNullOrWhiteSpace(name))
            return false;

        using var uow = db.GetDbContext();
        club = uow.Clubs.GetByName(name, discrim);
        if (club == null)
            return false;
        return true;
    }

    /// <summary>
    /// Applies a user to a club.
    /// </summary>
    /// <param name="user">The user applying to the club.</param>
    /// <param name="club">The club to apply to.</param>
    /// <returns>True if the application was successful, false otherwise.</returns>
    public async Task<bool> ApplyToClub(IUser user, ClubInfo club)
    {
        await using var uow = db.GetDbContext();
        var du = await uow.GetOrCreateUser(user).ConfigureAwait(false);
        await uow.SaveChangesAsync().ConfigureAwait(false);

        if (du.Club != null
            || new LevelStats(du.TotalXp).Level < club.MinimumLevelReq
            || club.Bans.Any(x => x.UserId == du.Id)
            || club.Applicants.Any(x => x.UserId == du.Id))
        {
            //user banned or a member of a club, or already applied,
            // or doesn't min minumum level requirement, can't apply
            return false;
        }

        var app = new ClubApplicants
        {
            ClubId = club.Id, UserId = du.Id
        };

        uow.Set<ClubApplicants>().Add(app);

        await uow.SaveChangesAsync().ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Accepts an application to a club.
    /// </summary>
    /// <param name="clubOwnerUserId">The user ID of the club owner.</param>
    /// <param name="userName">The name of the user whose application is being accepted.</param>
    /// <returns>A tuple containing the operation's success status and the <see cref="DiscordUser"/> if the operation was successful.</returns>
    public async Task<(bool, DiscordUser)> AcceptApplication(ulong clubOwnerUserId, string userName)
    {
        await using var uow = db.GetDbContext();
        var discordUser = await uow.DiscordUser.FirstOrDefaultAsync(x => x.Username == userName);
        var club = await uow.Clubs.GetByOwnerOrAdmin(clubOwnerUserId).ConfigureAwait(false);

        var applicant = club?.Applicants.Find(x =>
            string.Equals(x.User.ToString(), userName, StringComparison.InvariantCultureIgnoreCase));
        if (applicant == null)
            return (false, discordUser);

        applicant.User.Club = club;
        applicant.User.IsClubAdmin = 0;
        club.Applicants.Remove(applicant);

        //remove that user's all other applications
        uow.Set<ClubApplicants>()
            .RemoveRange(uow.Set<ClubApplicants>()
                .AsQueryable()
                .Where(x => x.UserId == applicant.User.Id));

        discordUser = applicant.User;
        await uow.SaveChangesAsync().ConfigureAwait(false);

        return (true, discordUser);
    }

    /// <summary>
    /// Retrieves club information along with its bans and applications.
    /// </summary>
    /// <param name="ownerUserId">The user ID of the club owner or admin.</param>
    /// <returns>The <see cref="ClubInfo"/> including bans and applications, or null if not found.</returns>
    public async Task<ClubInfo?> GetClubWithBansAndApplications(ulong ownerUserId)
    {
        await using var uow = db.GetDbContext();
        return await uow.Clubs.GetByOwnerOrAdmin(ownerUserId).ConfigureAwait(false);
    }

    /// <summary>
    /// A member leaves a club.
    /// </summary>
    /// <param name="user">The user leaving the club.</param>
    /// <returns>True if the operation was successful, false otherwise.</returns>
    public async Task<bool> LeaveClub(IUser user)
    {
        await using var uow = db.GetDbContext();
        var du = await uow.GetOrCreateUser(user).ConfigureAwait(false);
        if (du.Club == null || du.Club.OwnerId == du.Id)
            return false;

        du.Club = null;
        du.IsClubAdmin = 0;
        await uow.SaveChangesAsync().ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Changes the minimum level requirement to join a club.
    /// </summary>
    /// <param name="userId">The user ID of the club owner.</param>
    /// <param name="level">The new minimum level requirement.</param>
    /// <returns>True if the operation was successful, false otherwise.</returns>
    public async Task<bool> ChangeClubLevelReq(ulong userId, int level)
    {
        if (level < 5)
            return false;

        await using var uow = db.GetDbContext();
        var club = await uow.Clubs.GetByOwner(userId).ConfigureAwait(false);
        if (club == null)
            return false;

        club.MinimumLevelReq = level;
        await uow.SaveChangesAsync().ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Changes the description of a club.
    /// </summary>
    /// <param name="userId">The user ID of the club owner.</param>
    /// <param name="desc">The new description for the club.</param>
    /// <returns>True if the operation was successful, false otherwise.</returns>
    public async Task<bool> ChangeClubDescription(ulong userId, string? desc)
    {
        await using var uow = db.GetDbContext();
        var club = await uow.Clubs.GetByOwner(userId).ConfigureAwait(false);
        if (club == null)
            return false;

        club.Description = desc?.TrimTo(150, true);
        await uow.SaveChangesAsync().ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Disbands a club.
    /// </summary>
    /// <param name="userId">The user ID of the club owner.</param>
    /// <returns>A tuple containing the operation's success status and the <see cref="ClubInfo"/> of the disbanded club, if successful.</returns>
    public async Task<(bool, ClubInfo)> Disband(ulong userId)
    {
        await using var uow = db.GetDbContext();
        var club = await uow.Clubs.GetByOwner(userId).ConfigureAwait(false);
        if (club == null)
            return (false, null);

        uow.Clubs.Remove(club);
        await uow.SaveChangesAsync().ConfigureAwait(false);

        return (true, club);
    }

    /// <summary>
    /// Bans a user from a club.
    /// </summary>
    /// <param name="bannerId">The user ID of the person performing the ban.</param>
    /// <param name="userName">The name of the user to be banned.</param>
    /// <returns>A tuple indicating success status and the updated <see cref="ClubInfo"/>, if successful.</returns>
    public async Task<(bool, ClubInfo)> Ban(ulong bannerId, string userName)
    {
        await using var uow = db.GetDbContext();
        var club = await uow.Clubs.GetByOwnerOrAdmin(bannerId).ConfigureAwait(false);
        if (club == null)
            return (false, null);

        var usr = club.Users.Find(x =>
                      string.Equals(x.ToString(), userName, StringComparison.InvariantCultureIgnoreCase))
                  ?? club.Applicants.Find(x =>
                      string.Equals(x.User.ToString(), userName, StringComparison.InvariantCultureIgnoreCase))?.User;
        if (usr == null)
            return (false, null);

        if (club.OwnerId == usr.Id ||
            (false.ParseBoth(usr.IsClubAdmin) && club.Owner.UserId != bannerId)) // can't ban the owner kek, whew
        {
            return (false, club);
        }

        club.Bans.Add(new ClubBans
        {
            Club = club, User = usr
        });
        club.Users.Remove(usr);

        var app = club.Applicants.Find(x => x.UserId == usr.Id);
        if (app != null)
            club.Applicants.Remove(app);

        await uow.SaveChangesAsync().ConfigureAwait(false);

        return (true, club);
    }

    /// <summary>
    /// Unbans a user from a club.
    /// </summary>
    /// <param name="ownerUserId">The user ID of the club owner or an administrator.</param>
    /// <param name="userName">The name of the user to be unbanned.</param>
    /// <returns>A tuple indicating success status and the updated <see cref="ClubInfo"/>, if successful.</returns>
    public async Task<(bool, ClubInfo)> UnBan(ulong ownerUserId, string userName)
    {
        await using var uow = db.GetDbContext();
        var club = await uow.Clubs.GetByOwnerOrAdmin(ownerUserId).ConfigureAwait(false);

        var ban = club?.Bans.Find(x =>
            string.Equals(x.User.ToString(), userName, StringComparison.InvariantCultureIgnoreCase));
        if (ban == null)
            return (false, club);

        club.Bans.Remove(ban);
        await uow.SaveChangesAsync().ConfigureAwait(false);

        return (true, club);
    }

    /// <summary>
    /// Kicks a user from a club.
    /// </summary>
    /// <param name="kickerId">The user ID of the person performing the kick.</param>
    /// <param name="userName">The name of the user to be kicked.</param>
    /// <returns>A tuple indicating success status and the updated <see cref="ClubInfo"/>, if successful.</returns>
    public async Task<(bool, ClubInfo)> Kick(ulong kickerId, string userName)
    {
        await using var uow = db.GetDbContext();
        var club = await uow.Clubs.GetByOwnerOrAdmin(kickerId).ConfigureAwait(false);

        var usr = club?.Users.Find(x =>
            string.Equals(x.ToString(), userName, StringComparison.InvariantCultureIgnoreCase));
        if (usr == null)
            return (false, null);

        if (club.OwnerId == usr.Id || (false.ParseBoth(usr.IsClubAdmin) && club.Owner.UserId != kickerId))
            return (false, club);

        club.Users.Remove(usr);
        var app = club.Applicants.Find(x => x.UserId == usr.Id);
        if (app != null)
            club.Applicants.Remove(app);
        await uow.SaveChangesAsync().ConfigureAwait(false);

        return (true, club);
    }

    /// <summary>
    /// Retrieves a page of clubs for the leaderboard display.
    /// </summary>
    /// <param name="page">The page number to retrieve.</param>
    /// <returns>A list of <see cref="ClubInfo"/> objects representing the clubs on the requested leaderboard page.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the provided page number is less than 0.</exception>
    public async Task<List<ClubInfo>> GetClubLeaderboardPage(int page)
    {
        if (page < 0)
            throw new ArgumentOutOfRangeException(nameof(page));

        await using var uow = db.GetDbContext();
        return await uow.Clubs.GetClubLeaderboardPage(page);
    }
}