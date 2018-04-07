using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Input;


namespace DemoSplicer
{
	public struct DeltaPacket
	{
		/// <summary>
		/// Tick inside the demo file
		/// </summary>
		public int Tick;
		/// <summary>
		/// Some sort of global server tick thingy
		/// </summary>
		public int GlobalTick;

		public int DeltaFrom;
	}

	public class DemoWriteInfo
	{
		/// <summary>
		/// Should demo header be written?
		/// </summary>
		public bool WriteHeader { get; set; }

		public void SetLast(int tick, int global)
		{
			LastGlobal = global;
			LastTick = tick;
 		}

		public void SetStart(int tick, int global)
		{
			StartGlobal = global;
			StartTick = tick;
		}

		/// <summary>
		/// Last tick to write from the demo.
		/// </summary>
		public int LastTick { get; private set; }

		public int LastGlobal { get; private set; }

		/// <summary>
		/// First tick to write from the demo.
		/// </summary>
		public int StartTick { get; private set; }

		public int StartGlobal { get; private set; }

		public bool SL { get; set; }

		public void SetFirstDemo()
		{
			StartTick = int.MinValue;
			WriteHeader = true;
		}
	}

	public struct SourceDemoInfo
	{
		public int DemoProtocol, NetProtocol, TickCount, EventCount, SignonLength;
		public List<ParsedDemo.DemoMessage> Messages;
		public List<string> ParsingErrors;
		public float Seconds;
		public string ID, ServerName, ClientName, MapName, GameDirectory;

		/// <summary>
		/// Finds all the delta packets for the demo.
		/// </summary>
		/// <returns></returns>
		public List<DeltaPacket> FindDeltaPacketInfo()
		{
			List<DeltaPacket> packets = new List<DeltaPacket>();
			foreach (var message in Messages)
			{
				if (message.Type == ParsedDemo.MessageType.Packet)
				{
					try
					{
						var result = Packet.FindDeltaPacketTick(message.Data);

						if (result != -1)
						{
							var tick = Packet.FindTick(message.Data);
							var delta = Packet.FindDeltaFrom(message.Data);
							packets.Add(new DeltaPacket { DeltaFrom = delta, Tick = message.Tick, GlobalTick = tick });
						}
					}
					catch { }
				}
			}

			return packets;
		}
	}

	public class ParsedDemo
	{

		public enum MessageType
		{
			Nop = 0,
			Signon,
			Packet,
			SyncTick,
			ConsoleCmd,
			UserCmd,
			DataTables,
			Stop,
			// CustomData, // L4D2
			StringTables
		}

		private readonly Stream _fstream;
		public SourceDemoInfo Info;
		byte[] header;
		public string fileName { get; set; }

		public ParsedDemo(Stream s, string fileName)
		{
			this.fileName = fileName;
			_fstream = s;
			Info.Messages = new List<DemoMessage>();
			Parse();
		}

		const int HEADER_LENGTH = 0x430;

		private void ParseHeader(BinaryReader reader)
		{
			Info.ID = Encoding.ASCII.GetString(reader.ReadBytes(8));

			if (Info.ID != "HL2DEMO\0")
			{
				Info.ParsingErrors.Add("Source parser: Incorrect mw");
			}

			Info.DemoProtocol = reader.ReadInt32();
			if (Info.DemoProtocol >> 2 > 0)
			{
				Info.ParsingErrors.Add("Unsupported L4D2 branch demo!");
				//return;
			}

			Info.NetProtocol = reader.ReadInt32();

			Info.ServerName = Encoding.ASCII.GetString(reader.ReadBytes(260));
			Info.ClientName = Encoding.ASCII.GetString(reader.ReadBytes(260));
			Info.MapName = Encoding.ASCII.GetString(reader.ReadBytes(260));
			Info.GameDirectory = Encoding.ASCII.GetString(reader.ReadBytes(260));

			Info.Seconds = reader.ReadSingle();
			Info.TickCount = reader.ReadInt32();
			Info.EventCount = reader.ReadInt32();

			Info.SignonLength = reader.ReadInt32();

			long position = _fstream.Position;
			_fstream.Seek(0, SeekOrigin.Begin);
			header = reader.ReadBytes(HEADER_LENGTH);
		}

		private static string PadToByteLength(string s, int length)
		{
			int unicodeLength = Encoding.UTF7.GetByteCount(s);
			int toAdd = length - unicodeLength;

			if(toAdd <= 0)
			{
				return s.Substring(0, s.Length + toAdd);
			}
			else
			{
				while (toAdd-- > 0)
				{
					s += '\0';
				}
					
				return s;
			}
		}

