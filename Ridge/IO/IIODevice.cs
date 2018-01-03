using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ridge.IO
{
    public interface IIODevice
    {
        /// <summary>
        /// Sends an I/O read request to the specified device.
        /// The status from the read is returned; data is in the
        /// data parameter.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="status"></param>
        /// <returns></returns>
        uint Read(uint deviceData, out uint data);

        /// <summary>
        /// Sends an I/O write request to the specified device.
        /// Status from the write is returned.
        /// </summary>
        /// <param name="deviceData"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        uint Write(uint deviceData, uint data);

        void Clock();
    }
}
