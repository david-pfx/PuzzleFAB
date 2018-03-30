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
// see https://stackoverflow.com/questions/66893/tree-data-structure-in-c-sharp
using System;
using System.Collections.Generic;

namespace DOLE {
  /// <summary>
  /// Implementation of generic tree class
  /// Node T must be a subclass of Tree<T>
  /// </summary>
  public class Tree<T> where T : Tree<T> {
    protected T _parent = null;
    protected List<T> _children;
    public bool IsEmpty { get { return _children.Count == 0; } }

    // ctor
    public Tree() {
      _children = new List<T>();
    }

    // add child node
    public virtual void AddChild(T child) {
      child._parent = this as T;
      _children.Add(child);
    }

    // remove child node
    public virtual void RemoveChild(T child) {
      _children.Remove(child);
      //_parent = null;
    }

    // call visitor on every node
    public void Traverse(Action<int, T> visitor) {
      this.traverse(0, visitor);
    }

    protected virtual void traverse(int depth, Action<int, T> visitor) {
      visitor(depth, (T)this);
      foreach (T child in this._children)
        child.traverse(depth + 1, visitor);
    }

    // get path from root to this node
    public virtual IList<T> GetPath() {
      var list = new List<T>();
      for (T node = this as T; node != null; node = node._parent)
        list.Insert(0, node);
      return list;
    }
  }
}