		public void WriteHeader(Stream stream, int ticks, int packets, float tickTime = 0.015f)
		{
			var writer = new BinaryWriter(stream, Encoding.ASCII);

			writer.Write(Encoding.ASCII.GetBytes(Info.ID));
			writer.Write(Info.DemoProtocol);
			writer.Write(Info.NetProtocol);

			writer.Write(Encoding.ASCII.GetBytes(Info.ServerName));
			writer.Write(Encoding.ASCII.GetBytes(Info.ClientName));
			writer.Write(Encoding.ASCII.GetBytes(Info.MapName));
			writer.Write(Encoding.ASCII.GetBytes(Info.GameDirectory));

			writer.Write(ticks * tickTime);
			writer.Write(ticks);
			writer.Write(packets);
			writer.Write(Info.SignonLength);
		}

		private void Parse()
		{
			var reader = new BinaryReader(_fstream, Encoding.ASCII);
			Info.ParsingErrors = new List<string>();
			ParseHeader(reader);

			while (true)
			{
				var msg = new DemoMessage { Type = (MessageType)reader.ReadByte() };
				if (msg.Type == MessageType.Stop)
				{
					msg.Data = reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));
					msg.Tick = int.MaxValue;
					Info.Messages.Add(msg);
					break;
				}
				msg.Tick = reader.ReadInt32();

				switch (msg.Type)
				{
					case MessageType.Signon:
					case MessageType.Packet:
					case MessageType.ConsoleCmd:
					case MessageType.UserCmd:
					case MessageType.DataTables:
					case MessageType.StringTables:
						if (msg.Type == MessageType.Packet || msg.Type == MessageType.Signon)
							msg.PriorData = reader.ReadBytes(0x54); // command/sequence info
						else if (msg.Type == MessageType.UserCmd)
							msg.PriorData = reader.ReadBytes(0x4); // unknown

						msg.Data = reader.ReadBytes(reader.ReadInt32());

						break;
					case MessageType.SyncTick:
					case MessageType.Nop:
						msg.Data = new byte[0];
						break;
					default:
						throw new Exception("Unknown msg");
				}

