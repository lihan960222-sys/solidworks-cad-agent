using System;using System.IO;using System.Collections.Generic;using System.Runtime.InteropServices;using System.Security.Cryptography;using SolidWorks.Interop.sldworks;
public static class GeometryPdf {
 static string Hash(string p){using(var h=SHA256.Create())using(var f=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))return BitConverter.ToString(h.ComputeHash(f));}
 public static string ActivateSaved(string path){var sw=(ISldWorks)Marshal.GetActiveObject("SldWorks.Application");var d=sw.GetOpenDocumentByName(path) as IModelDoc2;int e=0,w=0;if(d==null)d=(IModelDoc2)sw.OpenDoc6(path,3,1,"",ref e,ref w);if(d==null||e!=0||d.GetType()!=3||!String.Equals(d.GetPathName(),path,StringComparison.OrdinalIgnoreCase))throw new Exception("Open saved drawing failed "+e);sw.ActivateDoc3(d.GetTitle(),false,0,ref e);d=(IModelDoc2)sw.ActiveDoc;if(d==null||!String.Equals(d.GetPathName(),path,StringComparison.OrdinalIgnoreCase))throw new Exception("Drawing activation mismatch");return "Active="+d.GetPathName()+" modified="+d.GetSaveFlag();}
 public static string PrepareCopy(string drawing,string copy){
  var sw=(ISldWorks)Marshal.GetActiveObject("SldWorks.Application");var d=(IModelDoc2)sw.ActiveDoc;
  if(d==null||d.GetType()!=3||!String.Equals(d.GetPathName(),drawing,StringComparison.OrdinalIgnoreCase)||d.GetSaveFlag())throw new Exception("Active unmodified saved drawing required; active="+(d==null?"none":d.GetPathName())+" modified="+(d==null?false:d.GetSaveFlag()));
  if(!Path.IsPathRooted(copy)||File.Exists(copy))throw new Exception("New absolute working copy required");
  string hash=Hash(drawing);Directory.CreateDirectory(Path.GetDirectoryName(copy));int e=0,w=0;
  if(!d.Extension.SaveAs(copy,0,1,null,ref e,ref w)||e!=0)throw new Exception("Working copy save failed "+e);
  ReopenCopy(copy);if(Hash(drawing)!=hash)throw new Exception("Original drawing changed");return hash;
 }
 public static void ReopenCopy(string copy){var sw=(ISldWorks)Marshal.GetActiveObject("SldWorks.Application");var d=(IModelDoc2)sw.ActiveDoc;if(d==null||!String.Equals(d.GetPathName(),copy,StringComparison.OrdinalIgnoreCase))throw new Exception("Unexpected active working copy");sw.CloseDoc(d.GetTitle());int e=0,w=0;d=(IModelDoc2)sw.OpenDoc6(copy,3,1,"",ref e,ref w);if(d==null||e!=0)throw new Exception("Working copy reopen failed "+e);sw.ActivateDoc3(d.GetTitle(),false,0,ref e);d.ViewZoomtofit2();d.GraphicsRedraw2();}
 public static bool HashMatches(string path,string hash){return Hash(path)==hash;}
 public static object Run(string drawing,string pdf){
  var sw=(ISldWorks)Marshal.GetActiveObject("SldWorks.Application");var d=(IModelDoc2)sw.ActiveDoc;
  if(d==null||d.GetType()!=3||!String.Equals(d.GetPathName(),drawing,StringComparison.OrdinalIgnoreCase)||d.GetSaveFlag())throw new Exception("Active unmodified saved drawing required");
  if(!Path.IsPathRooted(pdf)||File.Exists(pdf))throw new Exception("New absolute output required");
  string hash=Hash(drawing);var dr=(IDrawingDoc)d;var anns=new List<IAnnotation>();var states=new List<int>();bool ok=false;int e=0,w=0;
  if(((string[])dr.GetSheetNames()).Length!=1)throw new Exception("Single sheet only");Directory.CreateDirectory(Path.GetDirectoryName(pdf));
  try{
   for(var v=(IView)dr.GetFirstView();v!=null;v=(IView)v.GetNextView())foreach(var ao in v.GetAnnotations() as object[]??new object[0]){var a=(IAnnotation)ao;anns.Add(a);states.Add(a.Visible);a.Visible=3;}
   d.ClearSelection2(true);d.ForceRebuild3(false);d.GraphicsRedraw2();var data=(IExportPdfData)sw.GetExportFileData(1);data.ViewPdfAfterSaving=false;data.ExportAs3D=false;if(!data.SetSheets(3,dr.GetSheetNames()))throw new Exception("Sheet selection failed");ok=d.Extension.SaveAs(pdf,0,1,data,ref e,ref w);
  }finally{for(int i=0;i<anns.Count;i++)anns[i].Visible=states[i];d.ForceRebuild3(false);d.GraphicsRedraw2();}
  if(Hash(drawing)!=hash)throw new Exception("Drawing changed on disk");
  if(!ok||e!=0)throw new Exception("Geometry PDF failed: "+e+" warning "+w);
  return new Dictionary<string,object>{{"status","GEOMETRY_EXPORTED_REVIEW_REQUIRED"},{"pdf",pdf},{"api_success",ok},{"error",e},{"warning",w},{"annotation_states_restored",true},{"original_drawing_hash_unchanged",true}};
 }
}
