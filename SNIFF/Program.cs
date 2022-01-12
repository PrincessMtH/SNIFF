using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Linq;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

/*
*  The main class!
*  this is where the magic happens. sorry for the mess!
*  
*  this will keep changing as FNF's chart format
*  evolves over time. Exciting stuff
*  
*  TINY LITTLE TODO of things i wanna do:
*  - convert from MIDI!
*  - crop off the .0s after all the floats. this will
*    probably require making my own JSON deserializer :/
*  - BPM changes to and from automation data
*  - convert from playlist instead of from 1 pattern for
*    ease of use n shit
*  - read song data from inside the flp somewhere
*  - command line arguments
*  
*  pls give credit if u use or reference this or whatever.
*  <3 - MtH
*/

namespace SNIFF
{
	static class Globals
	{
		public const int VersionNumber = 10417;
		public const int NoteSize = 24;
		public static ushort ppqn = 96;
		public static string name = "";
		public static float bpm = 0;
		public static List<float> bpmList = new List<float>();
		public static int needsVoices = 0; //0 = undecided, -1 = false, 1 = true
		public static string player1 = "";
		public static string player2 = "";
	}

	public enum MIDINotes
	{
		L = 48,
		D = 49,
		U = 50,
		R = 51,

		BPM_CH = 53
	}

	public enum HedgeNotes : int
	{
		F_U = 0,
		F_D = 1,
		F_L = 2,
		F_R = 3,

		BPM_CH = 4
	}
	

	class Program
	{
		static void ResetGlobals()
		{
			Globals.ppqn = 96;
			Globals.bpm = 0;
		}

		public static FLNote MakeNote(float strumTime, int noteData, float sustainLength, float bpm)
		{
			byte velo = 0x64;
			uint noteTime = (uint)Math.Round(strumTime / MIDITimeToMillis(bpm));
			uint duration = (uint)Globals.ppqn / 4;
			uint midiPitch = 0;

			if (sustainLength > 0)
			{
				duration = (uint)(sustainLength / MIDITimeToMillis(bpm));
				if (duration < (uint)Globals.ppqn / 2)
					velo = 0x3F;
			}
			if (noteData >= (int)HedgeNotes.BPM_CH)
				duration = (uint)(Globals.ppqn * 4);
			
			switch (noteData)
			{
				case (int)HedgeNotes.F_L:
					midiPitch = (uint)MIDINotes.L;
					break;
				case (int)HedgeNotes.F_D:
					midiPitch = (uint)MIDINotes.D;
					break;
				case (int)HedgeNotes.F_U:
					midiPitch = (uint)MIDINotes.U;
					break;
				case (int)HedgeNotes.F_R:
					midiPitch = (uint)MIDINotes.R;
					break;
				case (int)HedgeNotes.BPM_CH:
					midiPitch = (uint)MIDINotes.BPM_CH;
					break;
				default:
					break;
			}

			return new FLNote
			{
				Time = noteTime,
				TBD = 0x4000,
				ChannelNo = 0x0000,
				Duration = duration,
				Pitch = midiPitch,
				FinePitch = 120,
				Release = 0x40,
				Flags = 0x00,
				Panning = 0x40,
				Velocity = velo,
				ModX = 0x80,
				ModY = 0x80
			};
		}

		static FLNote DefaultNote(uint time, uint duration, uint pitch)
		{
			return new FLNote
			{
				Time = time,
				TBD = 0x4000,
				ChannelNo = 0x0000,
				Duration = duration,
				Pitch = pitch,
				FinePitch = 120,
				Release = 0x40,
				Flags = 0x00,
				Panning = 0x40,
				Velocity = 0x64,
				ModX = 0x80,
				ModY = 0x80
			};
		}

		static FLNote DefaultNote()
		{
			return DefaultNote(0, (uint)Globals.ppqn / 4, 60);
		}

