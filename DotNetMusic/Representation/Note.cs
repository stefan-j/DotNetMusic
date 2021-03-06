﻿using DotNetLearn.Markov;
using NAudio.Midi;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeneticMIDI.Representation
{
    /// <summary>
    /// Includes Rest
    /// </summary>
    public enum NoteNames { C, Cs, D, Ds, E, F, Fs, G, GS, A, As, B, Rest };

    /// <summary>
    /// Notes names without rest
    /// </summary>
    public enum NoteNamesWORest { C, Cs, D, Ds, E, F, Fs, G, GS, A, As, B };

    public enum Durations { tn=1, sn=2, en=4, qn=8, hn=16, wn=32, bn=64};

    [ProtoContract]
    [Serializable]
    public class Note : ICloneable, IEquatable<Note>, IDifference<Note>
    {
        [ProtoMember(1)]
        public int Pitch { get; set; }
        [ProtoMember(2)]
        public int Velocity { get; set; }
        [ProtoMember(3)]
        public int Duration { get; set; } // wn = 16; bn 32; hn 8;

        public float RealDuration
        {
            get
            {
                return Note.ToRealDuration(Duration);
            }
        }

        public int Octave { get { return Pitch / 12; }
            set
            {
                int notepitch = NotePitch;
                int octave = value;
                Pitch = octave * 12 + notepitch;
            }
        }

        /// <summary>
        /// Chromatic Tone
        /// </summary>
        public int NotePitch
        {
            get
            {
                return Pitch % 12;
            }
            set
            {
                int octave = Octave;
                int notepitch = value;
                Pitch = octave * 12 + notepitch;
            }
        }
        public Note(int pitch, int duration, int volume = 127)
        {
            this.Pitch = pitch;
            this.Duration = duration;
            this.Velocity = volume;
        }

        public Note()
        {
            this.Pitch = 0;
            this.Duration = 0;
            this.Velocity = 0;
        }

        public Note(NoteNames chromatic_tone, int octave, Durations duration, int volume=127)
        {
            this.Pitch = (int)(chromatic_tone + 12 * octave);
            this.Duration = (int)duration;
            this.Velocity = volume;
        }

        public override string ToString()
        {
            string notename = "!";
            if (this.NotePitch >= 0)
                notename = NoteNames[this.NotePitch];
            string duration = this.Duration.ToString();
            duration = ((Durations)this.Duration).ToString();

            return "(" + notename + this.Octave + "-" + duration+ ")";
        }

        public string GetNotePitchString()
        {
            string notename = "!";
            if (this.NotePitch >= 0)
                notename = NoteNames[this.NotePitch];
            return notename;
        }

        public static float ToRealDuration(int note_duration, int bpm=120)
        {
            return note_duration * 60.0f * 4.0f / 32.0f / bpm;
        }

        public static float ToNoteDuration(int real_duration, int bpm=120)
        {
            return real_duration / 60.0f / 4.0f * 32.0f * bpm;
        }


        public static int ToNoteLength(int midi_length, int delta_ticks_qn, double tempo)
        {
            return (int)((double)midi_length / (double)delta_ticks_qn * (int)Durations.qn * (60/tempo));
        }

        public static int ToMidiLength(int note_length, int delta_ticks_qn, double tempo)
        {
            return (int)((double)delta_ticks_qn * (double)note_length / (int)Durations.qn / (60 / tempo));
        }

        public bool IsRest()
        {
            if (this.Pitch == -1 || this.Velocity < 0)
                return true;
            return false;
        }


        private static readonly string[] NoteNames = new string[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        public static Note[] LoadFromFile(string filename, int track=0)
        {
            List<Note> notes = new List<Note>();

            NAudio.Midi.MidiFile f = new MidiFile(filename);
            //f.Events.MidiFileType = 0;
            TempoEvent lastTempo = new TempoEvent(f.DeltaTicksPerQuarterNote, 0);
            lastTempo.Tempo = 60;
            foreach (var e in f.Events[track])
            {
                if (e as TempoEvent != null)
                    lastTempo = (TempoEvent)e;

                NoteOnEvent on = e as NoteOnEvent;
                if (on != null && on.OffEvent != null)
                {

                    double duration = ToNoteLength(on.NoteLength, f.DeltaTicksPerQuarterNote, lastTempo.Tempo);
                    notes.Add(new Note(on.NoteNumber, (int)duration));
                }
            }
            return notes.ToArray(); 
        }

        public NoteNames GetNoteName()
        {
            if (this.IsRest())
                return Representation.NoteNames.Rest;
            return (NoteNames)this.NotePitch;
        }

        public Durations GetStandardNoteDuration()
        {
             //public enum Durations { tn=1, sn=2, en=4, qn=8, hn=16, wn=32, bn=64};
            if (Duration <= (int)Durations.tn)
                return Durations.tn;
            if (Duration >= (int)Durations.wn)
                return Durations.wn;
            return (Durations)Duration;
        }

        public override bool Equals(object obj)
        {
            Note p = obj as Note;
            if (p == null)
                return false;
            return p.Duration == this.Duration && p.Velocity == this.Velocity && p.Pitch == this.Pitch;
        }

        /*public static bool operator == (Note n1, Note n2)
        {
            if (n1 == null && n2 == null)
                return true;
            else if (n1 == null || n2 == null)
                return false;

            return n1.Equals(n2);
        }*/

      /*  public static bool operator != (Note n1, Note n2)
        {
            return !n1.Equals(n2);
        }*/

        public static Note[] LoadFromFileSampledSpaced(string filename)
        {
            List<Note> notes = new List<Note>();

            NAudio.Midi.MidiFile f = new MidiFile(filename);
            f.Events.MidiFileType = 0;
            TempoEvent lastTempo = new TempoEvent(f.DeltaTicksPerQuarterNote, 0);

            int interval = f.DeltaTicksPerQuarterNote;
            int i = 0;
            int start = 0;
            while(i < f.Events[0].Count)
            {
                var e = f.Events[0][i];

                if (e as TempoEvent != null)
                    lastTempo = (TempoEvent)e;
                NoteOnEvent on = e as NoteOnEvent;
                if (on != null && on.OffEvent != null)
                {
                    if (on.AbsoluteTime <= start + interval && on.AbsoluteTime >= start)
                    {
                        notes.Add(new Note(on.NoteNumber, (int)(on.NoteLength / lastTempo.Tempo) * 60));
                        start += interval;
                    }
                    else if (on.AbsoluteTime > start)
                    {
                        start += interval;
                        continue;
                    }
                }
                i++;
            }
            return notes.ToArray();
        }

        public void StandardizeDuration()
        {

            var dur = GetClosestDuration();

            Duration = (int)dur;
        }

        public Durations GetClosestDuration()
        {
            var durations = Enum.GetValues(typeof(Durations)).Cast<Durations>().ToArray();

            int index = 0;
            double smallestError = int.MaxValue - 1;
            for (int i = 0; i < durations.Length; i++)
            {
                double error = (Duration - (int)durations[i]) * (Duration - (int)durations[i]);
                if (error < smallestError)
                {
                    smallestError = error;
                    index = i;
                }
            }
            return durations[index];
        }

        public void GetClosestLowerDurationAndRemainder(out Durations dur, out int remainder)
        {
            var durations = Enum.GetValues(typeof(Durations)).Cast<Durations>().ToArray();

            int index = 0;
            double smallestError = int.MaxValue - 1;
            for (int i = 0; i < durations.Length; i++)
            {
                double error = (Duration - (int)durations[i]) * (Duration - (int)durations[i]);
                if (error < smallestError && (int)durations[i] < Duration)
                {
                    smallestError = error;
                    index = i;
                }
            }

            dur = durations[index];

            remainder = this.Duration - (int)dur;

        }

        public int GetNumberOfDots()
        {

            Durations dur;
            int remainder;
            GetClosestLowerDurationAndRemainder(out dur, out remainder);

            int half = ((int)dur) / 2;

            int dots = 0;
            if(half > 0)
                dots = remainder / half;

            return dots;

        }


        public static int[] GetDurationRange()
        {
            var durations = Enum.GetValues(typeof(Durations)).Cast<Durations>().ToArray();
            int[] durs = new int[durations.Length];
            int j = 0;
            foreach (var i in durations)
                durs[j++] = (int)i;
            return durs;
        }

        public static int[] GetDurationRange(int low, int high)
        {
            int[] durs = GetDurationRange();
            List<int> filtered = new List<int>();
            foreach(int d in durs)
            {
                if (d >= low && d <= high)
                    filtered.Add(d);
            }
            return filtered.ToArray();
        }

        public static Note[] LoadFromFileSampled(string filename)
        {
            List<Note> notes = new List<Note>();

            NAudio.Midi.MidiFile f = new MidiFile(filename);
            f.Events.MidiFileType = 0;
            TempoEvent lastTempo = new TempoEvent(f.DeltaTicksPerQuarterNote, 0);

            int interval = f.DeltaTicksPerQuarterNote;
            int i = 0;
            int start = 0;
            while (i < f.Events[0].Count)
            {
                var e = f.Events[0][i];

                if (e as TempoEvent != null)
                    lastTempo = (TempoEvent)e;
                NoteOnEvent on = e as NoteOnEvent;
                if (on != null && on.OffEvent != null)
                {
                    if (on.AbsoluteTime <= start + interval && on.AbsoluteTime >= start)
                    {
                        notes.Add(new Note(on.NoteNumber, (int)((8*on.NoteLength) / lastTempo.Tempo)));
                        while (start + interval < on.AbsoluteTime + on.NoteLength)
                        {
                            start += interval;
                        }
                    }
                    else if (on.AbsoluteTime > start)
                    {
                        start += interval;
                        continue;
                    }
                }
                i++;
            }
            return notes.ToArray();
        }

        public override int GetHashCode()
        {
            return 3571 * this.Duration + 2903 * this.Pitch + 2129 * this.Velocity;
        }

        public object Clone()
        {
            return new Note(this.Pitch, this.Duration, this.Velocity);
        }

        public bool Equals(Note other)
        {
            if (other == null)
                return false;
            return (this.Pitch == other.Pitch && this.Duration == other.Duration);
        }

        public double GetDifference(Note other)
        {
            return Math.Sqrt((this.Pitch - other.Pitch) * (this.Pitch - other.Pitch) + (this.Duration - other.Duration) * (this.Duration - other.Duration));
        }
    }

    
}
