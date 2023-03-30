using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public static class UnitHelper
    {
        public static string GetStorageUnitString(double v)
        {
            string unit = "B";
            if (v < 1024)
            {
                return $"{v:#.##} {unit}";
            }
            v /= 1024;
            unit = "KB";
            if (v < 1024)
            {
                return $"{v:#.##} {unit}";
            }
            v /= 1024;
            unit = "MB";
            if (v < 1024)
            {
                return $"{v:#.##} {unit}";
            }
            v /= 1024;
            unit = "GB";
            if (v < 1024)
            {
                return $"{v:#.##} {unit}";
            }
            v /= 1024;
            unit = "TB";
            return $"{v:#.##} {unit}";
        }

        public static string GetSpeedUnit(double v)
        {
            v = v / 1000000;
            //string unit = "Mbps";
            return $"{v} Mbps";
        }

        public static string GetSpeedUnit2(double v)
        {
            string unit = "B/s";
            if (v < 1024)
            {
                return $"{v:#.##} {unit}";
            }
            v /= 1024;
            unit = "KB/s";
            if (v < 1024)
            {
                return $"{v:#.##} {unit}";
            }
            v /= 1024;
            unit = "MB/s";
            if (v < 1024)
            {
                return $"{v:#.##} {unit}";
            }
            v /= 1024;
            unit = "GB/s";
            if (v < 1024)
            {
                return $"{v:#.##} {unit}";
            }
            v /= 1024;
            unit = "TB/s";
            return $"{v:#.##} {unit}";
        }
    }
}