		static byte[] FLNotesToBytes(List<FLNote> notes)
		{
			List<byte> bytes = new List<byte>();
			foreach (FLNote note in notes)
			{
				bytes.AddRange(BitConverter.GetBytes(note.Time));
				bytes.AddRange(BitConverter.GetBytes(note.TBD));
				bytes.AddRange(BitConverter.GetBytes(note.ChannelNo));
				bytes.AddRange(BitConverter.GetBytes(note.Duration));
				bytes.AddRange(BitConverter.GetBytes(note.Pitch));
				bytes.AddRange(BitConverter.GetBytes(note.FinePitch));
				bytes.Add(note.Release);
				bytes.Add(note.Flags);
				bytes.Add(note.Panning);
				bytes.Add(note.Velocity);
				bytes.Add(note.ModX);
				bytes.Add(note.ModY);
			}
			return bytes.ToArray();
		}

		static List<byte> JSONtoFL(JObject o)
		{
			List<byte> file = new List<byte>()
			{//full FLhd plus FLdt bytes
				0x46, 0x4C, 0x68, 0x64, 0x06, 0x00, 0x00, 0x00, 0x10, 0x00, 0x05, 0x00, 0x60, 0x00, 0x46, 0x4C, 0x64, 0x74
			}; //then append int size of data (below) and then data itself
			List<byte> data = new List<byte>()
			{
				0xC7, 0x07, 0x31, 0x31, 0x2E, 0x31, 0x2E, 0x30, 0x00, 0x1C, 0x03, 0x41, 0x00, 0x00, 0xE0
			}; //then append size of notes and then notes themselves
			List<FLNote> notes = new List<FLNote>();

			//calc bpm from BeatLenght
			float bps = (float)o["BeatLenght"];
			float bpm = 60 / bps;
			float beatdiv = (float)o["Subdivisions"];

			Console.Write("\nBPM is estimated to be "+bpm+ ", is this right?\n" +
				"If so, leave the following field empty.\n" +
				"If not, please type the correct BPM.\n" +
				"BPM: ");
			string newbpm = Console.ReadLine();
			if (newbpm != "")
			{
				bpm = float.Parse(newbpm);
				Globals.bpm = bpm;
			}
			var lastBPMChangeTime = new {
				u = (uint)0, f = (float)0, s = (int)0
			};
			foreach (JObject hedgeNote in o["Notes"])
			{
				FLNote swagNote = MakeNote((float)hedgeNote["SpawnTime"] * 1000, (int)hedgeNote["Dir"], (int)hedgeNote["Type"] == 1 ? (float)hedgeNote["TrailLenght"] * 1000 : 0.0f, bpm);
				swagNote.Time += lastBPMChangeTime.u;
				notes.Add(swagNote);
				//j++;
			}
			byte[] nBytes = FLNotesToBytes(notes);
			// the array length lets goo
			List<byte> arrlen = new List<byte>();
			int len = nBytes.Length;
			while (len > 0)
			{
				arrlen.Add((byte)(len & 0x7f));
				len = len >> 7;
				if (len > 0)
					arrlen[arrlen.Count-1] += 0x80;
			}

			data.AddRange(arrlen.ToArray());
			data.AddRange(nBytes);
			file.AddRange(BitConverter.GetBytes(data.Count));
			file.AddRange(data);
			return file;
		}

		static void FlipNoteActor(JObject section)
		{
			for (int i = 0; i < ((JArray)section["sectionNotes"]).Count; i++)
			{
				int s = (int)section["sectionNotes"][i][1];
				if (s > 3)
					s -= 4;
				else
					s += 4;
				section["sectionNotes"][i][1] = s;
			}
		}
		static float MIDITimeToMillis(float bpm)
		{
			return (1000.0f * 60.0f / bpm / Globals.ppqn);
		}

		/* 
		 * This makes a note data event's data into a
		 * list of FLNotes
		 */
		static List<FLNote> BytesToFLNotes(byte[] b)
		{
			List<FLNote> notes = new List<FLNote>();
			int i = 0;
			while (i < b.Length)
			{
				//notes loop
				FLNote n = new FLNote
				{
					Time = BitConverter.ToUInt32(b, i),
					TBD = BitConverter.ToUInt16(b, i + 4),
					ChannelNo = BitConverter.ToUInt16(b, i + 6),
					Duration = BitConverter.ToUInt32(b, i + 8),
					Pitch = BitConverter.ToUInt32(b, i + 12),
					FinePitch = BitConverter.ToUInt16(b, i + 16),
					Release = b[i + 18],
					Flags = b[i + 19],
					Panning = b[i + 20],
					Velocity = b[i + 21],
					ModX = b[i + 22],
					ModY = b[i + 23]
				};
				notes.Add(n);
#if DEBUG
										Console.WriteLine("note added");
#endif
				i += Globals.NoteSize;
			}
			Console.WriteLine(notes.Count + " notes processed.");
			return notes;
		}