				Info.Messages.Add(msg);
			}
		}

		public Range FindPacketRange(int startTick, int lastTick, bool lastDemo)
		{
			int packetStart = 0;
			int packetEnd = Info.Messages.Count - 1;
			for(int i=0; i < Info.Messages.Count; ++i)
			{
				if (Info.Messages[i].Tick <= startTick && Info.Messages[i].Tick >= 0)
				{
					packetStart = i;
				}

				if (Info.Messages[i].Tick <= lastTick && (lastDemo || (Info.Messages[i].Type != MessageType.Stop && Info.Messages[i].Tick >= 0)))
				{
					packetEnd = i;
				}
			}

			return new Range(packetStart, packetEnd);
		}

		/// <summary>
		/// Inserts a console command into a demo.
		/// </summary>
		/// <param name="writer"></param>
		/// <param name="tick"></param>
		/// <param name="command"></param>
		public static void InsertConsoleCommand(BinaryWriter writer, int tick, string command)
		{
			writer.Write((byte)(MessageType.ConsoleCmd));
			writer.Write(tick);
			command += "\0"; // Make the string into a C-string by adding the null character
			writer.Write(command.Length);
			writer.Write(Encoding.ASCII.GetBytes(command));
		}

		/// <summary>
		/// Should skip message?
		/// </summary>
		/// <param name="index"></param>
		/// <param name="range"></param>
		/// <param name="firstDemo"></param>
		/// <returns></returns>
		private bool ShouldSkipMessage(int index, bool inRange, bool firstDemo)
		{
			var msg = Info.Messages[index];

			if (!inRange && !msg.IsImportantMessage)
				return true;
			else if (msg.Type == MessageType.SyncTick)
				return true;
			else
				return false;
		}

		public int Packets(int startTick, int lastTick, bool lastDemo)
		{
			int count = 0;
			var range = FindPacketRange(startTick, lastTick, lastDemo);
			for(int i=0; i <= range.End; ++i)
			{
				if (!ShouldSkipMessage(i, range.InRange(i), lastDemo))
					++count;
			}

			return count;
		}

		public int Ticks(int startTick, int lastTick)
		{
			int curTick = startTick;
			int firstTick = -1;

			for (int i = 0; i < Info.Messages.Count; ++i)
			{
				var msg = Info.Messages[i];
				if (msg.IsMovementPacket && msg.Tick <= lastTick && msg.Tick >= startTick)
				{
					if (firstTick == -1)
						firstTick = msg.Tick;

					curTick = msg.Tick;
				}
			}

			return curTick - firstTick + 1;
		}

		/// <summary>
		/// Writes the demo message into the file.
		/// </summary>
		/// <param name="writer"></param>
		/// <param name="msg"></param>
		/// <param name="runningTick"></param>
		public static void WriteMessage(BinaryWriter writer, DemoMessage msg, int runningTick, bool inRange)
		{
			writer.Write((byte)msg.Type);
			if (msg.Type == MessageType.SyncTick)
				writer.Write(0);
			else
				writer.Write(runningTick);

			switch (msg.Type)
			{
				case MessageType.Signon:
				case MessageType.Packet:
				case MessageType.ConsoleCmd:
				case MessageType.UserCmd:
				case MessageType.DataTables:
				case MessageType.StringTables:
					if (msg.PriorData != null)
						writer.Write(msg.PriorData);

					if((msg.Type == MessageType.Packet || msg.Type == MessageType.Signon) && !inRange)
					{
						var data = Packet.GetPacketDataWithoutType(msg.Data, new int[] { 17 });
						writer.Write(data.Length);
						writer.Write(data);
					}
					else
					{
						writer.Write(msg.Data.Length);
						writer.Write(msg.Data);
					}

					break;
			}
		}

		/// <summary>
		/// Writes the demo into a stream.
		/// </summary>
		/// <param name="s"></param>
		/// <param name="startTick"></param>
		/// <param name="lastTick"></param>
		/// <param name="firstDemo"></param>
		/// <param name="runningTick"></param>
		/// <param name="lastDemo"></param>
		/// <param name="firstDemoMap"></param>
		/// <param name="lastDemoMap"></param>
		/// <returns></returns>
		public int WriteToFile(Stream s, int startTick, int lastTick, bool firstDemo, int runningTick, bool lastDemo, bool lastDemoMap)
		{
			var writer = new BinaryWriter(s);
			var range = FindPacketRange(startTick, lastTick, lastDemo);
			int prevTick;

			if (startTick > 0)
				prevTick = startTick - 1;
			else
			{
				prevTick = 0;
			}

			bool historyOn = false;
			InsertConsoleCommand(writer, runningTick, "hud_drawhistory_time 0");

			for(int i=0; i <= range.End; ++i)
			{
				var msg = Info.Messages[i];
				bool inPacketRange = range.InRange(i);

				if (ShouldSkipMessage(i, inPacketRange, firstDemo))
					continue;
				else if (msg.Type == MessageType.Stop)
				{
					if (lastDemo)
					{
						writer.Write((byte)msg.Type);
						writer.Write(msg.Data);
					}
					break;
				}

				if (firstDemo)
				{
					runningTick = msg.Tick;
				}
				else if(inPacketRange)
				{
					if(msg.Tick > prevTick)
					{
						++runningTick;
						prevTick = msg.Tick;
					}
				}

				if (ShouldTurnOnDrawHistory(msg, startTick, historyOn))
				{
					TurnOnDrawHistory(writer, runningTick, ref historyOn);
				}

				WriteMessage(writer, msg, runningTick, range.InRange(i));
			}

			if(lastDemoMap)
			{
				InsertConsoleCommand(writer, runningTick, "stopsound");
			}
			InsertConsoleCommand(writer, runningTick, string.Format("echo Playing demo:{0}", fileName));

			writer.Flush();
			return runningTick;
		}

		private static void TurnOnDrawHistory(BinaryWriter writer, int runningTick, ref bool historyOn)
		{
			historyOn = true;
			InsertConsoleCommand(writer, runningTick, "hud_drawhistory_time 5");
		}

		private static bool ShouldTurnOnDrawHistory(DemoMessage msg, int startTick, bool historyOn)
		{
			return msg.Tick - 6 > startTick && msg.IsMovementPacket && !historyOn;
		}

		public class DemoMessage
		{
			public byte[] PriorData;
			public byte[] Data;
			public int Tick;
			public MessageType Type;

			public bool IsMovementPacket
			{
				get
				{
					return (Type == MessageType.Packet && (Packet.HasMSGType(Data, 26)));
				}
			}

			public bool IsImportantMessage
			{
				get
				{
					return Type == MessageType.DataTables ||
						Type == MessageType.Signon ||
						IsMovementPacket;
				}
			}
		}
	}
}
