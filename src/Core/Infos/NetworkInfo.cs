using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace Core.Infos
{
    public class NetworkInfo : BindableBase
    {
        public string Name { get => _name; set => SetProperty(ref _name, value); }
        public string InterfaceType { get; set; }
        public long MaxSpeed { get => _maxSpeed; set => SetProperty(ref _maxSpeed, value); }
        public string Mac { get; set; }
        public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }
        public string IPAddress { get => _ipAddress; set => SetProperty(ref _ipAddress, value); }
        public double UploadSpeed { get => _uploadSpeed; set => SetProperty(ref _uploadSpeed, value); }
        public double DownloadSpeed { get => _downloadSpeed; set => SetProperty(ref _downloadSpeed, value); }

        private NetworkInterface _network;
        private long _lastDownload;
        private long _lastUpload;
        private DateTime _lastUpdateTime;
        private string _name;
        private bool _isEnabled;
        private string _ipAddress;

        private long _maxSpeed;
        private double _uploadSpeed;
        private double _downloadSpeed;

        public NetworkInfo(NetworkInterface network)
        {
            _network = network;
        }

        public void Update()
        {
            if (null == _network)
                return;

            Name = _network.Name;
            InterfaceType = _network.NetworkInterfaceType.ToString();
            MaxSpeed = _network.Speed;
            Mac = _network.GetPhysicalAddress().ToString();
            IsEnabled = _network.OperationalStatus == OperationalStatus.Up;

            IPInterfaceProperties ipProperties = _network.GetIPProperties();
            foreach (UnicastIPAddressInformation ipAddress in ipProperties.UnicastAddresses)
            {
                if (ipAddress.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    IPAddress = ipAddress.Address.ToString();
                    break;
                }
            }

            long currentUpload = _network.GetIPv4Statistics().BytesSent;
            long currentDownload = _network.GetIPv4Statistics().BytesReceived;

            if (_lastUpdateTime != default)
            {
                double interval = DateTime.Now.Subtract(_lastUpdateTime).TotalSeconds;
                if (interval > 0)
                {
                    double up = (currentUpload - _lastUpload) / interval;
                    double down = (currentDownload - _lastDownload) / interval;
                    if (up > 0)
                    {
                        UploadSpeed = up;
                    }
                    if (down > 0)
                    {
                        DownloadSpeed = down;
                    }
                }
            }
            _lastUpdateTime = DateTime.Now;
            _lastDownload = currentDownload;
            _lastUpload = currentUpload;
        }
    }
}
