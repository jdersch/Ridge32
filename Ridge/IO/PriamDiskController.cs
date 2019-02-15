using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Ridge.CPU;
using Ridge.Memory;
using Ridge.IO.Disk;
using System.IO;
using Ridge.Logging;

namespace Ridge.IO
{
    /// <summary>
    /// "The Floppy Disc/Line Printer (FD/LP) controller can control two floppy
    ///  disc drives, four RS-232 ports, a Centronics/DataProducts printer
    ///  port, and a Versatec printer/plotter port.  The FD/LP communicates
    ///  with the Ridge cpu via Device Control Blocks (DCB's) in Ridge main
    ///  memory.  Each DCB is 32 bytes long and the eight control blocks for
    ///  the FD/LP are laid out as follows:
    ///  
    ///    Main Memory                                  Unit Number
    ///    Address 3C000H   +-----------------+
    ///                    0|   Terminal 0    |             0
    ///                  20H|   Terminal 1    |             1
    ///                  40H|   Terminal 2    |             2
    ///                  60H|   Terminal 3    |             3
    ///                  80H|    Printer      |             4
    ///                  A0H|   Versatec      |             5
    ///                  C0H|  Floppy Drive 0 |             6
    ///                  E0H|    Reserved     |             7
    ///                     +-----------------+
    /// 
    /// By convention, the FD/LP is device 1.
    /// 
    /// </summary>
    public class PriamDiskController : IIODevice
    {
        public PriamDiskController(byte deviceId, RidgeSystem sys)
        {
            _sys = sys;
            _mem = sys.Memory;
            _deviceId = deviceId;

            // TODO: load from file, etc.
            _disk = new PriamDisk(Geometry.Priam142);
        }

        public bool Interrupt
        {
            get { return _interrupt; }
        }        

        public uint AckInterrupt()
        {
            _interrupt = false;
            return _ioir;
        }

        public void Boot()
        {

        }

        public void Clock()
        {

        }

        public uint Read(uint addressWord, out uint data)
        {
            //
            // "The hard disc controller returns its device number and last interrupting
            //  unit in the following format:"
            //  0         7 8        14  15 16   23 24   31
            //  | device # | 010000 | unit | status|       |
            //
            // For now we always return a status of "OK".
            data = (uint)((_deviceId << 24) | 0x00400000 | (_lastUnit << 16) | (_status << 8));
            return 0;
        }

        public uint Write(uint addressWord, uint data)
        {
            Log.Write(LogType.Verbose, LogComponent.Priam, "IO Write Address 0x{0:x} Data 0x{1:x}",
                addressWord, data);

            // Address word is not used.
            // Data word looks like:
            //   0 1   5 6    7 8            31
            //  |1|00000| unit |               |
            uint ret = 0;

            // Upper bits are as above for normal operations.
            // Check for special operations.
            if ((data & 0xfc000000) != 0x80000000)
            {
                // Special operation
                uint op = data >> 24;
                Log.Write(LogComponent.Priam, "Special op is 0x{0:x}", op);
                switch (op)
                {
                    case 0xc1:
                        // Set DCB base to the value at (0x3c13e-0x3c13f) * 256.
                        _dcbAddress = (uint)(_mem.ReadHalfWord(0x3c13e) << 8);
                        Log.Write(LogComponent.Priam, "DCB address set to 0x{0:x}", _dcbAddress);
                        break;

                    default:
                        throw new NotImplementedException(
                            String.Format("Unimplemented Priam special op 0x{0:x)", op));
                }
            }
            else
            {
                // Normal operation
                uint unit = (data & 0x03000000) >> 24;
                Log.Write(LogComponent.Priam, "Selected unit is {0}", unit);
                StartDiscOperation(unit);
            }

            return ret;
        }

