using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Input;


namespace DemoSplicer
{
	public struct DeltaPacket
	{
		public int Tick;
		public int GlobalTick;
	}

	public class DemoWriteInfo
	{
		public bool WriteHeader { get; set; }
		public int LastTick { get; set; }
		public int StartTick { get; set; }

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
		public string ServerName, ClientName, MapName, GameDirectory;

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
							packets.Add(new DeltaPacket { Tick = message.Tick, GlobalTick = tick });
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

		public ParsedDemo(Stream s)
		{
			_fstream = s;
			Info.Messages = new List<DemoMessage>();
			Parse();
		}

		const int HEADER_LENGTH = 0x430;

		private void ParseHeader(BinaryReader reader)
		{
			var id = reader.ReadBytes(8);

			if (Encoding.ASCII.GetString(id) != "HL2DEMO\0")
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

			Info.ServerName = new string(reader.ReadChars(260)).Replace("\0", "");
			Info.ClientName = new string(reader.ReadChars(260)).Replace("\0", "");
			Info.MapName = new string(reader.ReadChars(260)).Replace("\0", "");
			Info.GameDirectory = new string(reader.ReadChars(260)).Replace("\0", "");

			Info.Seconds = reader.ReadSingle();
			Info.TickCount = reader.ReadInt32();
			Info.EventCount = reader.ReadInt32();

			Info.SignonLength = reader.ReadInt32();

			long position = _fstream.Position;
			_fstream.Seek(0, SeekOrigin.Begin);
			header = reader.ReadBytes(HEADER_LENGTH);
		}

		private void Parse()
		{
			var reader = new BinaryReader(_fstream);
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

		public KeyValuePair<int, int> FindPacketRange(int startTick, int lastTick, bool lastDemo)
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

			return new KeyValuePair<int, int>(packetStart, packetEnd);
		}

		public static void InsertConsoleCommand(BinaryWriter writer, int tick, string command)
		{
			writer.Write((byte)(MessageType.ConsoleCmd));
			writer.Write(tick);
			command += "\0";
			writer.Write(command.Length);
			writer.Write(Encoding.ASCII.GetBytes(command));
		}

		private bool ShouldSkipMessage(int i, KeyValuePair<int,int> range, bool firstDemo)
		{
			var msg = Info.Messages[i];

			if (i < range.Key && !msg.IsImportantMessage)
				return true;
			else if (!firstDemo && msg.Type == MessageType.Signon && !msg.ContinueDemoSignon)
				return true;
			else if (!firstDemo && msg.Type == MessageType.SyncTick)
				return true;
			else
				return false;
		}

		public static void WriteMessage(BinaryWriter writer, DemoMessage msg, int runningTick)
		{
			writer.Write((byte)msg.Type);
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

					writer.Write(msg.Data.Length);
					writer.Write(msg.Data);

					break;
			}
		}

		public int WriteToFile(Stream s, int startTick, int lastTick, bool firstDemo, int runningTick, bool lastDemo, bool firstMapDemo, bool lastDemoMap)
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

			if (firstDemo)
				writer.Write(header);

			for(int i=0; i <= range.Value; ++i)
			{
				var msg = Info.Messages[i];

				if (ShouldSkipMessage(i, range, firstDemo))
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
				else if(range.Key <= i)
				{
					if(msg.Tick > prevTick)
					{
						++runningTick;
						prevTick = msg.Tick;
					}
				}

				WriteMessage(writer, msg, runningTick);
			}

			if(!lastDemo)
				InsertConsoleCommand(writer, runningTick, "stopsound");

			writer.Flush();
			return runningTick;
		}

		public class DemoMessage
		{
			public byte[] PriorData;
			public byte[] Data;
			public int Tick;
			public MessageType Type;

			public bool ContinueDemoSignon
			{
				get
				{
					return Packet.GetSignonType(Data) != Packet.SigOnState.None;
				}
			}

			public bool IsImportantMessage
			{
				get
				{
					return Type == MessageType.DataTables ||
						(Type == MessageType.Signon && ContinueDemoSignon) ||
						(Type == MessageType.Packet && (Packet.HasMSGType(Data, 26) || Packet.HasMSGType(Data, 11)));
				}
			}
		}
	}
}
