using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DemoSplicer
{
	public class BitBufferDeluxe
	{
		byte[] data { get; set; }
		int offset;

		public int OffsetInByte
		{
			get
			{
				return offset % 8;
			}
		}

		public int CurrentByteIndex
		{
			get
			{
				return offset / 8;
			}
		}

		public int BitsInByteLeft
		{
			get
			{
				return 8 - OffsetInByte;
			}
		}

		public void SetPosition(int pos)
		{
			offset = pos;
		}

		private void SeekBits(int nBits)
		{
			offset += nBits;
		}

		public BitBufferDeluxe(IList<byte> data)
		{
			this.data = data.ToArray();
			offset = 0;
		}

		public byte ReadByte()
		{
			return (byte)ReadUnsignedBits(8);
		}

		public uint ReadUnsignedBits(int nBits)
		{
			int firstBits = Math.Min(nBits, BitsInByteLeft);
			int totalBits = 0;
			uint result = data[CurrentByteIndex];
			result <<= (sizeof(uint) - firstBits - OffsetInByte);
			result >>= (sizeof(uint) - firstBits);
			nBits -= firstBits;
			totalBits = firstBits;
			SeekBits(firstBits);

			while(nBits > 0)
			{
				int bits = Math.Min(nBits, 8);
				uint newPart = data[CurrentByteIndex];
				newPart <<= (sizeof(uint) - bits);
				newPart >>= (sizeof(uint) - totalBits - bits);
				totalBits += bits;
				SeekBits(bits);
				nBits -= bits;
				result |= newPart;
			}

			return result;
		}
	}

	public class BitWriterDeluxe
	{
		List<byte> data;
	    int offset;
		const int MIN_SIZE = 16;

		public BitWriterDeluxe()
		{
			data = new List<byte>();
			data.Add(0);
		}

		public byte[] Data
		{
			get
			{
				if (offset == 0)
					return data.GetRange(0, data.Count - 1).ToArray();
				else
					return data.ToArray();
			}
		}

		public int BitsRemaining
		{
			get
			{
				return 8 - (int)offset;
			}
		}

		public bool Aligned
		{
			get
			{
				return data.Count % 16 == 0;
			}
		}

		private byte CurrentByte
		{
			get
			{
				return data[data.Count - 1];
			}
		}

		public void AlignData()
		{
			while (!Aligned)
			{
				WriteByte(0);
			}
		}

		private void AdjustOffset(int bits)
		{
			offset += bits;
			if (offset >= 8)
			{
				AddByte();
				offset -= 8;
			}
		}

		private void AddByte()
		{
			data.Add(0);
		}

		private void AddToCurrentByte(byte b, int bits, int additionOffset)
		{
			if (bits > BitsRemaining)
				throw new BitBufferOutOfRangeException();

			uint addition = b;

			addition >>= additionOffset;
			addition <<= (sizeof(uint) - bits);
			addition >>= (sizeof(uint) - offset - bits);

			data[data.Count - 1] = (byte)(data[data.Count - 1] | addition);
			AdjustOffset(bits);
		}

		public void PrintStatus()
		{
			string arr = BitConverter.ToString(data.ToArray());
			Console.WriteLine("Array: " + arr);
			Console.WriteLine("Current byte " + CurrentByte);
			Console.WriteLine("Offset: " + offset);
		}

		public void WriteRangeFromArray(IList<byte> array, int start, int last)
		{
			WriteBitsFromArray(array, start, last - start);
		}

		public void WriteBitsFromArray(IList<byte> array, int bitIndex, int count)
		{
			BitBufferDeluxe deluxe = new BitBufferDeluxe(array);
			deluxe.SetPosition(bitIndex);

			while(count > 0)
			{
				int bits = Math.Min(count, 8);
				count -= bits;
				byte newData = (byte)deluxe.ReadUnsignedBits(bits);
				WriteBits(newData, bits, 0);
			}

			/*
			int firstSegment = Math.Min(8 - bitIndex % 8, count);
			int byteIndex = bitIndex / 8;
			WriteBits(array[byteIndex], firstSegment, bitIndex % 8);
			count -= firstSegment;
			++byteIndex;
			
			while(count > 0)
			{
				if(count >= 8)
				{
					WriteByte(array[byteIndex]);
					++byteIndex;
					count -= 8;
				}
				else
				{
					WriteBits(array[byteIndex], count, 0);
					count = 0;
				}
			} */
		}

		public void WriteBits(byte b, int nBits, int additionOffset)
		{
			if(nBits > BitsRemaining)
			{
				int firstBits = BitsRemaining;
				AddToCurrentByte(b, firstBits, additionOffset);
				AddToCurrentByte(b, nBits - firstBits, additionOffset + firstBits);
			}
			else
			{
				AddToCurrentByte(b, nBits, additionOffset);
			}
		}

		public void WriteByte(byte b)
		{
			if(offset != 0)
			{
				AddToCurrentByte(b, 8 - offset, 0);
				AddToCurrentByte(b, offset, 8 - offset);
			}
			else
			{
				AddToCurrentByte(b, 8, 0);
			}
		}
	}
}
