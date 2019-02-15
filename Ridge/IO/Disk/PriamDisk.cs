using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ridge.IO.Disk
{
    /// <summary>
    /// Represents a single sector on a Priam disk.
    /// This consists of a 12-byte label, 1024
    /// bytes of data, and a 4-byte checksum.
    /// </summary>
    public class PriamSector
    {
        public PriamSector(Stream s) : this()
        {

        }

        public PriamSector()
        {
            _label = new byte[12];
            _data = new byte[1024];
            _checksum = new byte[4];
        }

        public byte[] Label
        {
            get { return _label; }
        }

        public byte[] Data
        {
            get { return _data; }
        }

        public byte[] Checksum
        {
            get { return _checksum; }
        }

        private byte[] _label;
        private byte[] _data;
        private byte[] _checksum;
    }

    /// <summary>
    /// Represents a disk geometry in terms of cylinders, heads, and sectors, and the
    /// number of bytes per track (unformatted).
    /// </summary>
    public struct Geometry
    {
        public Geometry(int cylinders, int heads, int sectors, int bytesPerTrack)
        {
            Cylinders = cylinders;
            Heads = heads;
            Sectors = sectors;
            BytesPerTrack = bytesPerTrack;
        }

        public readonly int Cylinders;
        public readonly int Heads;
        public readonly int Sectors;
        public readonly int BytesPerTrack;

        public static Geometry Priam142 = new Geometry(1121, 7, 18, 20160);
        public static Geometry Priam60 = new Geometry(1121, 3, 18, 20160);
    }

    public class PriamDisk
    {
        public PriamDisk(Geometry g)
        {
            _geometry = g;
            _sectors = new PriamSector[g.Cylinders, g.Heads, g.Sectors];

            for (int cyl = 0; cyl < g.Cylinders; cyl++)
            {
                for (int head = 0; head < g.Heads; head++)
                {
                    for (int sector = 0; sector < g.Sectors; sector++)
                    {
                        _sectors[cyl, head, sector] = new PriamSector();
                    }
                }
            }
        }

        public Geometry Geometry
        {
            get { return _geometry; }
        }

        public PriamSector GetSector(uint cylinder, uint head, uint sector)
        {
            return _sectors[cylinder, head, sector];
        }

        private Geometry _geometry;
        private PriamSector[,,] _sectors;
    }
}
