﻿using NAudio.Midi;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeneticMIDI.Representation
{
    [Serializable]
    [ProtoContract]
    public class Composition : IPlayable
    {
        [ProtoMember(1)]
        public List<Track> Tracks { get; private set; }

        [ProtoMember(2)]
        public string NameTag { get; private set; }

        public Composition()
        {
            this.Tracks = new List<Track>();
            this.NameTag = "";
        }

        public void Add(Track track)
        {
            this.Tracks.Add(track);
        }

        public PlaybackInfo GeneratePlaybackInfo()
        {
            PlaybackInfo info = new PlaybackInfo();
            foreach (Track t in Tracks)
                info += t.GeneratePlaybackInfo(0);
            return info;
        }

        public Track GetLongestTrack()
        {
            int max_dur = 0;
            int id = 0;
            for (int i = 0; i < Tracks.Count; i++)
            {
                var t = Tracks[i];
                if (t.Duration > max_dur)
                {
                    max_dur = t.Duration;
                    id = i;
                }
            }
            return Tracks[id];
        }

        public override string ToString()
        {
            string str = "";
            if (NameTag != "")
                str = NameTag + " ";
            for (int i = 0; i < Tracks.Count; i++ )
            {
                str += "(" + Tracks[i].ToString() + ")";

                if (i != Tracks.Count - 1)
                    str += " || ";
            }
            return str;
        }


        /// <summary>
        /// Loads a Harmony Sequence though
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        public static Composition LoadMidiType1(MidiFile f)
        {
            Composition comp = new Composition();
            int c = 1;
            foreach(var trackEvents in f.Events)
            {
                Track t = new Track();
                
                t.Channel = (byte)c++;
                TempoEvent tempo = new TempoEvent((int)(Note.ToRealDuration((int)Durations.qn, 60) * 1000000), 0);
                HarmonySequence seq = new HarmonySequence();
                long lastAbsTime = -1;

                List<int> chordNotes = new List<int>(); int chordDuration = 0;
                PatchNames instr = (PatchNames)9999;

                bool add = true;
                foreach(var e in trackEvents)
                {
                    if (e as TempoEvent != null)
                    {
                        tempo = (TempoEvent)e;
                    }
                    if (e as PatchChangeEvent != null)
                    {
                        var p = e as PatchChangeEvent;
                        t.Instrument = (PatchNames)p.Patch;      
                  
                        if(t.Instrument == instr)
                        {
                            break;
                        }
                        instr = t.Instrument;
                        /*if(t.Duration < 1)
                        {
                            foreach(var t_ in comp.Tracks)
                                if(t_.Instrument == (PatchNames)p.Patch)
                                {
                                    t = t_;
                                    seq = t_.GetMainSequence() as HarmonySequence;
                                    add = false;
                                    break;
                                }
                        }*/
                    }
                    NoteOnEvent on = e as NoteOnEvent;
                   
                    if (on != null && on.OffEvent != null)
                    {
                        int newDuration = Note.ToNoteLength(on.NoteLength, f.DeltaTicksPerQuarterNote, tempo.Tempo);

                        if (on.AbsoluteTime == lastAbsTime && chordDuration==newDuration)//skip chords
                        {
                            chordNotes.Add(on.NoteNumber);
                        }
                        else
                        {
                            Chord lastChord = new Chord(chordNotes.ToArray(), chordDuration);
                            chordNotes.Clear(); chordDuration = 0;
                            seq.AddChord(lastChord);

                            chordNotes.Add(on.NoteNumber);
                        }

                        chordDuration = newDuration;
                        lastAbsTime = on.AbsoluteTime;
                    }
                }
                if(chordNotes.Count > 0)
                {
                    Chord lastChord = new Chord(chordNotes.ToArray(), chordDuration);
                    chordNotes.Clear(); chordDuration = 0;
                    seq.AddChord(lastChord);
                }
                
                t.AddSequence(seq);
                if (!comp.Tracks.Contains(t) && t.Duration > 0 && add)
                    comp.Add(t);
            }
            
            return comp;
        }

        public static Composition LoadFromMIDI(string filename)
        {
            Composition comp = new Composition();
            comp.Tracks.Clear();

            NAudio.Midi.MidiFile f = new MidiFile(filename);
    

          //  if (f.Events.MidiFileType == 1)
           //     return LoadMidiType1(f);
            
            f.Events.MidiFileType = 0;

            byte max_channels = 0;
            foreach(var trackEvent in f.Events)
            foreach (var e in trackEvent)
                if (e.Channel > max_channels)
                    max_channels = (byte)e.Channel;
            max_channels++;
            Track[] tracks = new Track[max_channels];
            MelodySequence[] seqs = new MelodySequence[max_channels];
            TempoEvent[] tempos = new TempoEvent[max_channels];
            for (byte i = 0; i < max_channels; i++ )
            {
                tracks[i] = new Track(PatchNames.Acoustic_Grand, i);
                seqs[i] = new MelodySequence();
                tempos[i] = new TempoEvent((int)(Note.ToRealDuration((int)Durations.qn, 60) * 1000), 0);
                tempos[i].Tempo = 60;
            }
            foreach(var trackEvents in f.Events)
            foreach (var e in trackEvents)
            {
                if (e as TempoEvent != null)
                {
                    tempos[e.Channel] = (TempoEvent)e;
                }
                if (e as PatchChangeEvent != null)
                {
                    var p = e as PatchChangeEvent;
                    tracks[p.Channel].Instrument = (PatchNames)p.Patch;
                }
                NoteOnEvent on = e as NoteOnEvent;
                if (on != null && on.OffEvent != null)
                {
                    int total_dur = Note.ToNoteLength((int)on.AbsoluteTime, f.DeltaTicksPerQuarterNote, tempos[on.Channel].Tempo);
                    if (total_dur > seqs[on.Channel].Duration)
                        seqs[on.Channel].AddPause(total_dur - seqs[on.Channel].Duration);
                    
                    int duration = Note.ToNoteLength(on.NoteLength, f.DeltaTicksPerQuarterNote, tempos[on.Channel].Tempo);
                    seqs[on.Channel].AddNote(new Note(on.NoteNumber, (int)duration));
                }
            }
            for(byte i = 0; i < max_channels; i++)
            {
                if(seqs[i].Length > 0)
                {
                    tracks[i].AddSequence(seqs[i]);
                    comp.Tracks.Add(tracks[i]);
                }
            }
            comp.NameTag = System.IO.Path.GetFileNameWithoutExtension(filename);
            return comp;
        }

        public PlaybackInfo GeneratePlayback()
        {
            return GeneratePlaybackInfo();
        }

        public void WriteToMidi(string path)
        {
            MidiEventCollection events = new MidiEventCollection(1, 480);
            foreach(Track t in Tracks)
            {
                List<MidiEvent> trackEvents = new List<MidiEvent>();
                var info = t.GeneratePlaybackInfo();

                var keys = new int[info.Messages.Keys.Count];
                int i = 0;
                foreach (var k in info.Messages.Keys)
                {
                    keys[i++] = k;
                }

                for (i = 0; i < keys.Length - 1; i++)
                {
                    foreach (PlaybackMessage message in info.Messages[keys[i]])
                    {
                        try
                        {
                            var e = MidiEvent.FromRawMessage(message.GenerateMidiMessage().RawData);
                            int note_dur = (int)Note.ToNoteDuration(keys[i]);
                            int midi_dur = Note.ToMidiLength(note_dur, 240, 60);
                            e.AbsoluteTime = keys[i];
                            trackEvents.Add(e);
                        }
                        catch
                        {
                            // TODO figure out crash
                        }
                    }
                    int sleep_dur = keys[i + 1] - keys[i];
                    //Thread.Sleep(sleep_dur);
                }

                // Append the end marker to the track
                if(trackEvents[trackEvents.Count - 1].CommandCode == MidiCommandCode.NoteOn)
                {
                    var noteOn = trackEvents[trackEvents.Count - 1] as NoteOnEvent;
                    int note = noteOn.NoteNumber;
                    int duration = noteOn.NoteLength;
                    int channel = noteOn.Channel;
                    var offM = MidiMessage.StopNote(note, noteOn.Velocity, channel);
                    var offE = MidiEvent.FromRawMessage(offM.RawData);
                    offE.AbsoluteTime = noteOn.AbsoluteTime;
                    trackEvents.Add(offE);
                }

                long absoluteTime = 0;
                if (trackEvents.Count > 0)
                    absoluteTime = trackEvents[trackEvents.Count - 1].AbsoluteTime+100;
                trackEvents.Add(new MetaEvent(MetaEventType.EndTrack, 0, absoluteTime));

                events.AddTrack(trackEvents);
            }

            MidiFile.Export(path, events);
        }
    }
}
