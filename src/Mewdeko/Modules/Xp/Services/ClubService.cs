using System.Net.Http;
using System.Threading.Tasks;
using Mewdeko.Modules.Xp.Common;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Xp.Services;

public class ClubService : INService
{
    private readonly DbService db;
    private readonly IHttpClientFactory httpFactory;

    public ClubService(DbService db, IHttpClientFactory httpFactory)
    {
        this.db = db;
        this.httpFactory = httpFactory;
    }

    public async Task<(bool, ClubInfo)> CreateClub(IUser user, string clubName)
    {
        //must be lvl 5 and must not be in a club already

        await using var uow = db.GetDbContext();
        var du = await uow.GetOrCreateUser(user).ConfigureAwait(false);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        var xp = new LevelStats(du.TotalXp);

        if (xp.Level >= 5 && du.Club == null)
        {
            du.IsClubAdmin = true;
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

        club.Owner.IsClubAdmin = true; // old owner will stay as admin
        newOwnerUser.IsClubAdmin = true;
        club.Owner = newOwnerUser;
        await uow.SaveChangesAsync().ConfigureAwait(false);

        return club;
    }

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

        var newState = adminUser.IsClubAdmin = !adminUser.IsClubAdmin;
        await uow.SaveChangesAsync().ConfigureAwait(false);

        return newState;
    }

    public async Task<ClubInfo?> GetClubByMember(IUser user)
    {
        await using var uow = db.GetDbContext();
        return await uow.Clubs.GetByMember(user.Id).ConfigureAwait(false);
    }

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
        applicant.User.IsClubAdmin = false;
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

    public async Task<ClubInfo?> GetClubWithBansAndApplications(ulong ownerUserId)
    {
        await using var uow = db.GetDbContext();
        return await uow.Clubs.GetByOwnerOrAdmin(ownerUserId).ConfigureAwait(false);
    }

    public async Task<bool> LeaveClub(IUser user)
    {
        await using var uow = db.GetDbContext();
        var du = await uow.GetOrCreateUser(user).ConfigureAwait(false);
        if (du.Club == null || du.Club.OwnerId == du.Id)
            return false;

        du.Club = null;
        du.IsClubAdmin = false;
        await uow.SaveChangesAsync().ConfigureAwait(false);

        return true;
    }

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

    public async Task<(bool, ClubInfo)> Ban(ulong bannerId, string userName)
    {
        await using var uow = db.GetDbContext();
        var club = await uow.Clubs.GetByOwnerOrAdmin(bannerId).ConfigureAwait(false);
        if (club == null)
            return (false, null);

        var usr = club.Users.Find(x => string.Equals(x.ToString(), userName, StringComparison.InvariantCultureIgnoreCase))
                  ?? club.Applicants.Find(x =>
                      string.Equals(x.User.ToString(), userName, StringComparison.InvariantCultureIgnoreCase))?.User;
        if (usr == null)
            return (false, null);

        if (club.OwnerId == usr.Id ||
            (usr.IsClubAdmin && club.Owner.UserId != bannerId)) // can't ban the owner kek, whew
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

    public async Task<(bool, ClubInfo)> Kick(ulong kickerId, string userName)
    {
        await using var uow = db.GetDbContext();
        var club = await uow.Clubs.GetByOwnerOrAdmin(kickerId).ConfigureAwait(false);

        var usr = club?.Users.Find(x =>
            string.Equals(x.ToString(), userName, StringComparison.InvariantCultureIgnoreCase));
        if (usr == null)
            return (false, null);

        if (club.OwnerId == usr.Id || (usr.IsClubAdmin && club.Owner.UserId != kickerId))
            return (false, club);

        club.Users.Remove(usr);
        var app = club.Applicants.Find(x => x.UserId == usr.Id);
        if (app != null)
            club.Applicants.Remove(app);
        await uow.SaveChangesAsync().ConfigureAwait(false);

        return (true, club);
    }

    public async Task<List<ClubInfo>> GetClubLeaderboardPage(int page)
    {
        if (page < 0)
            throw new ArgumentOutOfRangeException(nameof(page));

        await using var uow = db.GetDbContext();
        return await uow.Clubs.GetClubLeaderboardPage(page);
    }
}