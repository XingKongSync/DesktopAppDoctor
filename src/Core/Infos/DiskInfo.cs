using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Infos
{
    public class DiskInfo : BindableBase
    {
        private string _name;
        private long _totalSize;
        private long _freeSize;

        public string Name { get => _name; set => SetProperty(ref _name, value); }
        public long TotalSize
        {
            get => _totalSize; 
            set
            {
                SetProperty(ref _totalSize, value);
            }
        }
        public long FreeSize 
        {
            get => _freeSize;
            set
            {
                if (SetProperty(ref _freeSize, value))
                {
                    RaisePropertyChanged(nameof(UsedSize));
                }
            }
        }
        public long UsedSize { get => TotalSize - FreeSize; }
    }
}
