using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Windows.Devices.Spi;

using CP.Windows.IoT.Assets;

namespace CP.Windows.IoT.Components
{
    public class LEDPanel
    {
        #region Variables
        private SpiDevice Spi;
        private byte[][] Buffer;
        #endregion

        #region Properties
        public int Rows
        {
            get;
            private set;
        }

        public int Columns
        {
            get;
            private set;
        }

        public int Segments
        {
            get;
            private set;
        }
        #endregion

        #region Creation
        public LEDPanel(SpiDevice spiDevice, int segments)
        {
            if (spiDevice == null)
                throw new ArgumentNullException("spiDevice");

            this.Segments = segments;
            this.Rows = 8;
            this.Columns = this.Segments * 8;
            this.Spi = spiDevice;
            this.Buffer = new byte[this.Rows][];
            for (int r = 0; r < this.Rows; r++)
            {
                this.Buffer[r] = new byte[this.Segments + 1]; // plus 1 to provide row select
                this.Buffer[r][this.Segments] = (byte)(1 << this.Rows - 1 - r); // row select byte
            }
        }
        #endregion

        #region Display Logic        
        /// <summary>
        /// Clears the display
        /// </summary>
        public void Clear(byte value = 0)
        {
            var buffer = this.Buffer;
            for (int r = 0; r < buffer.Length; r++)
                for (int s = 0; s < buffer[r].Length-1; s++)
                    buffer[r][s] = value;
        }

        // Converts row and colum to actual bitmap bit and turn it off/on
        public void Plot(int col, int row, bool isOn, byte[][] buffer = null)
        {
            buffer = buffer ?? this.Buffer;
            int sement = (col >> 3) % this.Segments; // devide by 8
            int colBitIndex = col % 8;
            byte colBit = (byte)(1 << colBitIndex);
            row = row % this.Rows;
            if (isOn)
                buffer[row][sement] = (byte)((buffer[row][sement] & ~colBit) | ((byte)255 & colBit));
            else
                buffer[row][sement] = (byte)(buffer[row][sement] & ~colBit);
        }

        /// <summary>
        /// Gets a rendered dispay buffer of the specified string
        /// </summary>
        /// <param name="message">The message</param>
        /// <returns>The buffer</returns>
        public byte[][] Render(string message)
        {
            Font8x8.Character character = null;
            message = (message ?? string.Empty);

            var buffer = CreateBuffer(message.Length);

            for (int charIndex = 0; charIndex < message.Length; charIndex++)
            {
                var characterToRender = message[charIndex];
                character = Font8x8.GetCharacter(characterToRender);
                if (character == null)
                    character = Font8x8.GetCharacter(' ');
                if (character == null)
                    throw new NotSupportedException(string.Format("Character '{0}' is not supported", characterToRender));

                for (var r = 0; r < this.Rows; r++)
                    buffer[r][charIndex] = character.Bitmap[r];
            }

            return buffer;
        }
        #endregion

        #region Utility Methods
        private byte[][] CreateBuffer(int segments)
        {
            var buffer = new byte[this.Rows][];
            for (int r = 0; r < this.Rows; r++)
                buffer[r] = new byte[segments];
            return buffer;
        }

        private bool BitRead(byte data, int position)
        {
            return (data & (byte)(1 << position)) != 0;
        }

        private void BitWrite(ref byte data, int position, bool value)
        {
            if (value)
                data |= (byte)(1 << position);
            else
                data &= (byte)(~(1 << position));
        }
        #endregion

        public void Display(byte[][] buffer, int bufferColumnOffset = 0)
        {
            var bufferSegments = buffer[0].Length;
            var bufferColumns = bufferSegments * 8;

            do
            {
                var startingSegment = bufferColumnOffset >> 3;
                var localOffset = bufferColumnOffset % 8;
                byte rightMask = (byte)(~(255 << bufferColumnOffset % 8));
                byte leftMask = (byte)(~rightMask);

                for (int r = 0; r < this.Rows; r++)
                {
                    for (int s = 0, bufferSegment = startingSegment; s < this.Segments; s++, bufferSegment++)
                    {
                        byte leftByte = 0;
                        byte rightByte = 0;
                        if (bufferSegment < buffer[r].Length)
                            leftByte = (byte)(buffer[r][bufferSegment] << localOffset);
                        if (bufferSegment + 1 < buffer[r].Length)
                            rightByte = (byte)(buffer[r][bufferSegment + 1] >> (8 - localOffset));

                        this.Buffer[r][s] = (byte)((leftMask & leftByte) | (rightMask & rightByte));
                    }
                }
                this.WriteToPanel();
            } while (++bufferColumnOffset < bufferColumns - this.Columns);
        }

        public void Display()
        {
            this.WriteToPanel();
        }

        private void WriteToPanel()
        {  
            for (var row = 0; row < this.Buffer.Length; row++)
                this.Spi.Write(this.Buffer[row]);

            ////-- Draw one character of the message --
            //// Each character is only 5 columns wide, but I loop two more times to create 2 pixel space betwen characters
            //for (int col = 0; col < 8; col++)
            //{
            //    for (int row = 0; row < 8; row++)
            //    {
            //        Plot(ColCount - 1, row, character.GetBit(row, col));
            //    }
            //    RefreshDisplay();

            //    //-- Shift the bitmap one column to left --
            //    for (int row = 0; row < 8; row++)
            //    {
            //        for (int zone = 0; zone < ZoneCount; zone++)
            //        {
            //            var index = row * ZoneCount + zone;

            //            // This right shift would show as a left scroll on display because leftmost column is represented by least significant bit of the byte.
            //            Bitmap[index] = (byte)(Bitmap[index] >> 1);

            //            // Roll over lowest bit from the next zone as highest bit of this zone.
            //            if (zone < MaxZoneIndex)
            //                BitWrite(ref Bitmap[index], 7, BitRead(Bitmap[index + 1], 0));
            //        }
            //    }
            //}
        }
    }
}
