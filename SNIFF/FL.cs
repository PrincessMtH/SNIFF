using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/*
*  FL Studio specific stuff
*  research still not complete
*  but at least i got to use that old project for something
*  
*  some of this was aided by information gathered from
*  other sources, i will link them here:
*  https://github.com/monadgroup/FLParser/blob/master/Enums.cs
*  https://github.com/interstar/pyflp/blob/master/event.py
*  
*  pls give credit if u use or reference this or whatever.
*  <3 - MtH
*/

namespace SNIFF
{
	public struct FLNote
	{
		public uint Time;
		public ushort TBD;
		public ushort ChannelNo;
		public uint Duration;
		public uint Pitch;
		public ushort FinePitch;
		public byte Release;
		public byte Flags;
		public byte Panning;
		public byte Velocity;
		public byte ModX;
		public byte ModY;
	}

	class ByteEvent : Event
	{
		public ByteEvent(byte id, byte val) : base(id, val) { }

		public override byte[] ToBytes()
		{
			return new byte[] { ID, (byte)Value };
		}
		public override string ToString() => $"[{GetName(ID)}] {((byte)Value).ToString("X2")})";
	}

	class WordEvent : Event
	{
		public WordEvent(byte id, ushort val) : base(id, val) { }
		public override byte[] ToBytes()
		{
			byte[] b = BitConverter.GetBytes((ushort)Value);
			return new byte[] { ID, b[0], b[1] };
		}
	}

	class DwordEvent : Event
	{
		public DwordEvent(byte id, uint val) : base(id, val) { }
		public override byte[] ToBytes()
		{
			byte[] b = BitConverter.GetBytes((uint)Value);
			return new byte[] { ID, b[0], b[1], b[2], b[3] };
		}
	}

	class ArrayEvent : Event
	{
		private static byte[] GatherBytes(byte[] b, ref uint i)
		{
			int arrlen = b[i] & 0x7f;
			int shift = 0;

			while ((b[i] & 0x80) != 0)
			{
				i++;
				arrlen = arrlen | ((b[i] & 0x7f) << (shift += 7));
			}
			i++;
			return b.Skip((int)i).Take(arrlen).ToArray();
		}
		public ArrayEvent(byte id, byte[] b, ref uint i) : base(id, GatherBytes(b, ref i)) { }

		public override byte[] ToBytes()
		{
			List<byte> b = new List<byte> { ID };
			//the funny array lengthhh lets goo
			List<byte> arrlen = new List<byte>();
			int len = ((byte[])Value).Length;
			while (len > 0) {
				arrlen.Add((byte)(len & 0x7f));
				len = len >> 7;
				if (len > 0)
					arrlen[arrlen.Count - 1] += 0x80;
			}
			b.AddRange(arrlen);
			b.AddRange((byte[])Value);
			return b.ToArray();
		}
		public override string ToString() => $"[{GetName(ID)}] {BitConverter.ToString((byte[])Value).Replace("-", "")})";
	}

	abstract class Event
	{
		public Event(byte id, object val)
		{
			ID = id;
			Value = val;
		}

		public byte ID { get; }
		public object Value { get; set; }

		public abstract byte[] ToBytes();
		public override string ToString() => $"[{ID}] {Value})";

		public string GetName(byte i)
		{
			if (Names[i] != "")
				return (Names[i]);
			return (i.ToString("X2")+"\tunknown"+ (i < 64 ? "byte" : i < 128 ? "word" : i < 192 ? "dword" : "array"));
		}

		public enum EventIDs : byte
		{
			/* List of common shorthands
			*  B = byte			W = word		D = dword		A = array
			*  CH = channel		PLUG = plugin	AUTO = automation
			*  EN = enable		DIS = disable
			*  VOL = volume		PAN = panning	DLY = delay		TMP = tempo
			*  PROJ = project   PLST = playlist	PAT = pattern	GRP = group
			*  GEN = generator	EFF = effect	MXR = mixer		INS = insert
			*  INF = information
			*  CUR = current
			*  FTR = filter
			*  
			*  UNK = unknown
			*/
			B_CH_EN_DIS = 0x00,
			B_NOTE_ON,
			B_CH_VOL,
			B_CH_PAN,
			B_MIDI_CH,
			B_MIDI_NOTE,
			B_MIDI_PATCH,
			B_MIDI_BANK,

