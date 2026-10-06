using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.IO;
using SolidWorks.Interop.sldworks;

public static class DrawingDocuments {
 public static string[] Titles() {
  var sw=(ISldWorks)Marshal.GetActiveObject("SldWorks.Application");
  var titles=new List<string>();
  foreach(var o in sw.GetDocuments() as object[]??new object[0]) titles.Add(((IModelDoc2)o).GetTitle());
  return titles.ToArray();
 }
 // Close only exact, newly created output paths. A modified source is always kept.
 public static int CloseCompleted(string[] ownedPaths,string source,string createdTitle,string[] before) {
  var sw=(ISldWorks)Marshal.GetActiveObject("SldWorks.Application");
  var originals=new HashSet<string>(before,StringComparer.OrdinalIgnoreCase);
  var owned=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  foreach(string p in ownedPaths) if(!String.IsNullOrEmpty(p)) owned.Add(Path.GetFullPath(p));
  var close=new List<string>();
  foreach(var o in sw.GetDocuments() as object[]??new object[0]) {
   var d=(IModelDoc2)o;string p=d.GetPathName();
   bool isSource=!String.IsNullOrEmpty(source)&&String.Equals(p,source,StringComparison.OrdinalIgnoreCase);
   if(isSource&&d.GetSaveFlag()) continue;
   bool generated=!String.IsNullOrEmpty(p)&&owned.Contains(Path.GetFullPath(p));
   bool newUnsaved=String.IsNullOrEmpty(p)&&d.GetType()==3&&d.GetTitle()==createdTitle&&!originals.Contains(d.GetTitle());
   if(generated||newUnsaved||isSource) close.Add(d.GetTitle());
  }
  foreach(string title in close) sw.CloseDoc(title);
  return close.Count;
 }
}
