using DomainService.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainService.Repositories
{
    public class BlocksLanguageKey: BaseEntity
    {
        public string KeyName { get; set; }
        public string Module { get; set; }
        public string Value { get; set; }
        public Dictionary<string, string> Translations { get; set; }
        public List<string> Routes { get; set; }
    }
}
