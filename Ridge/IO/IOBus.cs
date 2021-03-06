﻿using Ridge.Logging;
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
            _registeredDevices = new List<IIODevice>();
        }

        /// <summary>
        /// Adds a new device to the bus.
        /// The order the devices are added defines the interrupt priority
        /// -- the first added device has the highest priority, the last
        /// has the lowest.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="device"></param>
        public void RegisterDevice(uint address, IIODevice device)
        {
            if (_deviceMap[address] != null)
            {
                throw new InvalidOperationException(
                    String.Format("Duplicate I/O address at {0}.",
                        address));
            }

            _registeredDevices.Add(device);
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
                Log.Write(LogComponent.IOBus, "Nonexistent device {0:x} on read.", address);
                data = 0;
                return 0x2;
            }
            else
            {
                // Let the device deal with this.
                Log.Write(LogComponent.IOBus, "IO Read: Address 0x{0:x} DeviceData 0x{1:x}", address, deviceData);
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
                Log.Write(LogComponent.IOBus, "Nonexistent device {0:x} on write.", address);
                data = 0;
                return 0x2;
            }
            else
            {
                // Let the device deal with this.
                Log.Write(LogComponent.IOBus, "IO Write: Address 0x{0:x} DeviceData 0x{1:x} Data 0x{2:x}", address, deviceData, data);
                return _deviceMap[address].Write(deviceData, data);
            }
        }

        /// <summary>
        /// Returns the first device in the interrupt chain
        /// (from the highest to the lowest) that requesting an interrupt.
        /// Returns null if no devices are currently requesting an interrupt.
        /// </summary>
        /// <returns></returns>
        public IIODevice InterruptRequested()
        {
            for(int i=0;i<_registeredDevices.Count;i++)
            {
                if (_registeredDevices[i].Interrupt)
                {
                    return _registeredDevices[i];
                }
            }
            return null;
        }


        private List<IIODevice> _registeredDevices;
        private IIODevice[] _deviceMap;
    }
}
