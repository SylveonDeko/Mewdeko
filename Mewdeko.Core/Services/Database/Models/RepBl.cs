using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mewdeko.Core.Services.Database.Models
{
        public class RepBlacklistEntry : DbEntity
        {
            public ulong ItemId { get; set; }
        }
}
