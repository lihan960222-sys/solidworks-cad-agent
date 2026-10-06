using System;using System.IO;using System.Collections.Generic;using System.Runtime.InteropServices;using System.Security.Cryptography;using System.Drawing.Printing;using System.Threading;using SolidWorks.Interop.sldworks;
public static class PrintPdf {
 static string Hash(string p){using(var h=SHA256.Create())using(var f=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))return BitConverter.ToString(h.ComputeHash(f));}
 public static object Run(string drawing,string pdf){
  var sw=(ISldWorks)Marshal.GetActiveObject("SldWorks.Application");var doc=(IModelDoc2)sw.ActiveDoc;
  if(doc==null||doc.GetType()!=3||!String.Equals(doc.GetPathName(),drawing,StringComparison.OrdinalIgnoreCase))throw new Exception("Active saved drawing must match input");
  if(doc.GetSaveFlag())throw new Exception("Save or reopen drawing before printing");
  if(!Path.IsPathRooted(pdf)||File.Exists(pdf))throw new Exception("New absolute PDF path required");
  var dr=(IDrawingDoc)doc;var names=(string[])dr.GetSheetNames();if(names.Length!=1)throw new Exception("This verified printer route supports one sheet only");
  var sheet=(ISheet)dr.GetCurrentSheet();var props=(double[])sheet.GetProperties2();double width=props[5]*1000,height=props[6]*1000;
  string printer="Microsoft Print to PDF";var settings=new PrinterSettings();settings.PrinterName=printer;if(!settings.IsValid)throw new Exception("Microsoft Print to PDF is unavailable");
  PaperSize paper=null;foreach(PaperSize p in settings.PaperSizes){double w=p.Width*0.254,h=p.Height*0.254;if(Math.Abs(Math.Min(w,h)-Math.Min(width,height))<0.6&&Math.Abs(Math.Max(w,h)-Math.Max(width,height))<0.6){paper=p;break;}}
  if(paper==null)throw new Exception("Printer has no matching paper; refusing silent scale change");
  string hash=Hash(drawing),oldPrinter=doc.Printer;var ext=doc.Extension;int oldUse=ext.UsePageSetup;var ps=(IPageSetup)doc.PageSetup;
  int oldPaper=ps.PrinterPaperSize,oldOrient=ps.Orientation,oldColor=ps.DrawingColor;bool oldFit=ps.ScaleToFit,oldHigh=ps.HighQuality;double oldScale=ps.Scale2;
  Directory.CreateDirectory(Path.GetDirectoryName(pdf));
  try{
   doc.Printer=printer;ext.UsePageSetup=2;ps.PrinterPaperSize=paper.RawKind;ps.Orientation=width>=height?2:1;ps.ScaleToFit=false;ps.Scale2=100.0;ps.HighQuality=true;ps.DrawingColor=3;
   doc.ClearSelection2(true);doc.GraphicsRedraw2();
   var spec=(IPrintSpecification)ext.GetPrintSpecification();spec.ScaleMethod=1;spec.NumberOfCopies=1;spec.ConvertToHighQuality=true;spec.PrintBackground=false;spec.PrintWhiteItemsBlack=true;spec.PrintCrossHatchOnOutOfDateViews=false;spec.PrintToFile=true;spec.PrinterQueue=printer;spec.PrintFile=pdf;
   ext.PrintOut4(printer,pdf,spec);
  }finally{doc.Printer=oldPrinter;ext.UsePageSetup=oldUse;ps.PrinterPaperSize=oldPaper;ps.Orientation=oldOrient;ps.ScaleToFit=oldFit;ps.Scale2=oldScale;ps.HighQuality=oldHigh;ps.DrawingColor=oldColor;}
  bool valid=false;for(int i=0;i<40;i++){try{using(var f=new FileStream(pdf,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)){var b=new byte[5];f.Read(b,0,5);valid=System.Text.Encoding.ASCII.GetString(b)=="%PDF-";}}catch(IOException){}if(valid)break;Thread.Sleep(500);}
  if(Hash(drawing)!=hash)throw new Exception("Input drawing changed on disk");
  return new Dictionary<string,object>{{"status",valid?"PDF_CREATED_REVIEW_REQUIRED":"PRINT_FAILED"},{"method","SolidWorks PrintOut4 / Microsoft Print to PDF"},{"drawing",drawing},{"pdf",pdf},{"paper",paper.PaperName},{"sheet_width_mm",width},{"sheet_height_mm",height},{"print_scale",1.0},{"original_drawing_hash_unchanged",true},{"settings_restored",true}};
 }
}
