/// Library to generate programmatic sounds for games
/// Converted to C# by Polomino Games for use in Puzzlang

using System;
using System.Collections.Generic;

namespace NsfxrLib {
  public class RNG {
    byte stateI = new byte();
    byte stateJ = new byte();
    byte[] table = new byte[256];

    public RNG(List<byte> seed) {
      this.stateI = 0;
      this.stateJ = 0;
      for (int i = 0; i < 256; i++) {
        table[i] = (byte)i;
      }
      int j = 0;
      for (int i = 0; i < 256; i++) {
        j += table[i] + seed[i % seed.Count];
        Swap(ref table[i], ref table[j % 256]);
      }
    }
    public RNG(string seed) : this(ToBytes(seed)) { }

    public RNG(int seed) : this(ToBytes(seed)) { }

    public double Uniform() {
      int BYTES = 7; // 56 bits to make a 53-bit double
      double output = 0.0;
      for (int i = 0; i < BYTES; i++) {
        output *= 256.0;
        output += NextByte();
      }
      return output / (Math.Pow(2.0, BYTES * 8.0) - 1.0);
    }
    public int Irnd(int max) {
      return (int)(Uniform() * max);
    }
    public int Rnd(int max) {
      return (int)(Uniform() * (max + 1));
    }
    public float Frnd(double max) {
      return (float)Uniform() * (float)max;
    }

    byte NextByte() {
      stateI++;
      stateJ += table[stateI];
      Swap(ref table[stateI], ref table[stateJ]);
      int index = table[stateI] + table[stateJ];
      return table[index % 256];
    }

    void Swap(ref byte v1, ref byte v2) {
      byte temp = v1;
      v1 = v2;
      v2 = temp;
    }

    static List<byte> ToBytes(string txt) {
      List<byte> v = new List<byte>();
      foreach (byte c in txt) {
        v.Add(c);
      }
      return v;
    }

    static List<byte> ToBytes(int i) {
      return ToBytes(i.ToString());
    }

  }
}