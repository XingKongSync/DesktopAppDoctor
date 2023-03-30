using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Infos
{
    public class EventLogInfo : BindableBase
    {
        public DateTime TimeWritten { get; set; }
        public string Message { get; set; }
        public string Source { get; set; }
    }
}
