using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mewdeko.Core.Services.Database.Models
{
    public class Starboard : DbEntity
    {
        public ulong MessageId { get; set; }
        public ulong PostId { get; set; }
    }
}