		static JObject FLtoJSON(List<FLNote> notes)
		{
			if (notes == null)
				return null;
			// after da data loop
			// let us start assembling the funk
			//Console.WriteLine("\nFirst, we gotta set up some data...");
			JObject song = new JObject();
			if (Globals.bpm == 0) {
				Console.Write("BPM: ");
				Globals.bpm = float.Parse(Console.ReadLine());
			}
			else if (Globals.bpmList.Count > 0)
				Globals.bpm = Globals.bpmList[0];
			song.Add("BeatLenght", 60 / Globals.bpm);
			song.Add("Subdivisions", 1.0f); // arbitrary cuz i think it only used for editor
			int enableChangeBPM = 0; // 0 = no, 1 = yes, 2 = yes and use bpmList.txt
			for (int i = 0; i < notes.Count; i++)
			{
				if (notes[i].Pitch == (uint)MIDINotes.BPM_CH)
				{
					Console.Write("\nLooks like you have one or more BPM changes. ");
					if (File.Exists("bpmList.txt") && Globals.bpmList.Count == 0)
					{
						Console.Write("Do you want to use bpmList.txt?\n" +
							"(y/N, default N) ");
						if (Console.ReadLine().ToLower().Trim() == "y")
						{
							string[] bpmListFile = File.ReadAllLines("bpmList.txt");
							foreach (string bpmLine in bpmListFile)
							{
								bool success = float.TryParse(bpmLine, out float outBPM);
								if (success)
								{
									Globals.bpmList.Add(outBPM);
									Console.WriteLine("Added BPM " + outBPM);
								}
							}
						}
					}
					if (Globals.bpmList.Count > 0)
					{
						enableChangeBPM = 2;
						Globals.bpm = Globals.bpmList[0];
						song["bpm"] = Globals.bpm;
					}

					if (enableChangeBPM == 0)
					{
						Console.Write("Is the initial BPM of " + Globals.bpm + " correct? If so, leave the\n" +
							"following field empty. If not, please type the correct BPM.\n" +
							"BPM: ");
						string newbpm = Console.ReadLine();
						if (newbpm != "")
						{
							float daBPM = float.Parse(newbpm);
							Globals.bpm = daBPM;
							song["bpm"] = daBPM;
							Globals.bpmList.Add(daBPM);
						}
						Console.WriteLine("Selected BPM: " + Globals.bpm + "\nGreat! Keep an eye out, we'll be asking you for the new BPMs.");
						enableChangeBPM = 1;
					}
					i = notes.Count;
				}
			}
			Console.WriteLine("");

			var lastBPMChangeTime = new {
				u = (uint)0, f = (float)0
			};
			int bpmListIdx = 1;

			JArray hedgeNotes = new JArray();

			while (notes.Count > 0)
			{
				// THE NOTE LOOP
				// this is where you have sex
				//Console.WriteLine("note FLS TIME " + notes[0].Time);
				JObject n = null;
				float time = lastBPMChangeTime.f + MIDITimeToMillis(Globals.bpm) * (notes[0].Time - lastBPMChangeTime.u) / 1000;
				//Console.WriteLine("note FNF TIME " + time);
				float sus = 0;
				//if note is 2 steps or longer, or if the velocity is lower than half
				//we actually get the sus
				if (notes[0].Duration >= Globals.ppqn / 2 || notes[0].Velocity < 0x40)
					sus = MIDITimeToMillis(Globals.bpm) * notes[0].Duration / 1000;
				switch (notes[0].Pitch)
				{
					case (uint)MIDINotes.BPM_CH:
						Console.WriteLine("BPM change event found on bar " + notes[0].Time / Globals.ppqn / 4 + "!");
						if (enableChangeBPM == 2 && bpmListIdx < Globals.bpmList.Count)
							Globals.bpm = Globals.bpmList[bpmListIdx++];
						else if (enableChangeBPM == 1)
						{
							Console.Write("New BPM: ");
							float daBPM = float.Parse(Console.ReadLine());
							Globals.bpm = daBPM;
							Globals.bpmList.Add(daBPM);
						}
							
						lastBPMChangeTime = new {
							u = notes[0].Time, f = time
						};
						break;
					case (uint)MIDINotes.L:
						n = new JObject(new JProperty("SpawnTime", time),
							new JProperty("Dir", (uint)HedgeNotes.F_L),
							new JProperty("Type", sus > 0.0f ? 1 : 0),
							new JProperty("TrailLenght", sus)
						);
						break;
					case (uint)MIDINotes.D:
						n = new JObject(new JProperty("SpawnTime", time),
							new JProperty("Dir", (uint)HedgeNotes.F_D),
							new JProperty("Type", sus > 0.0f ? 1 : 0),
							new JProperty("TrailLenght", sus)
						);
						break;
					case (uint)MIDINotes.U:
						n = new JObject(new JProperty("SpawnTime", time),
							new JProperty("Dir", (uint)HedgeNotes.F_U),
							new JProperty("Type", sus > 0.0f ? 1 : 0),
							new JProperty("TrailLenght", sus)
						);
						break;
					case (uint)MIDINotes.R:
						n = new JObject(new JProperty("SpawnTime", time),
							new JProperty("Dir", (uint)HedgeNotes.F_R),
							new JProperty("Type", sus > 0.0f ? 1 : 0),
							new JProperty("TrailLenght", sus)
						);
						break;
					default:
						break;
				}
				if (n != null)
					hedgeNotes.Add(n);
				notes.RemoveAt(0);
			}
			//note to avoid confusion: the array of sections is called notes in json
			song.Add("Notes", hedgeNotes);
			song.Add("generatedBy", "SNIFF ver." + Globals.VersionNumber);
			return song;
		}

