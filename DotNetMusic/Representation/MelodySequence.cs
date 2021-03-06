﻿using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeneticMIDI.Representation
{
    [ProtoContract]
    [Serializable]
    public class MelodySequence : ISequence, ICloneable, IPlayable
    {
        [ProtoMember(1)]
        List<Note> sequence;

        [ProtoMember(2)]
        public int Duration { get; private set; }

        public float RealDuration
        {
            get
            {
                return Note.ToRealDuration(Duration);
            }
        }

        public int Length
        {
            get
            {
                return sequence.Count;
            }
        }

        public IEnumerable<Note> Notes
        {
            get
            {
                return sequence;
            }
        }

        public MelodySequence()
        {
            sequence = new List<Note>();
        }

        public MelodySequence(IEnumerable<Note> notes)
        {
            sequence = new List<Note>();
            foreach (Note n in notes)
                AddNote(n);
        }

        public void AddMelodySequence(MelodySequence seq)
        {
            foreach (Note n in seq.ToArray())
                AddNote(n);
        }

        public void TrimLeadingRests()
        {
            Note[] seq = new Note[sequence.Count];
            sequence.CopyTo(seq);
            foreach (Note n in seq)
            {
                if (n.Pitch < 0 || n.Velocity == 0)
                    sequence.Remove(n);
                else
                    break;
            }
        }

        public void AddNote(NoteNames name, int octave, Durations d)
        {
            AddNote(new Note(name, octave, d));
        }

        public void AddNote(Note n)
        {
            sequence.Add(n);
            Duration += n.Duration;
        }

        /// <summary>
        /// Returns the total duration of all non silent notes
        /// </summary>
        /// <returns></returns>
        public int TotalNoteDuration()
        {
            int sum = 0;
            foreach(var n in sequence)
            {
                if (!n.IsRest())
                    sum += n.Duration;
            }
            return sum;
        }

        /// <summary>
        /// Returns the total duration of all rest notes
        /// </summary>
        /// <returns></returns>
        public int TotalRestDuration()
        {
            int sum = 0;
            foreach (var n in sequence)
            {
                if (n.IsRest())
                    sum += n.Duration;
            }
            return sum;
        }


        public void AddNotes(IEnumerable<Note> notes)
        {
            foreach (Note n in notes)
            {
                AddNote(n);
            }
        }

        public void AddPause(int d)
        {
            sequence.Add(new Note(-1, d, 0));
            Duration += d;
        }

        public Note[] ToArray()
        {
            return sequence.ToArray();
        }

        public void Transpose(int ht)
        {
            foreach (Note n in sequence)
                if(!n.IsRest())
                    n.Pitch+=ht;
        }

        public void OctaveUp()
        {
            Transpose(12);
        }

        public void OctaveDown()
        {
            Transpose(-12);
        }

        public void ScaleVelocity(float scale)
        {
            foreach (Note n in sequence)
                n.Velocity = (int)((float)n.Velocity * scale);
        }

        public void RemoveLastNote()
        {
            if (sequence.Count > 1)
            {
                var lastNote = sequence[sequence.Count - 1];
                Duration -= lastNote.Duration;
                sequence.Remove(lastNote);
            }
        }

        public string GetNoteStr()
        {
            string noteStr = "";
            foreach (Note n in sequence)
            {
                noteStr += n.ToString();
            }
            return noteStr;
        }

        public override string ToString()
        {
            return GetNoteStr();
        }

        public static string GetNoteStr(IEnumerable<Note> notes)
        {
            string noteStr = "";
            foreach (Note n in notes)
            {
                noteStr += n.ToString();
            }
            return noteStr;
        }

        /// <summary>
        /// Returns the average pitch
        /// </summary>
        /// <returns></returns>
        public double GetPitchAvg()
        {
            double sum = 0;

            for(int i = 0; i < sequence.Count; i++)
            {
                sum += sequence[i].Pitch;
            }

            return sum / sequence.Count;
        }

        /// <summary>
        /// Returns the standard deviation of the pitch
        /// </summary>
        /// <returns></returns>
        public double GetPitchSTD()
        {
            double avg = GetPitchAvg();
            double sum = 0;
            for(int i = 0; i < sequence.Count; i++)
            {
                sum += Math.Pow(sequence[i].Pitch - avg, 2);
            }
            return Math.Sqrt(sum / sequence.Count);
        }

        public PlaybackInfo GeneratePlaybackInfo(byte channel, int time = 0)
        {
            PlaybackInfo info = new PlaybackInfo();
            foreach (Note n in sequence)
            {
                if (n.Pitch < 0 || n.Velocity == 0)
                {
                    time += (int)(1000 * Note.ToRealDuration(n.Duration));
                    continue;
                }
                var tag = Tuple.Create(channel,n);
                PlaybackMessage m = new PlaybackMessage(PlaybackMessage.PlaybackMessageType.Start, channel, (byte)n.Velocity, (byte)n.Pitch, n.Duration);
                m.Tag = tag; // Experimenta
                info.Add(time, m);
                time += (int)(1000 * Note.ToRealDuration(n.Duration));
                m = new PlaybackMessage(PlaybackMessage.PlaybackMessageType.Stop, channel, (byte)n.Velocity, (byte)n.Pitch);
                //m.Tag = tag; // Experimental
                info.Add(time, m);
                time += 1;
            }
            return info;
        }

        
        /// <summary>
        /// Creates a new Melody Sequence for each n notes
        /// </summary>
        /// <param name="n">Period of notes</param>
        /// <returns></returns>
        public MelodySequence[] Split(int n)
        {
            List<MelodySequence> sequences = new List<MelodySequence>();
            for(int i = 0; i < sequence.Count - n; i+=n)
            {
                MelodySequence seq = new MelodySequence();
                for(int j = 0; j < n; j++)
                {
                    seq.AddNote(this.sequence[i + j]);
                }
                sequences.Add(seq);
            }
            return sequences.ToArray();
        }

        public void Trim(int newSize)
        {
            if (newSize < sequence.Count)
            {
                this.sequence.RemoveRange(newSize, sequence.Count - newSize);
            }
        }

        public void NormalizeNotes(int octave)
        {
            foreach(var n in sequence)
            {
                n.StandardizeDuration();
                n.Octave = octave;
            }
        }

        public void StandardizeDuration()
        {
            foreach (var n in sequence)
            {
                n.StandardizeDuration();
                //n.Octave = octave;
            }
        }

        public int CalculateAverageOctave()
        {
            int sum = 0;
            int count = 0;
            foreach(var n in sequence)
            {
                if(!n.IsRest())
                {
                    count++;
                    sum += n.Octave;
                }
            }
            return (int)((double)sum / (double)count);
        }

        public void DoubleSpeed()
        {
            foreach (var n in Notes)
                n.Duration /= 2;

        }

        public void HalveSpeed()
        {
            foreach (var n in Notes)
                n.Duration *= 2;
        }


        public object Clone()
        {
            MelodySequence seq = new MelodySequence();
            foreach (Note n in sequence)
                seq.AddNote(n.Clone() as Note);
            return seq;
        }

        public PlaybackInfo GeneratePlayback()
        {
            return this.GeneratePlaybackInfo(1);
        }

        public IEnumerator<Note> GetEnumerator()
        {
            return sequence.GetEnumerator();
        }
    }
}
