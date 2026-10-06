using System;using System.IO;using System.Collections.Generic;using System.Runtime.InteropServices;using System.Security.Cryptography;using System.Drawing.Text;using SolidWorks.Interop.sldworks;
public static class FontPdf {
 static Dictionary<string,object> D(params object[] a){var r=new Dictionary<string,object>();for(int i=0;i<a.Length;i+=2)r[(string)a[i]]=a[i+1];return r;}
 static object[] A(object o){return o as object[]??new object[0];}
 static string Hash(string p){using(var h=SHA256.Create())using(var f=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))return BitConverter.ToString(h.ComputeHash(f));}
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
 public static object Run(string input,string drawingPath,string pdfPath,string font,int[] textFormatIds){
  var sw=(ISldWorks)Marshal.GetActiveObject("SldWorks.Application");var doc=(IModelDoc2)sw.ActiveDoc;
  Check(doc!=null&&doc.GetType()==3&&string.Equals(doc.GetPathName(),input,StringComparison.OrdinalIgnoreCase),"Unexpected active drawing");
  Check(!File.Exists(drawingPath)&&!File.Exists(pdfPath),"Refusing existing outputs");Check(Path.IsPathRooted(drawingPath)&&Path.IsPathRooted(pdfPath),"Absolute outputs required");
  bool installed=false;using(var fc=new InstalledFontCollection())foreach(var f in fc.Families)if(f.Name==font)installed=true;Check(installed,"Font must match an installed family name");
  string hash=Hash(input);var dr=(IDrawingDoc)doc;var replaced=new Dictionary<string,int>();Action<string> remember=s=>{if(!replaced.ContainsKey(s))replaced[s]=0;replaced[s]++;};int defaults=0,formats=0,cells=0;var skipped=new List<int>();
  foreach(int id in textFormatIds){ITextFormat tf=null;try{tf=(ITextFormat)doc.GetUserPreferenceTextFormat(id);}catch{skipped.Add(id);continue;}if(tf==null){skipped.Add(id);continue;}remember(tf.TypeFaceName);tf.TypeFaceName=font;Check(doc.SetUserPreferenceTextFormat(id,tf),"Document font setting failed: "+id);defaults++;}
  foreach(string sheet in dr.GetSheetNames() as string[]??new string[0]){Check(dr.ActivateSheet(sheet),"Activate sheet failed");
  for(var view=(IView)dr.GetFirstView();view!=null;view=(IView)view.GetNextView()){
   foreach(var ao in A(view.GetAnnotations())){var an=(IAnnotation)ao;for(int i=0;i<an.GetTextFormatCount();i++){var tf=(ITextFormat)an.GetTextFormat(i);if(tf==null)continue;remember(tf.TypeFaceName);tf.TypeFaceName=font;Check(an.SetTextFormat(i,false,tf),"Annotation font failed");formats++;}}
   foreach(var to in A(view.GetTableAnnotations())){var table=(ITableAnnotation)to;var tf=table.GetTextFormat();remember(tf.TypeFaceName);tf.TypeFaceName=font;Check(table.SetTextFormat(false,tf),"Table font failed");for(int row=0;row<table.RowCount;row++)for(int col=0;col<table.ColumnCount;col++){var cf=table.GetCellTextFormat(row,col);remember(cf.TypeFaceName);cf.TypeFaceName=font;Check(table.SetCellTextFormat(row,col,false,cf),"Cell font failed");cells++;}}}}
  dr.ActivateView("");doc.ClearSelection2(true);doc.ForceRebuild3(false);doc.GraphicsRedraw2();Directory.CreateDirectory(Path.GetDirectoryName(drawingPath));int e=0,w=0;Check(doc.Extension.SaveAs(drawingPath,0,1,null,ref e,ref w)&&e==0,"Drawing save failed: "+e);
  sw.CloseDoc(doc.GetTitle());e=0;w=0;doc=(IModelDoc2)sw.OpenDoc6(drawingPath,3,1,"",ref e,ref w);Check(doc!=null&&e==0,"Reopen changed-font drawing failed");dr=(IDrawingDoc)doc;sw.ActivateDoc3(doc.GetTitle(),false,0,ref e);Directory.CreateDirectory(Path.GetDirectoryName(pdfPath));var pdf=(IExportPdfData)sw.GetExportFileData(1);pdf.ViewPdfAfterSaving=false;pdf.ExportAs3D=false;Check(pdf.SetSheets(3,dr.GetSheetNames()),"PDF sheet selection failed");e=0;w=0;bool ok=doc.Extension.SaveAs(pdfPath,0,1,pdf,ref e,ref w);
  Check(Hash(input)==hash,"Original drawing file changed");doc.ViewZoomtofit2();doc.GraphicsRedraw2();
  return D("status",ok&&e==0?"EXPORTED_REVIEW_REQUIRED":"PDF_FAILED","font",font,"replaced",replaced,"document_formats",defaults,"unsupported_format_ids",skipped,"annotation_formats",formats,"table_cells",cells,"drawing",drawingPath,"pdf",pdfPath,"pdf_api_success",ok,"pdf_error",e,"pdf_warning",w,"pdf_exists",File.Exists(pdfPath),"original_drawing_hash_unchanged",true);
 }
}
