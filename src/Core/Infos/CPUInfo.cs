using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Infos
{
    public class CPUInfo : BindableBase
    {
        public string CPUName { get; set; }
        public string Manufacture { get; set; }
        public string MaxClockSpeed { get; set; }
    }
}
