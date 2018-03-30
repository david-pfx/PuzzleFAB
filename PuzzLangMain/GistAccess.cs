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
using System.Net;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PuzzLangMain {
  static class GistAccess {

    // Load a PS game as a piece of JSON given a gist id
    static internal string Load(string id) {
      var json = LoadJson(id);
      if (json == null) return null;
      var pjson = JObject.Parse(json);
      return pjson["files"]["script.txt"]["content"].Value<string>();
    }

    // Load a piece of JSON given a gist id
    static internal string LoadJson(string id) {
      var url = @"https://api.github.com/gists/" + id;
      var request = WebRequest.Create(url) as HttpWebRequest;
      request.Method = "GET";
      request.ContentType = "application/x-www-form-urlencoded";
      request.UserAgent = "PS";
      request.ServicePoint.Expect100Continue = false;
      request.KeepAlive = false;
      var response = request.GetResponse();
      using (var st = new StreamReader(response.GetResponseStream()))
        return st.ReadToEnd();
    }

    // OBS
    static internal string LoadWC(string id) {
      var url = @"https://api.github.com/gists/" + id;
      var client = new WebClient();
      var result = client.DownloadString(url);
      return result;
    }
  }
}