		static void CollectFLPGlobals(FLFile flFile)
		{
			Globals.ppqn = flFile.ppqn;
			DwordEvent tempoEvent = (DwordEvent)flFile.FindFirstEvent(Event.EventIDs.D_PROJ_TMP);
			if (tempoEvent != null)
			{
				Globals.bpm = (uint)tempoEvent.Value / 1000.0f;
				Console.WriteLine("BPM found: " + Globals.bpm);
			}
		}

		static List<FLNote> CollectFLNotes(FLFile flFile, ushort pattern, bool strict = false)
		{
			List<FLNote> notes = new List<FLNote>();

			// if it has a project tempo it's an .flp
			if (flFile.FindFirstEvent(Event.EventIDs.D_PROJ_TMP) != null)
			{
				CollectFLPGlobals(flFile);
				bool triedPat = false;

				// get the first fpc channel and get just the notes from that,
				// if it dont exist just get them from whatever the first channel is
				ushort generator = 0;
				for (int i = 0; i < flFile.eventList.Count; i++)
				{
					if (flFile.eventList[i].ID == (byte)Event.EventIDs.A_PLUG_NAME &&
						((byte[])flFile.eventList[i].Value).SequenceEqual(new byte[] { 0x46, 0x50, 0x43, 0x00 }))
					{
						generator = (ushort)flFile.FindPrevEvent(Event.EventIDs.W_GEN_CH_NO, i).Value;
						i = flFile.eventList.Count;
						Console.WriteLine("FPC channel found at " + generator);
					}
				}

				// scrub pattern for notes from selected channel
				while (notes.Count == 0)
				{
					byte[] noteData = flFile.FindNoteDataByPatternNum(pattern);
					if (noteData != null)
					{
						notes = BytesToFLNotes(noteData);
						for (int i = 0; i < notes.Count; i++)
						{
							// remove any notes not from selected channel
							if (notes[i].ChannelNo != generator)
								notes.RemoveAt(i--);
						}
						if (notes.Count == 0 && !triedPat)
						{
							pattern = 0;
							triedPat = true;
							if (strict)
								return null;
						}
						pattern++;
					}
					else
					{
						Console.WriteLine("No notes found.");
						//Console.ReadLine();
						return null;
					}
				}
				Console.WriteLine("Notes grabbed from pattern " + (pattern - 1));
			}
			else
			{
				// if .fsc file (pattern number is ignored because there's only one pattern with id 0)
				ArrayEvent noteData = (ArrayEvent)flFile.FindFirstEvent(Event.EventIDs.A_NOTE_DATA);
				if (noteData != null)
					notes = BytesToFLNotes((byte[])noteData.Value);
				else
				{
					Console.WriteLine("No notes found.");
					return null;
				}
			}
			return notes;
		}

