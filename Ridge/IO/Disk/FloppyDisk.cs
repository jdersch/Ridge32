using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ridge.IO.Disk
{
    /// <summary>
    /// Presents data for a floppy disk, organized by cylinder, head, and sector.
    /// </summary>
    public class FloppyDisk
    {
        public FloppyDisk(Stream s)
        {            
            _tracks = new Track[2, 77];
            LoadTracks(s);
        }

        public byte[] ReadSector(int cylinder, int head, int sector)
        {
            return _tracks[head, cylinder].ReadSector(sector).Data;
        }


        private void LoadTracks(Stream s)
        {
            // For now, we assume a double-density, double-sided floppy.
            // This looks like:
            // Track 0, Head 0 - single-density, 26 sectors of 128 bytes
            // All other Tracks/Heads - 16 sectors of 512 bytes.

            for (int cyl = 0; cyl < 77; cyl++)
            {
                for (int head = 0; head < 2; head++)
                {
                    if (head == 0 && cyl == 0)
                    {
                        _tracks[head, cyl] = new Track(26, 128, s);
                    }
                    else
                    {
                        _tracks[head, cyl] = new Track(16, 512, s);
                    }
                }
            }
        }

        private Track[,] _tracks;
    }

    /// <summary>
    /// Represents a single track's worth of sectors
    /// </summary>
    public class Track
    {
        public Track(int sectors, int sectorSize)
        {
            _sectors = new Sector[sectors];

            for(int i=0;i<sectors;i++)
            {
                _sectors[i] = new Sector(sectorSize);
            }
        }

        public Track(int sectors, int sectorSize, Stream s)
        {
            _sectors = new Sector[sectors];

            for (int i = 0; i < sectors; i++)
            {
                _sectors[i] = new Sector(sectorSize, s);
            }
        }

        public Sector ReadSector(int sector)
        {
            return _sectors[sector];
        }

        private Sector[] _sectors;
    }

    public class Sector
    {
        public Sector(int sectorSize)
        {
            _data = new byte[sectorSize];
        }

        public Sector(int sectorSize, Stream s)
        {
            _data = new byte[sectorSize];

            int read = s.Read(_data, 0, sectorSize);

            if (read != sectorSize)
            {
                throw new InvalidOperationException("Short read on sector init.");
            }
        }

        public byte[] Data
        {
            get { return _data; }
        }

        private byte[] _data;
    }
}
