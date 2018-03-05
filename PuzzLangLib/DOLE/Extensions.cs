/// Puzzlang is a pattern matching language for abstract games and puzzles. See http://www.polyomino.com/puzzlang.
///
/// Copyright © Polyomino Games 2018. All rights reserved.
/// 
/// This is free software. You are free to use it, modify it and/or 
/// distribute it as set out in the licence at http://www.polyomino.com/licence.
/// You should have received a copy of the licence with the software.
/// 
/// This software is distributed in the hope that it will be useful, but with
/// absolutely no warranty, express or implied. See the licence for details.using System;
/// 
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using static System.Math;

namespace DOLE {
  // General extensions
  public static class UtilExtensions {
    public static string Format(this byte[] value) {
      var s = value.Select(b => String.Format("{0:x2}", b));
      return String.Join("", s.ToArray()); // .NET 3.5
    }

    // string join that works on any enumerable
    public static string Join<T>(this IEnumerable<T> values, string delim = ",") {
      return String.Join(delim, values.Select(v => v == null ? "null" : v.ToString()).ToArray()); // .NET 3.5
    }

    // truncate a string if too long
    public static string Shorten(this string argtext, int len) {
      var text = argtext.Replace('\n', '.').Replace('\r', '.');
      if (text.Length <= len) return text;
      return text.Substring(0, len - 3) + "...";
    }

    public static string ShortenLeft(this string argtext, int len) {
      var text = argtext.Replace('\n', '.');
      if (text.Length <= len) return text;
      return "..." + text.Substring(text.Length - len + 3);
    }

    public static string Repeat(this string arg, int count) {
      var tmp = new StringBuilder();
      while (count-- > 0)
        tmp.Append(arg);
      return tmp.ToString();
    }

    public static string Left(this string arg, int count) {
      return arg.Substring(0, Min(count, arg.Length));
    }

    public static string Right(this string arg, int count) {
      return arg.Substring(Max(0, arg.Length - count));
    }

    // return simple split with trim, excluding empty parts
    public static IList<string> SplitTrim(this string target, string delim = ",") {
      var parts = target
        .Split(delim[0])
        .Select(p => p.Trim())
        .Where(p=>p!="");
      return parts.ToList();
    }

    // shorthand for string format
    public static string Fmt(this string format, params object[] args) {
      return String.Format(format, args);
    }

    //----- safe parsing routines, return null on error -----
    public static bool? SafeBoolParse(this string s) {
      bool value;
      return bool.TryParse(s, out value) ? value as bool? : null;
    }

    public static DateTime? SafeDatetimeParse(this string s) {
      DateTime value;
      return DateTime.TryParse(s, out value) ? value as DateTime? : null;
    }

    public static decimal? SafeDecimalParse(this string s) {
      decimal value;
      return Decimal.TryParse(s, out value) ? value as decimal? : null;
    }

    public static double? SafeDoubleParse(this string s) {
      double value;
      return double.TryParse(s, out value) ? value as double? : null;
    }

    public static int? SafeIntParse(this string s) {
      int value;
      return int.TryParse(s, out value) ? value as int? : null;
    }

    public static int? SafeHexParse(this string s) {
      int value;
      return int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value) ? value as int? : null;
    }

    // safe conversion from enum
    // tricky code avoids converting numeric values
    public static T? SafeEnumParse<T>(this string s) where T:struct {
      var index = Array.FindIndex(Enum.GetNames(typeof(T)), n => n.Equals(s, StringComparison.InvariantCultureIgnoreCase));
      if (index >= 0)
        return (Enum.GetValues(typeof(T)) as T[])[index];
      else return null;
    }

    // Simplistic manipulation of MultiDictionary
    public static void AddMulti<T, U>(this Dictionary<T, List<U>> dict, T key, U item) {
      List<U> value;
      if (dict.TryGetValue(key, out value)) value.Add(item);
      else dict.Add(key, new List<U> { item });
    }

    public static List<U> GetMulti<T, U>(this Dictionary<T, List<U>> dict, T key) {
      List<U> value;
      if (dict.TryGetValue(key, out value)) return value;
      else return null;
    }

    public static U SafeLookup<T, U>(this Dictionary<T, U> dict, T key, U other = default(U)) {
      U ret;
      if (dict.TryGetValue(key, out ret)) return ret;
      return other;
    }

    public static bool SafeAdd<T,U>(this Dictionary<T,U> dict, T key, U value) {
      if (dict.ContainsKey(key)) return false;
      dict.Add(key, value);
      return true;
    }

    public static T[] ArrayOf<T>(params T[] args) {
      return args;
    }

    public static List<T> ListOf<T>(params T[] args) {
      return args.ToList();
    }

    // set difference
    public static HashSet<T> Minus<T>(this HashSet<T> hash, HashSet<T> other) {
      return (other == null || other.Count == 0) ? hash
        : new HashSet<T>(hash.Except(other));
    }

    // set intersection
    public static HashSet<T> And<T>(this HashSet<T> hash, HashSet<T> other) {
      return (other == null || other.Count == 0) ? new HashSet<T>()
        : new HashSet<T>(hash.Intersect(other));
    }

  }
}
