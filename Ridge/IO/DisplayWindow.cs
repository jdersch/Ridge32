using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;


namespace Ridge.IO
{
    //
    // Provides a console using only Winforms.  This is a fallback for when other methods are not available.
    // It is not fast.
    //
    public class DisplayWindow
    {
        public DisplayWindow()
        {
            _lock = new ReaderWriterLockSlim();

            _displayData = new byte[128 * 1024];

            InvokeDisplayThread();
        }

        public bool KeyAvailable
        {
            get { return _keyAvailable; }
        }

        public byte GetKey()
        {
            _keyAvailable = false;
            return _keyChar;
        }

        public void Render(uint[] buffer)
        {
            _lock.EnterWriteLock();

            //Buffer.BlockCopy(buffer, 0, _displayData, 0, 1024 * 128);

            
            uint dispIndex = 0;
            for(int i=0;i<buffer.Length;i++)
            {
                _displayData[dispIndex++] = (byte)(buffer[i] >> 24);
                _displayData[dispIndex++] = (byte)(buffer[i] >> 16);
                _displayData[dispIndex++] = (byte)(buffer[i] >> 8);
                _displayData[dispIndex++] = (byte)buffer[i];
            }  

            _dispBox.Invalidate();
            _lock.ExitWriteLock();
        }

        private void InvokeDisplayThread()
        {
            _displayThread = new System.Threading.Thread(new System.Threading.ThreadStart(DisplayThread));
            _displayThread.Start();

            _initEvent = new ManualResetEvent(false);

            WaitHandle[] handles = { _initEvent };

            WaitHandle.WaitAll(handles);

            Thread.Sleep(500);
        }

        private void DisplayThread()
        {
            _display = new Form();
            _display.CreateControl();
            _display.BackColor = Color.Black;
            _display.Text = "Ridge32";
            _display.ControlBox = false;
            _display.ClientSize = new Size(768, 1024);
            _display.SizeGripStyle = SizeGripStyle.Hide;
            _display.WindowState = FormWindowState.Normal;
            _display.KeyPreview = true;
            _display.KeyUp += new KeyEventHandler(OnKeyUp);
            _display.KeyDown += new KeyEventHandler(OnKeyDown);

            //_display.MouseWheel += new MouseEventHandler(OnMouseWheel);


            _buffer = new Bitmap(768, 1024, PixelFormat.Format1bppIndexed);

            _dispBox = new PictureBox();
            _dispBox.Image = _buffer;
            _dispBox.Size = new Size(768, 1024);
            _dispBox.Cursor = Cursors.Cross;
            _dispBox.Paint += new PaintEventHandler(OnPaint);
            _dispBox.Enabled = false;

            //_dispBox.MouseDown += new MouseEventHandler(OnMouseDown);
            //_dispBox.MouseUp += new MouseEventHandler(OnMouseUp);
            //_dispBox.MouseMove += new MouseEventHandler(OnMouseMove);

            _display.Controls.Add(_dispBox);

            _displayRect = new Rectangle(0, 0, 768, 1024);

            _initEvent.Set();

            _display.ShowDialog();

        }

        void OnKeyDown(object sender, KeyEventArgs e)
        {
            
        }

        void OnKeyUp(object sender, KeyEventArgs e)
        {
            _keyAvailable = true;
            _keyChar = (byte)e.KeyValue;
        }        

        private void OnPaint(object sender, PaintEventArgs e)
        {
            _lock.EnterReadLock();
            BitmapData data = _buffer.LockBits(_displayRect, ImageLockMode.WriteOnly, PixelFormat.Format1bppIndexed);

            IntPtr ptr = data.Scan0;
            System.Runtime.InteropServices.Marshal.Copy(_displayData, 0, ptr, 96 * 1024);

            _buffer.UnlockBits(data);
            _lock.ExitReadLock();
        }

        private System.Threading.Thread _displayThread;
        private Form _display;
        private Bitmap _buffer;
        private PictureBox _dispBox;
        private Rectangle _displayRect;
        private byte[] _displayData;

        private ManualResetEvent _initEvent;
        private ReaderWriterLockSlim _lock;

        private bool _keyAvailable;
        private byte _keyChar;

    }
}
