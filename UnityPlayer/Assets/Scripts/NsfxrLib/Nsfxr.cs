/// Puzzlang is a pattern matching language for abstract games and puzzles. See http://www.polyomino.com/puzzlang.
///
/// Copyright © Polyomino Games 2018. All rights reserved.
/// 
/// This is free software. You are free to use it, modify it and/or 
/// distribute it as set out in the licence at http://www.polyomino.com/licence.
/// You should have received a copy of the licence with the software.
/// 
/// This software is distributed in the hope that it will be useful, but with
/// absolutely no warranty, express or implied. See the licence for details.
/// 
using System;
using System.Collections.Generic;
using System.Linq;
using DOLE;
using PuzzLangLib;
using UnityEngine;

namespace NsfxrLib {
  public static class Nsfxr {

    static readonly Dictionary<int, Func<RNG, Patch>> _seedlookup = new Dictionary<int, Func<RNG, Patch>> {
      { 0, r=>Global.PickupCoin(r) },
      { 1, r=>Global.LaserShoot(r) },
      { 2, r=>Global.Explosion(r) },
      { 3, r=>Global.PowerUp(r) },
      { 4, r=>Global.HitHurt(r) },
      { 5, r=>Global.Jump(r) },
      { 6, r=>Global.BlipSelect(r) },
      { 7, r=>Global.PushSound(r) },
      { 8, r=>Global.Random(r) },
      { 9, r=>Global.Bird(r) },
    };

    // call with seed RRRRGG where R is random seed and G is generator
    public static AudioClip Generate(int nseed) {
      var rng = new RNG(nseed / 100);
      var patch = _seedlookup[nseed % 10](rng);
      var gen = new Generator(patch);
      var samples = gen.Generate();
      Util.Trace(3, "seed: {0} samples {1}\n{2}", nseed, samples.Count, patch);
      return CreateClip(nseed.ToString(), (int)Global.SampleRate, 1, samples);
    }

    static AudioClip CreateClip(string name, int frequency, int channels, IList<float> samples) {
      var clip = AudioClip.Create(name, samples.Count, channels, frequency, false);
      clip.SetData(samples.ToArray(), 0);
      return clip;
    }
  }
}
