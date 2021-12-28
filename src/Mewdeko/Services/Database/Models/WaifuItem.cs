using System;

namespace Mewdeko.Services.Database.Models;

public class WaifuItem : DbEntity
{
    public int? WaifuInfoId { get; set; }
    public string ItemEmoji { get; set; }
    public string Name { get; set; }


    [Obsolete] public int Price { get; set; }

    [Obsolete] public int Item { get; set; }
}