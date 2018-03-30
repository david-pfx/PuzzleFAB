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
/// General utilities
/// 
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DOLE {
  public static class Util {
    // helper function for varargs
    public static string Join(string delim, params object[] args) {
      return args.Join(delim);
    }

    // varargs string joiner (omits nulls and empty strings)
    public static string JoinNonEmpty(string delim, params object[] args) {
      return String.Join(delim, args
        .Select(a => a == null ? "" : a.ToString())
        .Where(a => a != "")
        .ToArray());  // .NET 3.5
    }

    // Shuffle a sequence using a common RNG
    public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> input, Random rng) {
      var list = new List<T>(input);
      while (list.Count > 0) {
        var x = rng.Next(list.Count);
        yield return list[x];
        list.RemoveAt(x);
      }
    }
  }

  // for when we don't have Pair
  public class Pair<T1, T2> : IFormattable {
    public T1 Item1;
    public T2 Item2;
    // this ctor is required by the vm
    public Pair(T1 item1, T2 item2) {
      Item1 = item1;
      Item2 = item2;
    }
    private static readonly IEqualityComparer<T1> Item1Comparer = EqualityComparer<T1>.Default;
    private static readonly IEqualityComparer<T2> Item2Comparer = EqualityComparer<T2>.Default;

    public override int GetHashCode() {
      var hc = 0;
      if (!object.ReferenceEquals(Item1, null))
        hc = Item1Comparer.GetHashCode(Item1);
      if (!object.ReferenceEquals(Item2, null))
        hc = (hc << 3) ^ Item2Comparer.GetHashCode(Item2);
      return hc;
    }
    public override bool Equals(object obj) {
      var other = obj as Pair<T1, T2>;
      if (object.ReferenceEquals(other, null))
        return false;
      else
        return Item1Comparer.Equals(Item1, other.Item1) && Item2Comparer.Equals(Item2, other.Item2);
    }
    public override string ToString() { return ToString(null, CultureInfo.CurrentCulture); }
    public string ToString(string format, IFormatProvider formatProvider) {
      return string.Format(formatProvider, format ?? "{0}:{1}", Item1, Item2);
    }
  }

  // This very odd construct enables type inference to work.
  // http://stackoverflow.com/questions/7120845/equivalent-of-tuple-net-4-for-net-framework-3-5
  public static class Pair {
    public static Pair<T, U> Create<T, U>(T first, U second) {
      return new Pair<T, U>(first, second);
    }
  }

  // type that just means optional
  // relies on being class, and null means missing (value types are too hard!)
  public struct Maybe<T> where T : class {
    public T Value;
    public bool IsNull { get { return Value == null; } }
    public static Maybe<T> Null { get { return new Maybe<T>() { Value = null }; } }
    public static Maybe<T> Create(T body) { return new Maybe<T> { Value = body }; }
    public override string ToString() {
      return IsNull ? "null" : Value.ToString();
    }
  }
}
