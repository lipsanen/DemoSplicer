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
		private static readonly Dictionary<uint, MsgHandler> Handlers = new Dictionary<uint, MsgHandler>
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

		public static void TransferBits(BitWriterDeluxe writer, byte[] array, int current, int previous)
		{

			if (previous != current)
			{
				writer.WriteRangeFromArray(array, previous, current);
				
			}
		}

		public static byte[] GetPacketDataWithoutSound(byte[] data)
		{
			try
			{
				int type = 0;
				int previous = 0;
				var bw = new BitWriterDeluxe();
				var bb = new BitBuffer(data);
				while (bb.BitsLeft > 6)
				{
					type =	(int)bb.ReadUnsignedBits(6);
					MsgHandler handler;
					if (Handlers.TryGetValue((uint)type, out handler))
					{
						handler(bb);
						if(type != 17)
							TransferBits(bw, data, bb.CurrentBit, previous);
						previous = bb.CurrentBit;
					}
					else
					{
						throw new Exception("Couldn't find handler for message type");
					}
				}

				bb.SeekBits(bb.BitsLeft);
				TransferBits(bw, data, bb.CurrentBit, previous);

				var newData = bw.Data;

				if (TryToReadPacket(newData))
					return bw.Data;
				else
					throw new Exception("Written packet data was invalid.");
			}
			catch(Exception e)
			{
				Console.WriteLine(e.Message);
				return data;
			}
		}

		public static SignOnState GetSignonType(byte[] data)
		{
			try
			{
				var bb = new BitBuffer(data);
				var type = bb.ReadBits(6);
				return get_signonstate(bb);
			}
			catch{ }

			return SignOnState.None;
		}

		public static bool HasMSGType(byte[] data, int id)
		{
			try
			{
				var bb = new BitBuffer(data);
				while (bb.BitsLeft > 6)
				{
					var type = bb.ReadBits(6);

					if (type == id)
					{
						return true;
					}
					else
					{
						MsgHandler handler;
						if (Handlers.TryGetValue((uint)type, out handler))
						{
							handler(bb);
						}
					}
				}

			}
			catch { }

			return false;
		}

		public static bool TryToReadPacket(byte[] data)
		{
			List<int> msgs = new List<int>();
			try
			{
				var type = 0;
				var bb = new BitBuffer(data);
				while (bb.BitsLeft > 6)
				{
					type = bb.ReadBits(6);
					msgs.Add(type);
					MsgHandler handler;
					if (Handlers.TryGetValue((uint)type, out handler))
					{
						handler(bb);
					}
					else
					{

						/*Console.WriteLine("Failed.");
						
						foreach(int msg in msgs)
						{
							Console.Write(msg + " ");
						}
						Console.WriteLine(); */
						return false;
					}
				}

				return true;
			}
			catch
			{
				/*Console.WriteLine("Threw.");
				
				foreach (int msg in msgs)
				{
					Console.Write(msg + " ");
				}
				Console.WriteLine(); */
				return false;
			}
		}

		public static int FindDeltaPacketTick(byte[] data)
		{
			var bb = new BitBuffer(data);
			while (bb.BitsLeft > 6)
			{
				var type = bb.ReadBits(6);

				if (type == 26)
				{
					return is_deltapacket(bb);
				}
				else
				{
					MsgHandler handler;
					if (Handlers.TryGetValue((uint)type, out handler))
					{
						handler(bb);
					}
				}
			}

			return -1;
		}

		public static int FindTick(byte[] data)
		{
			var bb = new BitBuffer(data);
			while (bb.BitsLeft > 6)
			{
				var type = bb.ReadBits(6);

				if (type == 3)
				{
					return get_tick(bb);
				}
				else
				{
					MsgHandler handler;
					if (Handlers.TryGetValue((uint)type, out handler))
					{
						handler(bb);
					}
				}
			}

			return -1;
		}

		private static int get_tick(BitBuffer bb)
		{
			return bb.ReadBits(32);
		}

		private static SignOnState get_signonstate(BitBuffer bb)
		{
			return (SignOnState)bb.ReadBits(8);
		}

		private static int is_deltapacket(BitBuffer bb)
		{
			bb.SeekBits(11);
			var d = bb.ReadBoolean();
			if (d)
			{
				int rval = bb.ReadBits(32);
				return rval;
			}

			return -1;
		}

		private static void net_disconnect(BitBuffer bb)
		{
			bb.ReadString();
		}

		private static void net_file(BitBuffer bb)
		{
			bb.ReadBits(32);
			bb.ReadString();
			bb.ReadBoolean();
			bb.ReadBoolean();
		}

		private static void net_tick(BitBuffer bb)
		{
			bb.ReadBits(32);
			bb.ReadBits(16);
			bb.ReadBits(16);
		}

		private static void net_stringcmd(BitBuffer bb)
		{
			bb.ReadString();
		}

		private static void net_setconvar(BitBuffer bb)
		{
			var n = bb.ReadBits(8);
			while (n-- > 0)
			{
				bb.ReadString();
				bb.ReadString();
			}
		}

		private static void net_signonstate(BitBuffer bb)
		{
			bb.ReadByte();
			bb.ReadBits(32);
		}

		private static void svc_print(BitBuffer bb)
		{
			bb.ReadString();
		}

		private static void svc_serverinfo(BitBuffer bb)
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

		private static void svc_sendtable(BitBuffer bb)
		{
			bb.ReadBoolean();
			var n = (int)bb.ReadUnsignedBits(16);
			bb.SeekBits(n);
		}

		private static void svc_classinfo(BitBuffer bb)
		{
			var n = bb.ReadBits(16);
			var cc = bb.ReadBoolean();
			if (!cc)
				while (n-- > 0)
				{
					bb.ReadBits((int)Math.Log(n, 2) + 1);
					bb.ReadString();
					bb.ReadString();
				}
		}

		private static void svc_setpause(BitBuffer bb)
		{
			bb.ReadBoolean();
		}

		const int NET_MAX_PAYLOAD_BITS = 17;

		static int Q_log2(int val)
		{
			int answer = 0;
			while ((val>>=1) != 0)
				answer++;
			return answer;
		}

		private static void svc_createstringtable(BitBuffer bb)
		{
			if(bb.ReadByte() == 58)
			{
				bb.ReadByte();
			}

			bb.ReadString();
			var maxEntries = bb.ReadUInt16();
			int encodeBits = (int)bb.ReadUnsignedBits(Q_log2(maxEntries));
			var numEntries = (int)bb.ReadUnsignedBits(encodeBits + 1);
			var length = (int)bb.ReadUnsignedBits(NET_MAX_PAYLOAD_BITS + 3);
			var f = bb.ReadBoolean();

			if (f)
			{
				bb.ReadBits(12);
				bb.ReadBits(4);
			}

			bb.SeekBits(length);
		}

		private static void svc_updatestringtable(BitBuffer bb)
		{
			bb.ReadBits(5);
			var sound = (bb.ReadBoolean() ? bb.ReadBits(16) : 1);
			var b = (int)bb.ReadUnsignedBits(20);
			bb.SeekBits(b);
		}

		private static void svc_voiceinit(BitBuffer bb)
		{
			bb.ReadString();
			bb.ReadBits(8);
		}

		private static void svc_voicedata(BitBuffer bb)
		{
			bb.ReadBits(8);
			bb.ReadBits(8);
			var b = (int)bb.ReadUnsignedBits(16);
			bb.SeekBits(b);
		}

		private static void svc_sounds(BitBuffer bb)
		{
			var r = bb.ReadBoolean();
			var sounds = r ? 1 : bb.ReadBits(8);
			var b = r ? (int)bb.ReadUnsignedBits(8) : (int)bb.ReadUnsignedBits(16);
			bb.SeekBits(b);
		}

		private static bool is_sound_reliable(BitBuffer bb)
		{
			var r = bb.ReadBoolean();
			var sounds = r ? 1 : bb.ReadBits(8);
			var b = r ? (int)bb.ReadUnsignedBits(8) : (int)bb.ReadUnsignedBits(16);
			bb.SeekBits(b);

			return r;
		}


		private static void svc_setview(BitBuffer bb)
		{
			bb.ReadBits(11);
		}

		private static void svc_fixangle(BitBuffer bb)
		{
			bb.ReadBoolean();
			bb.ReadInt16();
			bb.ReadInt16();
			bb.ReadInt16();
		}

		private static void svc_crosshairangle(BitBuffer bb)
		{
			bb.ReadInt16();
			bb.ReadInt16();
			bb.ReadInt16();
		}

		private static void svc_bspdecal(BitBuffer bb)
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

		private static void svc_usermessage(BitBuffer bb)
		{
			bb.ReadBits(8);
			var b = (int)bb.ReadUnsignedBits(11);
			bb.SeekBits(b);
		}

		const int MAX_EDICT_BITS = 11;
		const int MAX_SERVER_CLASS_BITS = 9;

		private static void svc_entitymessage(BitBuffer bb)
		{
			bb.ReadUnsignedBits(MAX_EDICT_BITS);
			bb.ReadBits(MAX_SERVER_CLASS_BITS);
			var b = (int)bb.ReadUnsignedBits(11);
			bb.SeekBits(b);
		}

		private static void svc_gameevent(BitBuffer bb)
		{
			var b = (int)bb.ReadUnsignedBits(11);
			bb.SeekBits(b);
		}

		private static void svc_packetentities(BitBuffer bb)
		{
			bb.ReadBits(11);
			var d = bb.ReadBoolean();
			if (d)
				bb.ReadBits(32);
			bb.ReadBoolean();
			bb.ReadBits(11);
			var b = (int)bb.ReadUnsignedBits(20);
			bb.ReadBoolean();
			bb.SeekBits(b);
		}

		private static void svc_tempentities(BitBuffer bb)
		{
			bb.ReadBits(8);
			var b = (int)bb.ReadUnsignedBits(17);
			bb.SeekBits(b);
		}

		private static void svc_prefetch(BitBuffer bb)
		{
			bb.ReadBits(13);
		}

		private static void svc_menu(BitBuffer bb)
		{
			bb.ReadBits(16);
			var b = (int)bb.ReadUnsignedBits(16);
			bb.SeekBits(b << 3);
		}

		const int MAX_EVENT_BITS = 9;

		private static void svc_gameeventlist(BitBuffer bb)
		{
			bb.ReadBits(MAX_EVENT_BITS);
			var b = (int)bb.ReadUnsignedBits(20);
			bb.SeekBits(b);
		}

		private static void svc_getcvarvalue(BitBuffer bb)
		{
			bb.ReadBits(32);
			bb.ReadString();
		}

		private static void svc_cmdkeyvalues(BitBuffer bb)
		{
			var b = (int)bb.ReadUnsignedBits(32);
			bb.SeekBits(b);
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
