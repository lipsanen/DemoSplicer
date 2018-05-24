using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DemoSplicer
{
	public class MessageReceivedEventArgs
	{
		public MessageReceivedEventArgs(int message, BitBuffer buffer)
		{
			Message = message;
			Buffer = buffer;
		}

		public int Message { get; set; }
		public BitBuffer Buffer { get; set; }
	}

	public class ExceptionEventArgs
	{
		public ExceptionEventArgs(Exception e)
		{
			this.e = e;
		}

		public Exception e { get; set; }
	}

	public class DataReadEventArgs
	{
		public DataReadEventArgs(byte[] data, BitWriterDeluxe dataBitWriter, byte[] withoutType, BitWriterDeluxe withoutTypeBitWriter, int type)
		{
			Data = data;
			DataBitWriter = dataBitWriter;
			WithoutType = withoutType;
			WithoutTypeBitWriter = withoutTypeBitWriter;
			Type = type;
		}

		public byte[] Data { get; set; }
		public BitWriterDeluxe DataBitWriter { get; set; }
		public byte[] WithoutType { get; set; }
		public BitWriterDeluxe WithoutTypeBitWriter { get; set; }
		public int Type { get; set; }
	}

	public class PacketReader
	{
		SourceDemoInfo DemoInfo { get; set; }
		byte[] Data { get; set; }

		public PacketReader(SourceDemoInfo demoInfo, byte[] data)
		{
			DemoInfo = demoInfo;
			Data = data;
			InitHandlers();
			ShouldStop = false;
		}

		public event EventHandler<MessageReceivedEventArgs> MessageTypeRead;
		public event EventHandler<ExceptionEventArgs> ExceptionThrown;
		public event EventHandler<DataReadEventArgs> MessageRead;
		public event EventHandler<EventArgs> PacketSuccessfullyRead;

		bool ShouldStop { get; set; }

		public void Stop()
		{
			ShouldStop = true;
		}

		public void Parse()
		{
			try
			{
				int type = 0;
				var bb = new BitBuffer(Data);
				while (bb.BitsLeft > NetMessageBits && !ShouldStop)
				{
					var dataBitWriter = new BitWriterDeluxe();
					var withoutTypeBitWriter = new BitWriterDeluxe();
					int startIndex = bb.CurrentBit;

					type = (int)bb.ReadUnsignedBits(NetMessageBits);
					MessageTypeRead?.Invoke(this, new MessageReceivedEventArgs(type, bb));
					int withoutTypeIndex = bb.CurrentBit;

					MsgHandler handler;
					if (Handlers.TryGetValue((uint)type, out handler))
					{
						handler(bb);
						dataBitWriter.WriteRangeFromArray(Data, startIndex, bb.CurrentBit);
						withoutTypeBitWriter.WriteRangeFromArray(Data, withoutTypeIndex, bb.CurrentBit);

						var bytes = dataBitWriter.Data;
						var bytes2 = withoutTypeBitWriter.Data;
						MessageRead?.Invoke(this, new DataReadEventArgs(bytes, dataBitWriter, bytes2, withoutTypeBitWriter, type));
					}
					else
					{
						throw new Exception("Unknown packet type found.");
					}
				}

				PacketSuccessfullyRead?.Invoke(this, new EventArgs());
			}
			catch (Exception e)
			{
				ExceptionThrown?.Invoke(this, new ExceptionEventArgs(e));
			}
		}

		int NetMessageBits
		{
			get
			{
				int bits;
				if (DemoInfo.NetProtocol <= 14)
				{
					bits = 5;
				}
				else
					bits = 6;

				return bits;
			}

		}

		const int NET_MAX_PAYLOAD_BITS = 17;
		const int MAX_EDICT_BITS = 11;
		const int MAX_SERVER_CLASS_BITS = 9;
		const int MAX_EVENT_BITS = 9;
		const int DELTASIZE_BITS = 20;

		private delegate void MsgHandler(BitBuffer bb);
		private Dictionary<uint, MsgHandler> Handlers;

		void InitHandlers()
		{
			Handlers = new Dictionary<uint, MsgHandler>
			{
				{
					0, (_) => { }
				},
				{1, net_disconnect},
				{2, net_file},
				{3, net_tick},
				{4, net_stringcmd},
				{5, net_setconvar},
				{6, net_signonstate},
				{7, svc_print},
				{8, svc_serverinfo},
				{9, svc_sendtable},
				{10, svc_classinfo},
				{11, svc_setpause},
				{12, svc_createstringtable},
				{13, svc_updatestringtable},
				{14, svc_voiceinit},
				{15, svc_voicedata},
				{17, svc_sounds},
				{18, svc_setview},
				{19, svc_fixangle},
				{20, svc_crosshairangle},
				{21, svc_bspdecal},
				{23, svc_usermessage},
				{24, svc_entitymessage},
				{25, svc_gameevent},
				{26, svc_packetentities},
				{27, svc_tempentities},
				{28, svc_prefetch},
				{29, svc_menu},
				{30, svc_gameeventlist},
				{31, svc_getcvarvalue},
				{32, svc_cmdkeyvalues}
			};
		}	

		private void net_disconnect(BitBuffer bb)
		{
			bb.ReadString();
		}

		private void net_file(BitBuffer bb)
		{
			bb.ReadBits(32);
			bb.ReadString();
			bb.ReadBoolean();
			bb.ReadBoolean();
		}

		private void net_tick(BitBuffer bb)
		{
			bb.ReadBits(32);
			bb.ReadBits(16);
			bb.ReadBits(16);
		}

		private void net_stringcmd(BitBuffer bb)
		{
			bb.ReadString();
		}

		private void net_setconvar(BitBuffer bb)
		{
			int n = (int)bb.ReadUnsignedBits(8);
			while (n-- > 0)
			{
				bb.ReadString();
				bb.ReadString();
			}
		}

		private void net_signonstate(BitBuffer bb)
		{
			bb.ReadByte();
			bb.ReadBits(32);
		}

		private void svc_print(BitBuffer bb)
		{
			bb.ReadString();
		}

		private void svc_serverinfo(BitBuffer bb)
		{
			var version = bb.ReadInt16();
			bb.ReadInt32();
			bb.ReadBoolean();
			bb.ReadBoolean();
			bb.ReadInt32();
			bb.ReadInt16();
			if (version < 18)
				bb.ReadBits(32);
			else
			{
				bb.ReadInt32();
				bb.ReadInt32();
				bb.ReadInt32();
				bb.ReadInt32();
			}
			bb.ReadByte();
			bb.ReadByte();
			bb.ReadSingle();
			bb.ReadByte();

			bb.ReadString();
			bb.ReadString();
			bb.ReadString();
			bb.ReadString();
		}

		private void svc_sendtable(BitBuffer bb)
		{
			bb.ReadBoolean();
			var n = (int)bb.ReadUnsignedBits(16);
			bb.SeekBits(n);
		}

		private void svc_classinfo(BitBuffer bb)
		{
			var n = bb.ReadBits(16);
			var cc = bb.ReadBoolean();
			if (!cc)
				while (n-- > 0)
				{

					int bitCount = (int)Math.Log(n, 2) + 1;
					bb.ReadBits(bitCount);
					bb.ReadString();
					bb.ReadString();
				}
		}

		private void svc_setpause(BitBuffer bb)
		{
			bb.ReadBoolean();
		}

		private void svc_createstringtable(BitBuffer bb)
		{
			bb.ReadString(); // table name;
			var m = bb.ReadBits(16); // max entries
			bb.SeekBits((int)Math.Log(m, 2) + 1);
			var n = bb.ReadBits(20); // Length in bits
			var f = bb.ReadBoolean(); // fixed size?
			if (f)
			{
				bb.ReadBits(12); // size
				bb.ReadBits(4); // bits
			}

			bb.ReadBoolean(); // compressed
			bb.SeekBits(n);
		}

		private void svc_updatestringtable(BitBuffer bb)
		{
			bb.ReadBits(5);
			var sound = (bb.ReadBoolean() ? bb.ReadBits(16) : 1);
			var b = (int)bb.ReadUnsignedBits(20);
			bb.SeekBits(b);
		}

		private void svc_voiceinit(BitBuffer bb)
		{
			bb.ReadString();
			bb.ReadBits(8);
		}

		private void svc_voicedata(BitBuffer bb)
		{
			bb.ReadBits(8);
			bb.ReadBits(8);
			var b = (int)bb.ReadUnsignedBits(16);
			bb.SeekBits(b);
		}

		private void svc_sounds(BitBuffer bb)
		{
			var r = bb.ReadBoolean();
			var sounds = r ? 1 : bb.ReadBits(8);
			var b = r ? (int)bb.ReadUnsignedBits(8) : (int)bb.ReadUnsignedBits(16);
			bb.SeekBits(b);
		}

		private void svc_setview(BitBuffer bb)
		{
			bb.ReadBits(11);
		}

		private void svc_fixangle(BitBuffer bb)
		{
			bb.ReadBoolean();
			bb.ReadInt16();
			bb.ReadInt16();
			bb.ReadInt16();
		}

		private void svc_crosshairangle(BitBuffer bb)
		{
			bb.ReadInt16();
			bb.ReadInt16();
			bb.ReadInt16();
		}

		private void svc_bspdecal(BitBuffer bb)
		{
			var pos = bb.ReadVectorCoord();
			bb.ReadBits(9);
			if (bb.ReadBoolean())
			{
				bb.ReadBits(11);
				bb.ReadBits(12);
			}
			bb.ReadBoolean();
		}

		private void svc_usermessage(BitBuffer bb)
		{
			bb.ReadBits(8);
			var b = (int)bb.ReadUnsignedBits(11);
			bb.SeekBits(b);
		}

		private void svc_entitymessage(BitBuffer bb)
		{
			bb.ReadUnsignedBits(MAX_EDICT_BITS);
			bb.ReadBits(MAX_SERVER_CLASS_BITS);
			var b = (int)bb.ReadUnsignedBits(11);
			bb.SeekBits(b);
		}

		private void svc_gameevent(BitBuffer bb)
		{
			var b = (int)bb.ReadUnsignedBits(11);
			bb.SeekBits(b);
		}

		private void svc_packetentities(BitBuffer bb)
		{
			bb.ReadBits(MAX_EDICT_BITS);
			var isDelta = bb.ReadBoolean();
			int deltaTick;
			if (isDelta)
				deltaTick = bb.ReadInt32();
			bool baseline = bb.ReadBoolean(); // Is baseline?
			bb.ReadBits(MAX_EDICT_BITS);
			var b = (int)bb.ReadUnsignedBits(DELTASIZE_BITS);
			bb.ReadBoolean();
			bb.SeekBits(b);
		}

		private bool is_delta_baseline(BitBuffer bb)
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

		private void svc_tempentities(BitBuffer bb)
		{
			bb.ReadBits(8);
			var b = (int)bb.ReadUnsignedBits(17);
			bb.SeekBits(b);
		}

		private void svc_prefetch(BitBuffer bb)
		{
			bb.ReadBits(13);
		}

		private void svc_menu(BitBuffer bb)
		{
			bb.ReadBits(16);
			var b = (int)bb.ReadUnsignedBits(16);
			bb.SeekBits(b << 3);
		}

		private void svc_gameeventlist(BitBuffer bb)
		{
			bb.ReadBits(MAX_EVENT_BITS);
			var b = (int)bb.ReadUnsignedBits(20);
			bb.SeekBits(b);
		}

		private void svc_getcvarvalue(BitBuffer bb)
		{
			bb.ReadBits(32);
			bb.ReadString();
		}

		private void svc_cmdkeyvalues(BitBuffer bb)
		{
			var b = (int)bb.ReadUnsignedBits(32);
			bb.SeekBits(b);
		}

	}
}
