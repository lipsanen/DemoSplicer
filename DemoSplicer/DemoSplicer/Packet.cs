using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;

namespace DemoSplicer
{
	internal class Packet
	{
		const int NET_MAX_PAYLOAD_BITS = 17;
		const int MAX_EDICT_BITS = 11;
		const int MAX_SERVER_CLASS_BITS = 9;
		const int MAX_EVENT_BITS = 9;
		const int DELTASIZE_BITS = 20;
		
		public static int GetNetMSGBits(int networkProto)
		{
			int bits;
			if (networkProto == 14)
			{
				bits = 5;
			}
			else
				bits = 6;

			return bits;
		}

		public static void PacketBreakDown(SourceDemoInfo info, byte[] data)
		{
			PacketReader reader = new PacketReader(info, data);
			reader.MessageTypeRead += (s, e) =>
			{
				Console.Write("{0} ", e.Message);
			};

			Console.Write("Messages: ");
			reader.Parse();
			Console.WriteLine();
		}

		public static byte[] GetPacketDataWithTypes(SourceDemoInfo info, byte[] data, IEnumerable<int> allowedFruits)
		{
			PacketReader reader = new PacketReader(info, data);
			BitWriterDeluxe writer = new BitWriterDeluxe();

			reader.MessageRead += (s, e) =>
			{
				if (allowedFruits.Contains(e.Type))
				{
					writer.MoveBitsIn(e.DataBitWriter);
				}
			};

			reader.Parse();
			var newData = writer.Data;

			if (PacketFormatValid(info, newData))
				return newData;
			else
				return data;
		}

		public static byte[] GetPacketDataWithoutType(SourceDemoInfo info, byte[] data, IEnumerable<int> forbiddenFruits)
		{
			PacketReader reader = new PacketReader(info, data);
			BitWriterDeluxe writer = new BitWriterDeluxe();

			reader.MessageRead += (s, e) =>
			{
				if (!forbiddenFruits.Contains(e.Type))
				{
					writer.MoveBitsIn(e.DataBitWriter);
				}
			};

			reader.Parse();
			var newData = writer.Data;

			if (PacketFormatValid(info, newData))
				return newData;
			else
				return data;
		}

		public static SignOnState GetSignonType(SourceDemoInfo info, byte[] data)
		{
			PacketReader reader = new PacketReader(info, data);
			SignOnState state = SignOnState.None;

			reader.MessageRead += (s, e) =>
			{
				if (e.Type == 26)
				{
					reader.Stop();
					BitBuffer bb = new BitBuffer(e.WithoutType);
					state = GetSignonState(bb);
				}
			};

			reader.Parse();
			return state;
		}

		public static bool HasMSGType(SourceDemoInfo info, byte[] data, int type)
		{
			PacketReader reader = new PacketReader(info, data);
			HashSet<int> messages = new HashSet<int>();

			reader.MessageTypeRead += (s, e) => { messages.Add(e.Message); };
			reader.Parse();

			return messages.Contains(type);
		}

		public static bool PacketFormatValid(SourceDemoInfo info, byte[] data)
		{
			PacketReader reader = new PacketReader(info, data);
			bool readCorrectly = false;

			reader.PacketSuccessfullyRead += (s, e) => { readCorrectly = true; };
			reader.Parse();

			return readCorrectly;
		}

		public static int FindDeltaPacketTick(SourceDemoInfo info, byte[] data)
		{
			PacketReader reader = new PacketReader(info, data);
			int deltaTick = -1;

			reader.MessageRead += (s, e) =>
			{
				if (e.Type == 26)
				{
					reader.Stop();
					BitBuffer bb = new BitBuffer(e.WithoutType);
					deltaTick = GetDeltaTick(bb);
				}
			};

			reader.Parse();
			return deltaTick;
		}

		public static bool IsRealDelta(SourceDemoInfo info, byte[] data)
		{
			PacketReader reader = new PacketReader(info, data);
			bool isRealDelta = false;

			reader.MessageRead += (s, e) =>
			{
				if (e.Type == 26)
				{
					reader.Stop();
					BitBuffer bb = new BitBuffer(e.WithoutType);
					isRealDelta = !IsDeltaBaseline(bb);
				}
			};

			reader.Parse();
			return isRealDelta;
		}

		public static int FindDeltaFrom(SourceDemoInfo info, byte[] data)
		{
			PacketReader reader = new PacketReader(info, data);
			int deltaFrom = -1;

			reader.MessageRead += (s, e) =>
			{
				if (e.Type == 26)
				{
					reader.Stop();
					BitBuffer bb = new BitBuffer(e.WithoutType);
					deltaFrom = DeltaFrom(bb);
				}
			};

			reader.Parse();
			return deltaFrom;
		}