		//yes the main function
		[STAThread]
		static void Main(string[] args)
		{
			Console.WriteLine("SiIva Note Importer For Fark VS Spark HedgeFunkin (SNIFF)\nquite pungent my dear... version  "+ Globals.VersionNumber +"\n");
			OpenFileDialog fileBrowser = new OpenFileDialog {
				InitialDirectory = Directory.GetCurrentDirectory(),
				Filter = "FL Studio file (*.fsc, *.flp)|*.fsc;*.flp|Chart file (*.chart)|*.chart|All files (*.*)|*.*",
				Multiselect = true
			};
			if (args.Length == 0)
				Console.WriteLine("Select your .fsc, .flp or .chart file...");
			if (args.Length > 0 || fileBrowser.ShowDialog() == DialogResult.OK)
			{
				if (args.Length == 0)
					args = fileBrowser.FileNames;
				string dir = Directory.GetCurrentDirectory();
				foreach (string fileName in args)
				{
					if (fileName.EndsWith(".chart"))
					{
						Console.WriteLine("Opened Chart file: "+fileName);
						JObject o;
						try {o = JObject.Parse(File.ReadAllText(fileName));}
						catch (Exception e) {
							MessageBox.Show(e.Message);
							return;
						}

						byte[] file = JSONtoFL(o).ToArray();

						SaveFileDialog saveBrowser = new SaveFileDialog
						{
							InitialDirectory = dir,
							Filter = "FL Studio score file (*.fsc)|*.fsc|All files (*.*)|*.*",
							FileName = Path.GetFileNameWithoutExtension(fileName) + ".fsc",
						};
						if (saveBrowser.ShowDialog() == DialogResult.OK)
						{
							File.WriteAllBytes(saveBrowser.FileName, file);
							dir = Path.GetDirectoryName(saveBrowser.FileName);
						}
					}
					else
					{
						byte[] b = null;
						try {b = File.ReadAllBytes(fileName);}
						catch (Exception e) {
							MessageBox.Show(e.Message);
							return;
						}
						if (b == null || b.Length < 4)
							return;

						FLFile flFile = new FLFile(b);

						ushort pattern = 0;
						WordEvent curPat = (WordEvent)flFile.FindFirstEvent(Event.EventIDs.W_CUR_PAT);
						if (curPat != null)
							pattern = (ushort)curPat.Value;
						else
							pattern = 1;

						if (pattern != 0)
						{
							JObject file = FLtoJSON(CollectFLNotes(flFile, pattern));
							if (file != null)
							{
								SaveFileDialog saveBrowser = new SaveFileDialog
								{
									InitialDirectory = dir,
									Filter = "Chart File (*.chart)|*.chart|All files (*.*)|*.*",
									FileName = Path.GetFileNameWithoutExtension(fileName),
								};
								saveBrowser.FileName += ".chart";
								if (saveBrowser.ShowDialog() == DialogResult.OK)
								{
									File.WriteAllText(saveBrowser.FileName, file.ToString(Formatting.None));
									dir = Path.GetDirectoryName(saveBrowser.FileName);
								}
							}
						}
						ResetGlobals();
					}
				}

				Console.WriteLine("Press any key to close...");
				Console.ReadKey();
				return;
			}
			else
				Console.WriteLine("Dialog closed");
		}
	}
}
