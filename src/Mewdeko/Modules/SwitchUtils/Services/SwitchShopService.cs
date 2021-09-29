// using System;
// using System.Collections.Generic;
// using Mewdeko.Services;
// using System.Threading.Tasks;
// using Mewdeko.Services.Database.Models;
// namespace Mewdeko.Modules.SwitchUtils.Services
// {
//     public class SwitchShopService : INService
//     {
//         private DbService _db;
//         public SwitchShopService(DbService db)
//         {
//             _db = db;
//         }
//         public async Task AddShop(ulong GuildId, ulong Owner, string ShopName, string ShopUrl, string InviteLink, string Status, string ExtraOwners = null)
//         {
//             var toadd = new SwitchShops
//             {
//                 GuildId = GuildId,
//                 Owner = Owner,
//                 ShopName = ShopName,
//                 InviteLink = InviteLink,
//                 ExtraOwners = ExtraOwners,
//                 ShopUrl = ShopUrl,
//                 Status = Status,
//                 Announcement = "None Yet"
//             };
//             using var uow = _db.GetDbContext();
//             uow.SwitchShops.Update(toadd);
//             await uow.SaveChangesAsync();
//         }
//         public SwitchShops[] GetAll()
//         {
//             using var uow = _db.GetDbContext();
//             return uow.SwitchShops.GetAll();
//         }
//     }
// }