			B_PAT_MODE = 0x09,
			B_SHOW_INF,
			B_MAIN_SWING,
			B_MAIN_VOL,
			B_STRETCH_SNAP,
			B_PITCHABLE,
			B_ZIPPED,
			B_DLY_FLAGS,
			B_TIMESIG_BAR,
			B_TIMESIG_BEAT,
			B_USE_LOOP_PTS,
			B_LOOP_TYPE,
			B_CH_TYPE,
			B_MIXER_CH,
			B_N_STEPS_SHOWN = 0x18,
			B_SS_LENGTH,
			B_SS_LOOP,
			B_FX_PROPS,
			B_REG_STATUS,
			B_APDC,
			B_PLAY_TRUNC_NOTES,
			B_EE_AUTO_MODE,
			B_UNK_FL20 = 0x25,

			W_GEN_CH_NO = 0x40,
			W_PAT_START,
			W_CUR_PAT = 0x43,

			D_CUR_FTR_GRP = 0x92,

			D_PROJ_TMP = 0x9C,

			D_UNK_FL20 = 0x9F,

			A_GEN_CH_NAME = 0xC0,
			A_PAT_NAME,
			A_PROJ_TITLE,

			A_SAMP_FILE_PATH = 0xC4,
			A_PROJ_URL,
			A_PROJ_INF,
			A_VER_NUM,

			A_PLUG_NAME = 0xC9,

			A_EFF_CH_NAME = 0xCB,
			A_MXR_INS_NAME,

			A_PROJ_GENRE = 0xCE,
			A_PROJ_AUTHOR,

			A_DELAY = 0xD1,

			A_MXR_PLUG_DATA = 0xD4,
			A_PLUG_DATA,

			A_AUTO_DATA = 0xDF,
			A_NOTE_DATA,

			A_LYR_FLAGS = 0xE2,

			A_CH_FTR_GRP_NAME = 0xE7,

			A_UNK_AUTO_DATA = 0xEA,

			A_SAVE_TIME = 0xED,
			A_PLST_TRK_INF,
			A_PLST_TRK_NAME,

			B_MAX = 0x3F,
			W_MAX = 0x7F,
			D_MAX = 0xBF,
			A_MAX = 0xFF
		}

		private static readonly string[] Names = {
			 "00	Channel Enabled/Disabled",
			 "01	Note On",
			 "02	Channel Volume",
			 "03	Channel Pan",
			 "04	MIDI Channel",
			 "05	MIDI Note",
			 "06	MIDI Patch",
			 "07	MIDI Bank",
			 "",
			 "09	Pattern/Song Mode",
			 "0A	Show Info",
			 "0B	Main Swing",
			 "0C	Main Vol",
			 "0D	Stretch/Snap",
			 "0E	Pitchable",
			 "0F	Zipped",
			 "10	Delay Flags",
			 "11	Time Signature (Bar)",
			 "12	Time Signature (Beat)",
			 "13	Use Loop Points",
			 "14	Loop Type",
			 "15	Channel Type",
			 "16	Mixer Channel",
			 "",
			 "18	n Steps Shown",
			 "19	SS Length",
			 "1A	SS Loop",
			 "1B	FX Properties",
			 "1C	unknown/registration status",
			 "1D	APDC",
			 "1E	Play Truncated Notes",
			 "1F	EE Auto Mode",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "25	unknown (FL20 specific)",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "40	Generator Channel Number",
			 "41	Pattern Start",
			 "",
			 "43	Current Pattern",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "92	Current Filter Group",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "9C	Project Tempo",
			 "",
			 "",
			 "9F	unknown (FL20 specific)",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "C0	Generator Channel Name",
			 "C1	Pattern Name",
			 "C2	Project Title",
			 "",
			 "C4	(Sampler) File Path",
			 "C5	Project URL",
			 "C6	Project Info (RTF)",
			 "C7	Version Number",
			 "",
			 "C9	Plugin Name",
			 "",
			 "CB	Effect Channel Name",
			 "CC	Mixer Insert Name",
			 "",
			 "CE	Project Genre",
			 "CF	Project Author",
			 "",
			 "D1	unknown/delay",
			 "",
			 "",
			 "D4	some Plugin Data (Mixer)",
			 "D5	Plugin Data",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "DF	Automation Data",
			 "E0	Note Data",
			 "",
			 "E2	Layer Flags",
			 "",
			 "",
			 "",
			 "",
			 "E7	Channel Filter Group Name",
			 "",
			 "",
			 "EA	unknown/automation data",
			 "",
			 "",
			 "ED	Save Timestamp",
			 "EE	Playlist Track Info",
			 "EF	Playlist Track Name",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 "",
			 ""
			};
	}

	class FLFile
	{
		public ushort format;
		public ushort ntracks;
		public ushort ppqn;

		public List<Event> eventList = new List<Event>();

