using System.Net.Http;
using Discord;
using Mewdeko._Extensions;
using Mewdeko.Database.Extensions;
using Mewdeko.Database.Models;
using Mewdeko.Modules.Xp.Common;
using System.Collections.Generic;

namespace Mewdeko.Modules.Xp.Services;

public class ClubService : INService
{
    private readonly DbService _db;
    private readonly IHttpClientFactory _httpFactory;

    public ClubService(DbService db, IHttpClientFactory httpFactory)
    {
        _db = db;
        _httpFactory = httpFactory;
    }

    public bool CreateClub(IUser user, string clubName, out ClubInfo club)
    {
        //must be lvl 5 and must not be in a club already

        club = null;
        using var uow = _db.GetDbContext();
        var du = uow.GetOrCreateUser(user);
        uow.SaveChanges();
        var xp = new LevelStats(du.TotalXp);

        if (xp.Level >= 5 && du.Club == null)
        {
            du.IsClubAdmin = true;
            du.Club = new ClubInfo
            {
                Name = clubName,
                Discrim = uow.Clubs.GetNextDiscrim(clubName),
                Owner = du
            };
            uow.Clubs.Add(du.Club);
            uow.SaveChanges();
        }
        else
        {
            return false;
        }

        uow.Set<ClubApplicants>()
            .RemoveRange(uow.Set<ClubApplicants>()
                .AsQueryable()
                .Where(x => x.UserId == du.Id));
        club = du.Club;
        uow.SaveChanges();

        return true;
    }

    public ClubInfo TransferClub(IUser from, IUser newOwner)
    {
        ClubInfo club;
        using var uow = _db.GetDbContext();
        club = uow.Clubs.GetByOwner(from.Id);
        var newOwnerUser = uow.GetOrCreateUser(newOwner);

        if (club == null ||
            club.Owner.UserId != from.Id ||
            !club.Users.Contains(newOwnerUser))
            return null;

        club.Owner.IsClubAdmin = true; // old owner will stay as admin
        newOwnerUser.IsClubAdmin = true;
        club.Owner = newOwnerUser;
        uow.SaveChanges();

        return club;
    }

    public bool ToggleAdmin(IUser owner, IUser toAdmin)
    {
        bool newState;
        using var uow = _db.GetDbContext();
        var club = uow.Clubs.GetByOwner(owner.Id);
        var adminUser = uow.GetOrCreateUser(toAdmin);

        if (club == null || club.Owner.UserId != owner.Id ||
            !club.Users.Contains(adminUser))
            throw new InvalidOperationException();

        if (club.OwnerId == adminUser.Id)
            return true;

        newState = adminUser.IsClubAdmin = !adminUser.IsClubAdmin;
        uow.SaveChanges();

        return newState;
    }

    public ClubInfo GetClubByMember(IUser user)
    {
        using var uow = _db.GetDbContext();
        return uow.Clubs.GetByMember(user.Id);
    }

    public async Task<bool> SetClubIcon(ulong ownerUserId, Uri url)
    {
        if (url != null)
            using (var http = _httpFactory.CreateClient())
            using (var temp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead)
                       .ConfigureAwait(false))
            {
                if (!temp.IsImage() || temp.GetImageSize() > 11)
                    return false;
            }

        await using var uow = _db.GetDbContext();
        var club = uow.Clubs.GetByOwner(ownerUserId);

        if (club == null)
            return false;

        club.ImageUrl = url.ToString();
        await uow.SaveChangesAsync();

