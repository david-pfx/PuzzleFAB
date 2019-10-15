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
using System.Text;
using DOLE;
using PuzzLangLib;

namespace PuzzLangLib {

  internal class TreeNode : Tree<TreeNode> {
    // location index in layer
    internal int Location { get; private set; }
    // order to use for sorting
    //public int Order { get; set; }
    // lookup for references within this tree
    internal Dictionary<int,IList<int>> Refs { get; private set; }

    internal TreeNode Parent { get { return _parent; } }

    public override string ToString() {
      var locations = new List<int>();
      for (var p = this; p != null; p = p.Parent)
        locations.Insert(0, p.Location);
      return $"Trail<{locations.Join()}>";
    }

    internal static TreeNode Create(int location, Dictionary<int,IList<int>> refs = null) {
      return new TreeNode {
        Location = location,
        Refs = refs,
      };
    }

    // set reference to object
    // inherit parent lookup, but can only add to lookup in this node
    internal void SetRef(int index, IList<int> value) {
      Logger.WriteLine(4, "Setref node {0} {1} = {{{2}}}", Location, index, value.Join());
      if (Refs == null) {
        var refs = FindRefs();
        Refs = (refs == null) ? new Dictionary<int, IList<int>>() : new Dictionary<int, IList<int>>(refs);
      }
      Refs.Add(index, value);
    }

    // get objectd given reference
    internal IList<int> GetRef(int index) {
      return FindRefs()[index];
    }

    internal Dictionary<int,IList<int>> FindRefs() {
      for (var node = this; node != null ; node = node.Parent)
        if (node.Refs != null) return node.Refs;
      return null;
    }
  }

  /// <summary>
  /// Implement a tree of pattern matches
  /// Then convert into sorted list for actions
  /// </summary>
  internal class Trail {
    internal bool IsEmpty { get { return _tree.IsEmpty; } }
    internal List<IList<int>> Paths { get { return _paths; } }
    internal IList<int> Current { get { return _pathsindex >= 0 ? _paths[_pathsindex] : null; } }
    public int NodeCount { get { return _nodecount; } }

    public override string ToString() {
      return $"Trail<{_nodecount},{_pathsindex}{_paths.Count}>";
    }

    TreeNode _tree = new TreeNode();
    List<IList<int>> _paths = new List<IList<int>>();
    int _pathsindex = -1;
    int _nodecount = 0;

    internal void Add(TreeNode parent, int value) {
      Logger.WriteLine(4, "Add node {0} to {1}", value, _nodecount);
      _nodecount++;
      (parent ?? _tree).AddChild(TreeNode.Create(value));
    }

    internal void Remove(TreeNode child) {
      Logger.WriteLine(4, "Remove node {0} of {1}", child.Location, _nodecount);
      _nodecount--;
      (child.Parent ?? _tree).RemoveChild(child);
    }

    internal void RemoveLeaf(TreeNode child) {
      Remove(child);
      if (child.Parent != null && child.Parent.IsEmpty)
        RemoveLeaf(child.Parent);
    }

    // generate list of leaves (not dynamic)
    public IList<TreeNode> GetLeaves() {
      var list = new List<TreeNode>();
      _tree.Traverse((i, t) => { if (t.IsEmpty) list.Add(t); });
      return list;
    }

    internal IList<RuleMatch> GetMatches(CompiledRule rule) {
      var matches = new List<RuleMatch>();
      if (!IsEmpty) {
        foreach (var node in GetLeaves()) {
          var refs = node.FindRefs();
          var path = node.GetPath()
            .Skip(1)      // always a zero in the first node
            .Select(n => n.Location).ToList();
          matches.Add(new RuleMatch {
            rule = rule,
            path = path,
            refs = refs });
        }
        if (matches.Count == 0) throw Error.Assert("no paths");
        //if (matches.Count >= 2 &&  paths.Any(p => p.Count != paths[0].Count)) throw Error.Assert("unequal paths");
      }
      return matches;
    }

    private object PathLength(List<int> p) {
      return Math.Abs(p.Last() - p.First());  //BUG: needs level to calculate correctly
    }

    internal bool Next() {
      if (IsEmpty || _pathsindex + 1 >= _paths.Count) return false;
      _pathsindex++;
      return true;
    }
  }
}
