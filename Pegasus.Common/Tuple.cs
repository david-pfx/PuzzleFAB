using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Pegasus.Common {
  // for when we don't have Tuple
  internal class Tuple<T1, T2> : IFormattable {
    internal T1 Item1;
    internal T2 Item2;
    // this ctor is required by the vm
    internal Tuple(T1 item1, T2 item2) {
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
      var other = obj as Tuple<T1, T2>;
      if (object.ReferenceEquals(other, null))
        return false;
      else
        return Item1Comparer.Equals(Item1, other.Item1) && Item2Comparer.Equals(Item2, other.Item2);
    }
    public override string ToString() { return ToString(null, CultureInfo.CurrentCulture); }
    public string ToString(string format, IFormatProvider formatProvider) {
      return string.Format(formatProvider, format ?? "{0},{1}", Item1, Item2);
    }
  }

  // This very odd construct enables type inference to work.
  // http://stackoverflow.com/questions/7120845/equivalent-of-tuple-net-4-for-net-framework-3-5
  internal static class Tuple {
    internal static Tuple<T, U> Create<T, U>(T first, U second) {
      return new Tuple<T, U>(first, second);
    }
  }

}