        private void StartDiscOperation(uint unit)
        {
            uint dcbOffset = _dcbAddress + unit * 0x40;

            if (unit != 0)
            {
                // Assume only one unit (unit 0) at this time.
                // Post Status 0x1 -- Not Ready
                PostInterrupt(unit, 0x1);
            }
            else
            {
                Function gOrder = (Function)_mem.ReadByte(dcbOffset);
                uint ridgeAddress = _mem.ReadWord(dcbOffset + 5) >> 8;
                uint byteCount = _mem.ReadHalfWord(dcbOffset + 8);
                uint head = (uint)(_mem.ReadByte(dcbOffset + 0xd) >> 4);
                uint cyl = (uint)(((_mem.ReadByte(dcbOffset + 0xd) & 0xf) << 8) |
                                   (_mem.ReadByte(dcbOffset + 0xe)));
                uint sector = _mem.ReadByte(dcbOffset + 0xf);

                Log.Write(LogComponent.Priam, "Operation: GORDER {0} address 0x{1:x}, count 0x{2:x}, c/h/s {3}/{4}/{5}",
                    gOrder,
                    ridgeAddress,
                    byteCount,
                    cyl,
                    head,
                    sector);

                switch(gOrder)
                {
                    case Function.Read:
                        {
                            uint bytesLeft = byteCount;
                            _status = 0x0;  // OK

                            while (true)
                            {
                                PriamSector sec = _disk.GetSector(cyl, head, sector);

                                // Read sector label into DCB 0x10-0x1b
                                for (int i = 0; i < 12; i++)
                                {
                                    _mem.WriteByte((uint)(dcbOffset + i + 0x10), sec.Label[i]);
                                }

                                // Read sector data
                                uint bytesToCopy = Math.Min(bytesLeft, (uint)sec.Data.Length);
                                for (int i = 0; i < bytesToCopy; i++)
                                {
                                    _mem.WriteByte(ridgeAddress, sec.Data[i]);
                                    ridgeAddress++;
                                }

                                bytesLeft -= bytesToCopy;

                                if (bytesLeft == 0)
                                {
                                    // Done!
                                    break;
                                }

                                // Next sector
                                if (!IncrementSector(ref sector, ref head, ref cyl))
                                {
                                    _status = 0xff; // bad DCB parameter
                                    break;
                                }
                            }

                            // Zero retries
                            _mem.WriteByte(dcbOffset + 0x4, 0);

                            // We transferred all the requested bytes, go us.
                            _mem.WriteHalfWord(dcbOffset + 0xa, (ushort)byteCount);
                        }
                        break;

                    case Function.Write:
                        {
                            uint bytesLeft = byteCount;
                            _status = 0x0;  // OK

                            while (true)
                            {
                                PriamSector sec = _disk.GetSector(cyl, head, sector);

                                // Write sector label from DCB 0x10-0x1b
                                for (int i = 0; i < 12; i++)
                                {
                                    sec.Label[i] = _mem.ReadByte((uint)(dcbOffset + i + 0x10));
                                }

                                // Write sector data
                                uint bytesToCopy = Math.Min(bytesLeft, (uint)sec.Data.Length);
                                for (int i = 0; i < bytesToCopy; i++)
                                {
                                    sec.Data[i] = _mem.ReadByte(ridgeAddress);
                                    ridgeAddress++;
                                }

                                bytesLeft -= bytesToCopy;

                                if (bytesLeft == 0)
                                {
                                    // Done!
                                    break;
                                }

                                // Next sector
                                if (!IncrementSector(ref sector, ref head, ref cyl))
                                {
                                    _status = 0xff; // bad DCB parameter
                                    break;
                                }
                            }

                            // Zero retries
                            _mem.WriteByte(dcbOffset + 0x4, 0);

                            // We transferred all the requested bytes, go us.
                            _mem.WriteHalfWord(dcbOffset + 0xa, (ushort)byteCount);
                        }
                        break;

                    case Function.Format:
                        // Formats a track.
                        // We just zero out all the sectors in the given track
                        for(uint i = 0; i < _disk.Geometry.Sectors; i++)
                        {
                            PriamSector sec = _disk.GetSector(cyl, head, i);
                            Array.Clear(sec.Label, 0, sec.Label.Length);
                            Array.Clear(sec.Data, 0, sec.Data.Length);
                        }
                        break;

                    case Function.GetHighestSector:
                        //
                        // "This is the physical address of the last addressable sector
                        //  in the HDCYL, CYL, and SECTOR fields.  The value returned
                        //  in the Byte Count Transferred is the actual number of bytes
                        //  between sector marks, which is needed for interpretation
                        //  of data from the Read Header order."
                        //
                        _mem.WriteByte(dcbOffset + 0xd,
                            (byte)(((_disk.Geometry.Heads - 1) << 4) | (((_disk.Geometry.Cylinders - 1) & 0xf00) >> 8)));
                        _mem.WriteByte(dcbOffset + 0xe, (byte)((_disk.Geometry.Cylinders - 1)));
                        _mem.WriteByte(dcbOffset + 0xf, (byte)(_disk.Geometry.Sectors - 1));

                        //
                        // "Actual number of bytes between sector marks" means the number of bytes per
                        // track divided by the number of sectors, that's all.
                        int unformattedBytesPerSector = _disk.Geometry.BytesPerTrack / _disk.Geometry.Sectors;

                        _mem.WriteHalfWord(dcbOffset + 0xa, (ushort)unformattedBytesPerSector);

                        _status = 0;    // OK
                        break;

                    case Function.ReadFullSector:
                        {
                            //
                            // "Data transfer is always 1040 bytes the transfer count is ignored.  The data
                            //  read is a 12-byte data label followed by 1024 data bytes plus a 4-byte checksum."
                            //
                            // I have no idea what the checksum algorithm is.
                            //
                            Console.WriteLine("RFS CHS {0}/{1}/{2}", cyl, head, sector);
                            PriamSector sec = _disk.GetSector(cyl, head, sector);

                            // Read sector label
                            for (int i = 0; i < sec.Label.Length; i++)
                            {
                                _mem.WriteByte(ridgeAddress, sec.Label[i]);
                                ridgeAddress++;
                            }

                            // Read sector data                            
                            for (int i = 0; i < sec.Data.Length; i++)
                            {
                                _mem.WriteByte(ridgeAddress, sec.Data[i]);
                                ridgeAddress++;
                            }

                            // Calc checksum ?
                            _mem.WriteWord(ridgeAddress, 0x0);
                            _status = 0;   // OK
                        }
                        break;

                    case Function.WriteFullSector:                        
                        {
                            //
                            // "The transfer count is ignored, 1040 bytes are transferred in the same format as
                            //  Read Full Sector."
                            //
                            Console.WriteLine("WFS CHS {0}/{1}/{2}", cyl, head, sector);
                            PriamSector sec = _disk.GetSector(cyl, head, sector);

                            // Write sector label
                            for (int i = 0; i < sec.Label.Length; i++)
                            {
                                sec.Label[i] = _mem.ReadByte(ridgeAddress);
                                ridgeAddress++;
                            }

                            // Write sector data
                            for (int i = 0; i < sec.Data.Length; i++)
                            {
                                sec.Data[i] = _mem.ReadByte(ridgeAddress);
                                ridgeAddress++;
                            }

                            // Discard checksum for now...

                            _status = 0;   // OK
                        } 
                        break;

                    case Function.ReadHeader:
                        //
                        // "Priam defect log is transferred into first 9 bytes of DATA LABELS."
                        // No idea what this means, I'm just gonna punt...
                        _status = 0;
                        break;

                    default:
                        throw new NotImplementedException(
                            String.Format("Unimplemented Priam gOrder {0}", gOrder));
                }               

                // Send an interrupt
                PostInterrupt(unit, 0x0);

                // Write status back to DCB
                _mem.WriteByte(dcbOffset + 2, (byte)_status);
            }
        }