		public static int FindTick(SourceDemoInfo info, byte[] data)
		{
			PacketReader reader = new PacketReader(info, data);
			int tick = -1;

			reader.MessageRead += (s, e) =>
			{
				if(e.Type == 3)
				{
					reader.Stop();
					BitBuffer bb = new BitBuffer(e.WithoutType);
					tick = GetTick(bb);
				}
			};

			reader.Parse();
			return tick;
		}

		private static int GetTick(BitBuffer bb)
		{
			return bb.ReadBits(32);
		}

		private static SignOnState GetSignonState(BitBuffer bb)
		{
			return (SignOnState)bb.ReadBits(8);
		}

		private static int GetDeltaTick(BitBuffer bb)
		{
			bb.ReadBits(MAX_EDICT_BITS);
			var isDelta = bb.ReadBoolean();

			if (isDelta)
				return bb.ReadInt32();
			bool baseline = bb.ReadBoolean(); // Is baseline?
			bb.ReadBits(MAX_EDICT_BITS);
			var b = (int)bb.ReadUnsignedBits(DELTASIZE_BITS);
			bb.ReadBoolean();
			bb.SeekBits(b);

			return -1;
		}

		private static bool IsSoundReliable(BitBuffer bb)
		{
			var r = bb.ReadBoolean();
			var sounds = r ? 1 : bb.ReadBits(8);
			var b = r ? (int)bb.ReadUnsignedBits(8) : (int)bb.ReadUnsignedBits(16);
			bb.SeekBits(b);

			return r;
		}

		public static void WriteDeleterPacket(BinaryWriter writer, int tick)
		{
			// Packet stuff
			writer.Write((byte)ParsedDemo.MessageType.Packet);
			writer.Write(tick);
			writer.Write(new byte[0x54]);

			// svc_packetentities
			var bw2 = new BitWriterDeluxe();
			bw2.WriteUnsignedBits(26, 6);
			bw2.WriteUnsignedBits(4095, MAX_EDICT_BITS);
			bw2.WriteBoolean(false);
			//bw.WriteBits(deltaFrom, 32);
			bw2.WriteBoolean(true);
			bw2.WriteUnsignedBits(4095, MAX_EDICT_BITS);
			var bw3 = GetDeleterPacketEntitiesData();
			bw2.WriteUnsignedBits((uint)bw3.BitsWritten, DELTASIZE_BITS);
			bw2.MoveBitsIn(bw3);
			bw2.WriteBoolean(false);

			var bytes = bw2.Data;
			writer.Write(bytes.Count());
			writer.Write(bytes);
		}

		private static BitWriterDeluxe GetDeleterPacketEntitiesData()
		{
			BitWriterDeluxe bw = new BitWriterDeluxe();
			for(int i=0; i < 4095; ++i)
			{
				bw.WriteUnsignedBits(0, 6);
				bw.WriteBoolean(false);
				bw.WriteBoolean(true);
			}

			return bw;
		}

		private static int DeltaFrom(BitBuffer bb)
		{
			bb.ReadBits(MAX_EDICT_BITS);
			var isDelta = bb.ReadBoolean();
			int deltaFrom = -1;
			if (isDelta)
				deltaFrom = bb.ReadInt32();
			bool baseline = bb.ReadBoolean(); // Is baseline?
			bb.ReadBits(MAX_EDICT_BITS);
			var b = (int)bb.ReadUnsignedBits(DELTASIZE_BITS);
			bb.ReadBoolean();
			bb.SeekBits(b);

			return deltaFrom;
		}

		private static bool IsDeltaBaseline(BitBuffer bb)
		{
			bb.ReadBits(MAX_EDICT_BITS);
			var isDelta = bb.ReadBoolean();
			int deltaFrom = -1;
			if (isDelta)
				deltaFrom = bb.ReadInt32();
			bool baseline = bb.ReadBoolean(); // Is baseline?
			bb.ReadBits(MAX_EDICT_BITS);
			var b = (int)bb.ReadUnsignedBits(DELTASIZE_BITS);
			bb.ReadBoolean();
			bb.SeekBits(b);

			return baseline;
		}



		public enum SignOnState : byte
		{
			[Description("No state yet! About to connect.")]
			None = 0,
			[Description("Client challenging the server with all OOB packets.")]
			Challenge = 1,
			[Description("Client has connected to the server! Netchans ready.")]
			Connected = 2,
			[Description("Got serverinfo and stringtables.")]
			New = 3,
			[Description("Recieved signon buffers.")]
			Prespawn = 4,
			[Description("Ready to recieve entity packets.")]
			Spawn = 5,
			[Description("Fully connected, first non-delta packet recieved.")]
			Full = 6,
			[Description("Server is changing level.")]
			ChangeLevel = 7
		}

		private delegate void MsgHandler(BitBuffer bb);

	}
}
