using Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace DesktopAppDoctor
{
    class LogManagerForUI : BindableBase
    {
        private static Lazy<LogManagerForUI> _instance = new Lazy<LogManagerForUI>(() => new LogManagerForUI());
        private ObservableCollection<string> _coreLogs = new ObservableCollection<string>();
        //private ObservableCollection<string> _detectorLogs = new ObservableCollection<string>();
        private object _collctionLock = new object();


        public static LogManagerForUI Instance { get => _instance.Value; }
        public ObservableCollection<string> CoreLogs { get => _coreLogs; set => _coreLogs = value; }
        //public ObservableCollection<string> DetectorLogs { get => _detectorLogs; set => _detectorLogs = value; }


        public LogManagerForUI()
        {
            BindingOperations.EnableCollectionSynchronization(CoreLogs, _collctionLock);
            //BindingOperations.EnableCollectionSynchronization(DetectorLogs, _collctionLock);
        }

        public void AddCoreLog(string log)
        {
            AddLog(CoreLogs, log);
        }

        //public void AddDetecotrLog(string log)
        //{
        //    AddLog(DetectorLogs, log);
        //}


        private void AddLog(ObservableCollection<string> collection, string log)
        {
            lock (_collctionLock)
            {
                collection.Add(log);
                if (collection.Count > 300)
                {
                    for (int i = 0; i < 10; i++)
                    {
                        collection.RemoveAt(0);
                    }
                }
            }
        }

    }
}