        private bool IncrementSector(ref uint sector, ref uint head, ref uint cylinder)
        {
            sector++;
            if (sector > _disk.Geometry.Sectors - 1)
            {
                sector = 0;
                head++;

                if (head > _disk.Geometry.Heads - 1)
                {
                    head = 0;
                    cylinder++;

                    if (cylinder > _disk.Geometry.Cylinders - 1)
                    {
                        // Ran off the end of the disk, stop.                        
                        return false;
                    }
                }
            }

            return true;
        }

        private void PostInterrupt(uint unit, uint status)
        {
            // The IOR word looks like:
            //  0         7 8        14  15 16   23 24   31
            //  | device # | 010000 | unit | status|       |            
            _status = status;
            _lastUnit = unit;
            _interrupt = true;
            _ioir = (uint)((_deviceId << 24) | 0x00400000 | (_lastUnit << 16) | (_status << 8));
        }

        private enum Function
        {
            Read = 0,
            Write = 1,
            Verify = 2,
            Format = 3,
            Seek = 4,
            GetHighestSector = 5,
            ReadFullSector = 6,
            WriteFullSector = 7,
            ReadHeader = 0xe,
        }

        private PriamDisk _disk;

        private bool _interrupt;
        private uint _ioir;

        private uint _lastUnit;
        private uint _status;
       

        private RidgeSystem _sys;
        private IPhysicalMemory _mem;
        private byte _deviceId;       

        private uint _dcbAddress = 0x3c100;
    }
}