		public FLFile(byte[] b)
		{
			if (b == null)
				throw new ArgumentNullException(nameof(b));
			uint i = 0;
			// 'FLhd'
			if (b[0] == 0x46 && b[1] == 0x4C && b[2] == 0x68 && b[3] == 0x64)
			{
				/*
				*  Fruity Loops header chunk
				*/
				i += 4;
				uint headLen = BitConverter.ToUInt32(b, (int)i);
				i += 4;
				if (headLen == 6)
				{
					format = BitConverter.ToUInt16(b, (int)i);
					ntracks = BitConverter.ToUInt16(b, (int)i+2);
					ppqn = BitConverter.ToUInt16(b, (int)i+4);
				}
				i += headLen;
				// 'FLdt'
				if (b[i] == 0x46 && b[i + 1] == 0x4C && b[i + 2] == 0x64 && b[i + 3] == 0x74)
				{
					/*
					*  Fruity Loops data chunk
					*/
					i += 4;
					uint dataLen = BitConverter.ToUInt32(b, (int)i);
					i += 4;
					uint dataEnd = i + dataLen;
					/*
					*  Event loop
					*/
					while (i < dataEnd && i < b.Length)
					{
						byte id = b[i++];
						if (id <= (byte)Event.EventIDs.B_MAX) {
							eventList.Add(new ByteEvent(id, b[i]));
							i++;
						} else if (id <= (byte)Event.EventIDs.W_MAX) {
							eventList.Add(new WordEvent(id, BitConverter.ToUInt16(b, (int)i)));
							i += 2;
						} else if (id <= (byte)Event.EventIDs.D_MAX) {
							eventList.Add(new DwordEvent(id, BitConverter.ToUInt32(b, (int)i)));
							i += 4;
						} else if (id <= (byte)Event.EventIDs.A_MAX) {
							eventList.Add(new ArrayEvent(id, b, ref i)); //makes i skip over the array length
							i += (uint)((byte[])eventList.Last().Value).Length;
						}
					}
				}
				else
					throw new InvalidOperationException("Invalid data chunk");
			}
			else
				throw new InvalidOperationException("Invalid header chunk");
		}

		public Event FindFirstEvent(byte id)
		{
			for (int i = 0; i < eventList.Count; i++)
				if (eventList[i].ID == id)
					return eventList[i];
			return null;
		}
		public Event FindFirstEvent(Event.EventIDs id) { return FindFirstEvent((byte)id); }

		public Event FindNextEvent(byte id, int i)
		{
			for (i++; i < eventList.Count; i++)
				if (eventList[i].ID == id)
					return eventList[i];
			return null;
		}
		public Event FindNextEvent(Event.EventIDs id, int i) { return FindNextEvent((byte)id, i); }

		public Event FindLastEvent(byte id)
		{
			for (int i = eventList.Count; i > 0; i--)
				if (eventList[i-1].ID == id)
					return eventList[i-1];
			return null;
		}
		public Event FindLastEvent(Event.EventIDs id) { return FindLastEvent((byte)id); }

		public Event FindPrevEvent(byte id, int i)
		{
			for (; i > 0; i--)
				if (eventList[i - 1].ID == id)
					return eventList[i - 1];
			return null;
		}
		public Event FindPrevEvent(Event.EventIDs id, int i) { return FindPrevEvent((byte)id, i); }

		public byte[] FindNoteDataByPatternNum(ushort n)
		{
			for (int i = 0; i < eventList.Count; i++)
				if (eventList[i].ID == (byte)Event.EventIDs.W_PAT_START && (ushort)eventList[i].Value == n)
					if (eventList[i + 1].ID == (byte)Event.EventIDs.A_NOTE_DATA)
						return (byte[])eventList[i + 1].Value;
			return null;
		}


		// returns 0 on fail, case insensitive
		public ushort FindPatternNumByName(string name)
		{
			name += '\0';
			for (int i = 0; i < eventList.Count; i++)
				if (eventList[i].ID == (byte)Event.EventIDs.W_PAT_START)
					if (eventList[i + 1].ID == (byte)Event.EventIDs.A_PAT_NAME)
					{
						byte[] patName = (byte[])eventList[i + 1].Value;
						int verNum = int.Parse(Encoding.ASCII.GetString((byte[])FindFirstEvent(Event.EventIDs.A_VER_NUM).Value).Split('.')[0]);
						//fl 12 and up use unicode strings
						if (verNum > 11)
							patName = Encoding.Convert(Encoding.Unicode, Encoding.ASCII, patName);
						if (Encoding.ASCII.GetString(patName).ToLower().Trim() == name)
							return (ushort)eventList[i].Value;
					}
			return 0;
		}
	}
}
