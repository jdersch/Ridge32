using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ridge.IO
{
    public class IOBus
    {
        public IOBus()
        {
            // Initialize the map.  Entries are null by default, indicating
            // no device at the specified address.
            _deviceMap = new IIODevice[256];
        }

        public void RegisterDevice(uint address, IIODevice device)
        {
            if (_deviceMap[address] != null)
            {
                throw new InvalidOperationException(
                    String.Format("Duplicate I/O address at {0}.",
                        address));
            }


            _deviceMap[address] = device;
        }

        public uint Read(uint address, uint deviceData, out uint data)
        {
            if (_deviceMap[address] == null)
            {
                // No device mapped here, return "3" to indicate
                // a device timeout.
                // Bit 31 : "0" is OK, "1" is I/O device not ready to accept command.
                // Bit 30 : "0" is OK, "1" is device timed out and did not respond.
                data = 0;
                return 0x3;
            }
            else
            {
                // Let the device deal with this.
                return _deviceMap[address].Read(deviceData, out data);
            }
        }

        public uint Write(uint address, uint deviceData, uint data)
        {
            if (_deviceMap[address] == null)
            {
                // No device mapped here, return "3" to indicate
                // a device timeout.
                // Bit 31 : "0" is OK, "1" is I/O device not ready to accept command.
                // Bit 30 : "0" is OK, "1" is device timed out and did not respond.
                data = 0;
                return 0x3;
            }
            else
            {
                // Let the device deal with this.
                return _deviceMap[address].Write(deviceData, data);
            }
        }


        private IIODevice[] _deviceMap;
    }
}