        return true;
    }

    public bool GetClubByName(string clubName, out ClubInfo club)
    {
        club = null;
        var arr = clubName.Split('#');
        if (arr.Length < 2 || !int.TryParse(arr[^1], out var discrim))
            return false;

        //incase club has # in it
        var name = string.Concat(arr.Except(new[] {arr[^1]}));

        if (string.IsNullOrWhiteSpace(name))
            return false;

        using var uow = _db.GetDbContext();
        club = uow.Clubs.GetByName(name, discrim);
        if (club == null)
            return false;
        return true;
    }

    public bool ApplyToClub(IUser user, ClubInfo club)
    {
        using var uow = _db.GetDbContext();
        var du = uow.GetOrCreateUser(user);
        uow.SaveChanges();

        if (du.Club != null
            || new LevelStats(du.TotalXp).Level < club.MinimumLevelReq
            || club.Bans.Any(x => x.UserId == du.Id)
            || club.Applicants.Any(x => x.UserId == du.Id))
            //user banned or a member of a club, or already applied,
            // or doesn't min minumum level requirement, can't apply
            return false;

        var app = new ClubApplicants
        {
            ClubId = club.Id,
            UserId = du.Id
        };

        uow.Set<ClubApplicants>().Add(app);

        uow.SaveChanges();

        return true;
    }

    public bool AcceptApplication(ulong clubOwnerUserId, string userName, out DiscordUser discordUser)
    {
        discordUser = null;
        using var uow = _db.GetDbContext();
        var club = uow.Clubs.GetByOwnerOrAdmin(clubOwnerUserId);
        if (club == null)
            return false;

        var applicant = club.Applicants.FirstOrDefault(x =>
            x.User.ToString().ToUpperInvariant() == userName.ToUpperInvariant());
        if (applicant == null)
            return false;

        applicant.User.Club = club;
        applicant.User.IsClubAdmin = false;
        club.Applicants.Remove(applicant);

        //remove that user's all other applications
        uow.Set<ClubApplicants>()
            .RemoveRange(uow.Set<ClubApplicants>()
                .AsQueryable()
                .Where(x => x.UserId == applicant.User.Id));

        discordUser = applicant.User;
        uow.SaveChanges();

        return true;
    }

    public ClubInfo GetClubWithBansAndApplications(ulong ownerUserId)
    {
        using var uow = _db.GetDbContext();
        return uow.Clubs.GetByOwnerOrAdmin(ownerUserId);
    }

    public bool LeaveClub(IUser user)
    {
        using var uow = _db.GetDbContext();
        var du = uow.GetOrCreateUser(user);
        if (du.Club == null || du.Club.OwnerId == du.Id)
            return false;

        du.Club = null;
        du.IsClubAdmin = false;
        uow.SaveChanges();

        return true;
    }

    public bool ChangeClubLevelReq(ulong userId, int level)
    {
        if (level < 5)
            return false;

        using var uow = _db.GetDbContext();
        var club = uow.Clubs.GetByOwner(userId);
        if (club == null)
            return false;

        club.MinimumLevelReq = level;
        uow.SaveChanges();

        return true;
    }

    public bool ChangeClubDescription(ulong userId, string desc)
    {
        using var uow = _db.GetDbContext();
        var club = uow.Clubs.GetByOwner(userId);
        if (club == null)
            return false;

        club.Description = desc?.TrimTo(150, true);
        uow.SaveChanges();

        return true;
    }

    public bool Disband(ulong userId, out ClubInfo club)
    {
        using var uow = _db.GetDbContext();
        club = uow.Clubs.GetByOwner(userId);
        if (club == null)
            return false;

        uow.Clubs.Remove(club);
        uow.SaveChanges();

        return true;
    }

    public bool Ban(ulong bannerId, string userName, out ClubInfo club)
    {
        using var uow = _db.GetDbContext();
        club = uow.Clubs.GetByOwnerOrAdmin(bannerId);
        if (club == null)
            return false;

        var usr = club.Users.FirstOrDefault(x => x.ToString().ToUpperInvariant() == userName.ToUpperInvariant())
                  ?? club.Applicants.FirstOrDefault(x =>
                      x.User.ToString().ToUpperInvariant() == userName.ToUpperInvariant())?.User;
        if (usr == null)
            return false;

        if (club.OwnerId == usr.Id ||
            (usr.IsClubAdmin && club.Owner.UserId != bannerId)) // can't ban the owner kek, whew
            return false;

        club.Bans.Add(new ClubBans
        {
            Club = club,
            User = usr
        });
        club.Users.Remove(usr);

        var app = club.Applicants.FirstOrDefault(x => x.UserId == usr.Id);
        if (app != null)
            club.Applicants.Remove(app);

        uow.SaveChanges();

        return true;
    }

    public bool UnBan(ulong ownerUserId, string userName, out ClubInfo club)
    {
        using var uow = _db.GetDbContext();
        club = uow.Clubs.GetByOwnerOrAdmin(ownerUserId);
        if (club == null)
            return false;

        var ban = club.Bans.FirstOrDefault(x =>
            x.User.ToString().ToUpperInvariant() == userName.ToUpperInvariant());
        if (ban == null)
            return false;

        club.Bans.Remove(ban);
        uow.SaveChanges();

        return true;
    }

    public bool Kick(ulong kickerId, string userName, out ClubInfo club)
    {
        using var uow = _db.GetDbContext();
        club = uow.Clubs.GetByOwnerOrAdmin(kickerId);
        if (club == null)
            return false;

        var usr = club.Users.FirstOrDefault(x =>
            x.ToString().ToUpperInvariant() == userName.ToUpperInvariant());
        if (usr == null)
            return false;

        if (club.OwnerId == usr.Id || (usr.IsClubAdmin && club.Owner.UserId != kickerId))
            return false;

        club.Users.Remove(usr);
        var app = club.Applicants.FirstOrDefault(x => x.UserId == usr.Id);
        if (app != null)
            club.Applicants.Remove(app);
        uow.SaveChanges();

        return true;
    }

    public List<ClubInfo> GetClubLeaderboardPage(int page)
    {
        if (page < 0)
            throw new ArgumentOutOfRangeException(nameof(page));

        using var uow = _db.GetDbContext();
        return uow.Clubs.GetClubLeaderboardPage(page);
    }
}