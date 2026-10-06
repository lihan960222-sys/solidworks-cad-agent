using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using SolidWorks.Interop.sldworks;

public static class DrawingEngine {
 static ISldWorks sw; static IModelDoc2 drawing; static IDrawingDoc dr;
 static Dictionary<string,IView> views;
 public static void ReleaseSessionReferences(){views=null;dr=null;drawing=null;sw=null;}
 static Dictionary<string,object> D(params object[] a){var r=new Dictionary<string,object>();for(int i=0;i<a.Length;i+=2)r[(string)a[i]]=a[i+1];return r;}
 static object[] A(object o){return o as object[]??new object[0];}
 static Dictionary<string,object> Map(object o){return (Dictionary<string,object>)o;}
 static object[] Rows(object o){var a=o as object[];if(a!=null)return a;var b=o as ArrayList;return b==null?new object[0]:b.ToArray();}
 static object[] OptionalRows(Dictionary<string,object> o,string key){return o.ContainsKey(key)?Rows(o[key]):new object[0];}
 static string S(Dictionary<string,object> m,string k){return Convert.ToString(m[k]);}
 static double N(Dictionary<string,object> m,string k){return Convert.ToDouble(m[k]);}
 static double[] Vec(object o){var a=Rows(o);var r=new double[a.Length];for(int i=0;i<a.Length;i++)r[i]=Convert.ToDouble(a[i]);return r;}
 static JavaScriptSerializer Serializer(){var s=new JavaScriptSerializer();s.MaxJsonLength=int.MaxValue;s.RecursionLimit=200;return s;}
 static Dictionary<string,object> Read(string p){return Map(Serializer().DeserializeObject(File.ReadAllText(p)));}
 public static string Hash(string p){using(var h=SHA256.Create())using(var f=File.OpenRead(p))return BitConverter.ToString(h.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
 static string SharedReadHash(string p){using(var h=SHA256.Create())using(var f=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))return BitConverter.ToString(h.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
 static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Position(object o,double width,double height){var p=Vec(o);Require(p.Length==2&&Finite(p[0])&&Finite(p[1])&&p[0]>0&&p[0]<width&&p[1]>0&&p[1]<height,"Invalid/out-of-sheet position");}
 static bool Finite(double n){return !double.IsNaN(n)&&!double.IsInfinity(n);}
 static void Positive(double n){Require(Finite(n)&&n>0,"Invalid positive size/scale");}
 static void Text(Dictionary<string,object> m,string k){Require(!string.IsNullOrWhiteSpace(S(m,k)),"Missing text: "+k);}
 static IModelDoc2 Source(string path,string cfg,out bool wasOpen,out string oldCfg){
  wasOpen=sw.GetOpenDocumentByName(path)!=null;int e=0,w=0;
  var model=wasOpen?(IModelDoc2)sw.GetOpenDocumentByName(path):(IModelDoc2)sw.OpenDoc6(path,1,3,cfg,ref e,ref w);
  Require(model!=null,"Open source failed: "+e);Require(model.GetType()==1,"Only part documents supported");
  Require(string.Equals(Path.GetFullPath(model.GetPathName()),Path.GetFullPath(path),StringComparison.OrdinalIgnoreCase),"Unexpected source identity");
  oldCfg=model.ConfigurationManager.ActiveConfiguration.Name;
  Require(!model.GetSaveFlag(),"Source has unsaved changes: save it manually before inspection/execution");
  if(!string.IsNullOrEmpty(cfg)&&oldCfg!=cfg)Require(model.ShowConfiguration2(cfg),"Unknown configuration");
  return model;
 }
 static void Restore(IModelDoc2 model,bool wasOpen,string oldCfg){
  if(model==null)return;
  if(wasOpen){if(model.ConfigurationManager.ActiveConfiguration.Name!=oldCfg)model.ShowConfiguration2(oldCfg);}
  else sw.CloseDoc(model.GetTitle());
 }
 public static Dictionary<string,object> Inspect(string path,string cfg){
  sw=(ISldWorks)Marshal.GetActiveObject("SldWorks.Application");
  if(string.IsNullOrEmpty(path)){var active=(IModelDoc2)sw.ActiveDoc;Require(active!=null&&active.GetType()==1,"Active document must be a saved part, or supply Source explicitly");path=active.GetPathName();}
  Require(Path.IsPathRooted(path)&&File.Exists(path),"Saved absolute source path required");
  bool wasOpen=false;string oldCfg="";IModelDoc2 model=null;string before=Hash(path);
  try{
   model=Source(path,cfg,out wasOpen,out oldCfg);cfg=model.ConfigurationManager.ActiveConfiguration.Name;
   var facts=Map(InspectGeometry.Run(path,cfg,"source"));
   facts["schema_version"]=1;facts["document_type"]="part";facts["sha256"]=before;
   facts["solidworks_revision"]=sw.RevisionNumber();facts["model_views"]=model.GetModelViewNames();
   facts["material_name"]=model.MaterialUserName;
   var bb=(double[])facts["bounds_si"];var mm=new double[6];for(int i=0;i<6;i++)mm[i]=bb[i]*1000;facts["bounds_mm"]=mm;
   Require(Finite(mm[0]),"No solid bodies found");facts["status"]="INSPECTED";
   Require(Hash(path)==before,"Source file changed during inspection");return facts;
  }finally{Restore(model,wasOpen,oldCfg);}
 }
 static Dictionary<int,Dictionary<string,object>> Edges(Dictionary<string,object> facts){var r=new Dictionary<int,Dictionary<string,object>>();foreach(var eo in Rows(facts["edges"])){var e=Map(eo);r.Add(Convert.ToInt32(e["id"]),e);}return r;}
 static void Validate(Dictionary<string,object> p,Dictionary<string,object> f){
  Require(Convert.ToInt32(p["version"])==1&&Convert.ToInt32(f["schema_version"])==1&&S(f,"document_type")=="part","Unsupported schema");
  Require(File.Exists(S(p,"template"))&&S(p,"template").EndsWith(".drwdot",StringComparison.OrdinalIgnoreCase),"Template missing");
  Require(Path.IsPathRooted(S(p,"output_drawing"))&&S(p,"output_drawing").EndsWith(".slddrw",StringComparison.OrdinalIgnoreCase)&&!File.Exists(S(p,"output_drawing")),"Output drawing must be a new absolute SLDDRW path");
  if(p.ContainsKey("output_pdf"))Require(Path.IsPathRooted(S(p,"output_pdf"))&&S(p,"output_pdf").EndsWith(".pdf",StringComparison.OrdinalIgnoreCase)&&!File.Exists(S(p,"output_pdf")),"PDF path must be new and absolute");
  var sh=Map(p["sheet"]);double w=N(sh,"width_mm"),h=N(sh,"height_mm");Positive(w);Positive(h);Require(sh["first_angle"] is bool,"Projection convention missing");
  var known=new HashSet<string>();foreach(var v in Rows(f["model_views"]))known.Add(Convert.ToString(v));var ids=new HashSet<string>();
  Require(Rows(p["views"]).Length>0,"No model views");
  foreach(var o in Rows(p["views"])){var v=Map(o);Text(v,"id");Require(ids.Add(S(v,"id")),"Duplicate view ID");Require(known.Contains(S(v,"model_view")),"Unknown model view");Position(v["position_mm"],w,h);Positive(N(v,"scale"));Text(v,"reason");}
  foreach(var o in Rows(p["sections"])){var s=Map(o);Require(ids.Contains(S(s,"parent")),"Unknown section parent");Text(s,"id");Require(ids.Add(S(s,"id")),"Duplicate section ID");Text(s,"label");Position(s["position_mm"],w,h);Positive(N(s,"scale"));Text(s,"reason");var ends=Rows(s["line_model_mm"]);Require(ends.Length==2,"Need two section endpoints");var a=Vec(ends[0]);var b=Vec(ends[1]);Require(a.Length==3&&b.Length==3,"Need 3D endpoints");double len=0;for(int i=0;i<3;i++){Require(Finite(a[i])&&Finite(b[i]),"Nonfinite endpoint");len+=(a[i]-b[i])*(a[i]-b[i]);}Require(len>1e-6,"Degenerate section");}
  foreach(var o in Rows(p["dimensions"])){var d=Map(o);Require(ids.Contains(S(d,"view")),"Unknown dimension view");Require(S(d,"direction")=="horizontal"||S(d,"direction")=="vertical","Unsupported dimension direction");Position(d["position_mm"],w,h);Positive(N(d,"expected_mm"));Text(d,"evidence");}
  var edges=Edges(f);
  foreach(var o in Rows(p["labels"])){var t=Map(o);Require(ids.Contains(S(t,"view")),"Unknown label view");int id=Convert.ToInt32(t["edge_id"]);Require(edges.ContainsKey(id)&&S(edges[id],"type")=="circle"&&!string.IsNullOrEmpty(S(edges[id],"persist_ref")),"Invalid circular edge reference");Text(t,"text");var offset=Vec(t["offset_mm"]);Require(offset.Length==2&&Finite(offset[0])&&Finite(offset[1]),"Invalid label offset");}
  foreach(var o in OptionalRows(p,"diameters")){var t=Map(o);Require(ids.Contains(S(t,"view")),"Unknown diameter view");int id=Convert.ToInt32(t["edge_id"]);Require(edges.ContainsKey(id)&&S(edges[id],"type")=="circle","Invalid diameter edge");Position(t["position_mm"],w,h);Positive(N(t,"expected_mm"));Text(t,"evidence");Require(Math.Abs(Vec(edges[id]["parameters_si"])[6]*2000-N(t,"expected_mm"))<1e-5,"Diameter differs from source circle");}
  foreach(var o in Rows(p["tables"])){var t=Map(o);Position(t["position_mm"],w,h);Text(t,"evidence");Require(S(t,"role")=="nominal_hole_schedule","Unsupported table role");Positive(N(t,"row_height_mm"));var widths=Vec(t["column_widths_mm"]);var rows=Rows(t["rows"]);Require(widths.Length>0&&rows.Length>0,"Empty table");double total=0;foreach(double x in widths){Positive(x);total+=x;}foreach(var row in rows){Require(Rows(row).Length==widths.Length,"Unequal table cells");foreach(var cell in Rows(row))Require(cell is string,"Table cell must be string");}var pos=Vec(t["position_mm"]);Require(pos[0]+total<w&&pos[1]-rows.Length*N(t,"row_height_mm")>0,"Table exceeds sheet");}
  foreach(var o in Rows(p["notes"])){var n=Map(o);Position(n["position_mm"],w,h);Text(n,"text");Positive(N(n,"font_mm"));}
 }
 static double[] Project(IView v,double[] xyz){var mu=(IMathUtility)sw.GetMathUtility();return (double[])((IMathPoint)((IMathPoint)mu.CreatePoint(xyz)).MultiplyTransform(v.ModelToViewTransform)).ArrayData;}
 static void Font(IAnnotation a,double mm){var tf=(ITextFormat)a.GetTextFormat(0);tf.TypeFaceName="Microsoft YaHei";tf.CharHeight=mm/1000;a.SetTextFormat(0,false,tf);}
 static INote Note(string text,double[] pos,double size){drawing.ClearSelection2(true);var n=(INote)drawing.InsertNote(text);Require(n!=null,"InsertNote failed");var a=(IAnnotation)n.GetAnnotation();Require(a.SetPosition2(pos[0]/1000,pos[1]/1000,0),"Note position failed");Font(a,size);return n;}
 static IView ModelView(Dictionary<string,object> v,string source,string cfg){var pos=Vec(v["position_mm"]);var result=(IView)dr.CreateDrawViewFromModelView3(source,S(v,"model_view"),pos[0]/1000,pos[1]/1000,0);Require(result!=null,"Model view creation failed");result.ReferencedConfiguration=cfg;result.UseSheetScale=0;result.ScaleDecimal=N(v,"scale");result.SetDisplayMode3(false,2,false,false);result.SetDisplayTangentEdges2(0);return result;}
 static IView Section(Dictionary<string,object> s){
  var parent=views[S(s,"parent")];Require(dr.ActivateView(parent.Name),"Activate section parent failed");drawing.ClearSelection2(true);
  var ends=Rows(s["line_model_mm"]);var mu=(IMathUtility)sw.GetMathUtility();var tf=((ISketch)parent.GetSketch()).ModelToSketchTransform;var pts=new double[2][];
  for(int i=0;i<2;i++){var xyz=Vec(ends[i]);for(int j=0;j<3;j++)xyz[j]/=1000;var sheet=Project(parent,xyz);pts[i]=(double[])((IMathPoint)((IMathPoint)mu.CreatePoint(sheet)).MultiplyTransform(tf)).ArrayData;}
  Require(Math.Abs(pts[0][0]-pts[1][0])+Math.Abs(pts[0][1]-pts[1][1])>1e-9,"Cut line collapses in selected view");
  var seg=(ISketchSegment)drawing.SketchManager.CreateLine(pts[0][0],pts[0][1],0,pts[1][0],pts[1][1],0);Require(seg!=null,"Section line failed");drawing.ClearSelection2(true);Require(seg.Select4(false,null),"Select section line failed");
  var pos=Vec(s["position_mm"]);var sec=(IView)dr.CreateSectionViewAt5(pos[0]/1000,pos[1]/1000,0,S(s,"label"),1,null,0);Require(sec!=null,"Section creation failed");sec.UseParentScale=false;sec.UseSheetScale=0;sec.ScaleDecimal=N(s,"scale");return sec;
 }
 static Dictionary<string,object> Dimension(Dictionary<string,object> spec){
  var view=views[S(spec,"view")];Require(dr.ActivateView(view.Name),"Activate dimension view failed");drawing.ClearSelection2(true);int axis=S(spec,"direction")=="horizontal"?0:1;
  IVertex low=null,high=null;double[] lp=null,hp=null;
  foreach(var co in A(view.GetVisibleComponents()))foreach(var vo in A(view.GetVisibleEntities2((Component2)co,2))){var vertex=vo as IVertex;if(vertex==null)continue;var pos=Project(view,(double[])vertex.GetPoint());if(low==null||pos[axis]<lp[axis]-1e-9||(Math.Abs(pos[axis]-lp[axis])<1e-9&&pos[1-axis]<lp[1-axis])){low=vertex;lp=pos;}if(high==null||pos[axis]>hp[axis]+1e-9||(Math.Abs(pos[axis]-hp[axis])<1e-9&&pos[1-axis]<hp[1-axis])){high=vertex;hp=pos;}}
  Require(low!=null&&high!=null&&Math.Abs(lp[axis]-hp[axis])>1e-9,"No usable overall vertices");
  var sd=(ISelectData)((ISelectionMgr)drawing.SelectionManager).CreateSelectData();sd.View=(View)view;Require(((IEntity)low).Select4(false,(SelectData)sd)&&((IEntity)high).Select4(true,(SelectData)sd),"Dimension vertex selection failed");
  var xy=Vec(spec["position_mm"]);var dd=(IDisplayDimension)(axis==0?drawing.AddHorizontalDimension2(xy[0]/1000,xy[1]/1000,0):drawing.AddVerticalDimension2(xy[0]/1000,xy[1]/1000,0));Require(dd!=null,"Associated dimension failed");
  double actual=((IDimension)dd.GetDimension2(0)).SystemValue*1000;Require(Math.Abs(actual-N(spec,"expected_mm"))<1e-5,"Dimension mismatch: "+actual+" expected "+N(spec,"expected_mm"));Font((IAnnotation)dd.GetAnnotation(),3.5);drawing.ClearSelection2(true);
  return D("view",S(spec,"view"),"direction",S(spec,"direction"),"expected_mm",N(spec,"expected_mm"),"actual_mm",actual,"status","PASS");
 }
 static IEdge VisibleCircle(Dictionary<string,object> tag,Dictionary<int,Dictionary<string,object>> edges,IModelDoc2 source){
  var v=views[S(tag,"view")];var edgeFact=edges[Convert.ToInt32(tag["edge_id"])];int error=0;var target=source.Extension.GetObjectByPersistReference3(Convert.FromBase64String(S(edgeFact,"persist_ref")),out error) as IEdge;Require(target!=null&&error==0,"Persistent edge reference cannot resolve");
  var targetRef=S(edgeFact,"persist_ref");IEdge visible=null;var targetParams=(double[])((ICurve)target.GetCurve()).CircleParams;var geometricMatches=new List<IEdge>();
  foreach(var co in A(v.GetVisibleComponents()))foreach(var eo in A(v.GetVisibleEntities2((Component2)co,1))){var ed=eo as IEdge;if(ed==null)continue;var data=source.Extension.GetPersistReference3(ed) as byte[];if(data!=null&&Convert.ToBase64String(data)==targetRef)visible=ed;var curve=(ICurve)ed.GetCurve();if(!curve.IsCircle())continue;var cp=(double[])curve.CircleParams;bool same=true;foreach(int i in new[]{0,1,2,6})if(Math.Abs(cp[i]-targetParams[i])>1e-8)same=false;double dot=cp[3]*targetParams[3]+cp[4]*targetParams[4]+cp[5]*targetParams[5];if(Math.Abs(Math.Abs(dot)-1)>1e-7)same=false;if(same)geometricMatches.Add(ed);}
  // Drawing-context edge proxies can have different persistent bytes. Resolve the source
  // object first, then require a unique full 3D circle match (never XY/radius alone).
  if(visible==null){Require(geometricMatches.Count==1,"Ambiguous/missing 3D circle correspondence: "+tag["edge_id"]);visible=geometricMatches[0];}
  Require(visible!=null,"Requested edge is not visible in label view: "+tag["edge_id"]);return visible;
 }
 static Dictionary<string,object> Diameter(Dictionary<string,object> spec,Dictionary<int,Dictionary<string,object>> edges,IModelDoc2 source){
  var v=views[S(spec,"view")];var edge=VisibleCircle(spec,edges,source);Require(dr.ActivateView(v.Name),"Activate diameter view failed");drawing.ClearSelection2(true);var sd=(ISelectData)((ISelectionMgr)drawing.SelectionManager).CreateSelectData();sd.View=(View)v;Require(((IEntity)edge).Select4(false,(SelectData)sd),"Diameter edge selection failed");var pos=Vec(spec["position_mm"]);var dd=(IDisplayDimension)drawing.AddDiameterDimension2(pos[0]/1000,pos[1]/1000,0);Require(dd!=null,"Native diameter creation failed");double actual=((IDimension)dd.GetDimension2(0)).SystemValue*1000;Require(Math.Abs(actual-N(spec,"expected_mm"))<1e-5,"Diameter mismatch: "+actual);Font((IAnnotation)dd.GetAnnotation(),3.5);drawing.ClearSelection2(true);return D("view",S(spec,"view"),"direction","diameter","expected_mm",N(spec,"expected_mm"),"actual_mm",actual,"status","PASS");
 }
 static void Label(Dictionary<string,object> tag,Dictionary<int,Dictionary<string,object>> edges,IModelDoc2 source){
  var v=views[S(tag,"view")];var visible=VisibleCircle(tag,edges,source);Require(dr.ActivateView(v.Name),"Activate label view failed");drawing.ClearSelection2(true);
  var sd=(ISelectData)((ISelectionMgr)drawing.SelectionManager).CreateSelectData();sd.View=(View)v;Require(((IEntity)visible).Select4(false,(SelectData)sd),"Label edge selection failed");
  var note=(INote)drawing.InsertNote(S(tag,"text"));Require(note!=null,"Label note failed");var visibleCircle=(double[])((ICurve)visible.GetCurve()).CircleParams;var center=Project(v,new[]{visibleCircle[0],visibleCircle[1],visibleCircle[2]});var offset=Vec(tag["offset_mm"]);var an=(IAnnotation)note.GetAnnotation();Require(an.SetPosition2(center[0]+offset[0]/1000,center[1]+offset[1]/1000,0),"Label position failed");Font(an,3);an.SetLeader3(1,0,true,false,false,false);drawing.ClearSelection2(true);
 }
 static void Table(Dictionary<string,object> spec){dr.ActivateView("");drawing.ClearSelection2(true);var rows=Rows(spec["rows"]);var widths=Vec(spec["column_widths_mm"]);var pos=Vec(spec["position_mm"]);var t=(ITableAnnotation)dr.InsertTableAnnotation2(false,pos[0]/1000,pos[1]/1000,1,"",rows.Length,widths.Length);Require(t!=null,"Native general table failed");for(int i=0;i<rows.Length;i++){var cells=Rows(rows[i]);for(int j=0;j<widths.Length;j++)t.Text[i,j]=(string)cells[j];t.SetRowHeight(i,N(spec,"row_height_mm")/1000,0);}for(int j=0;j<widths.Length;j++)t.SetColumnWidth(j,widths[j]/1000,0);var tf=t.GetTextFormat();tf.TypeFaceName="Microsoft YaHei";tf.CharHeight=.003;t.SetTextFormat(false,tf);}
 static List<object> AnnotationCounts(IModelDoc2 doc){var result=new List<object>();for(var v=(IView)((IDrawingDoc)doc).GetFirstView();v!=null;v=(IView)v.GetNextView()){int n=0,d=0,t=0,dangling=0;foreach(var ao in A(v.GetAnnotations())){var a=(IAnnotation)ao;if(a.GetSpecificAnnotation() is INote)n++;if(a.GetSpecificAnnotation() is IDisplayDimension)d++;if(a.GetSpecificAnnotation() is ITableAnnotation)t++;if(a.IsDangling())dangling++;}result.Add(D("view",v.Name,"notes",n,"dimensions",d,"tables",t,"dangling",dangling));}return result;}
 static void ReopenCheck(Dictionary<string,object> p,Dictionary<string,object> report){
  string output=S(p,"output_drawing");sw.CloseDoc(drawing.GetTitle());int e=0,w=0;drawing=(IModelDoc2)sw.OpenDoc6(output,3,1,"",ref e,ref w);Require(drawing!=null&&e==0,"Saved drawing reopen failed: "+e);dr=(IDrawingDoc)drawing;
  Require(string.Equals(drawing.GetPathName(),output,StringComparison.OrdinalIgnoreCase),"Reopened wrong document");var counts=AnnotationCounts(drawing);int dims=0,tables=0,notes=0,dangling=0;foreach(var row in counts){var m=Map(row);dims+=Convert.ToInt32(m["dimensions"]);tables+=Convert.ToInt32(m["tables"]);notes+=Convert.ToInt32(m["notes"]);dangling+=Convert.ToInt32(m["dangling"]);}
  Require(dims==Rows(p["dimensions"]).Length+OptionalRows(p,"diameters").Length&&tables==Rows(p["tables"]).Length&&notes>=Rows(p["notes"]).Length+Rows(p["labels"]).Length,"Reopened annotation counts mismatch");Require(dangling==0,"Dangling annotations after reopen");
  var actual=new List<object>();int vc=0,dc=0;string source=S(Read(S(p,"facts")),"path"),cfg=S(Read(S(p,"facts")),"configuration");
  for(var v=(IView)dr.GetFirstView();v!=null;v=(IView)v.GetNextView()){if(vc++==0)continue;Require(string.Equals(v.GetReferencedModelName(),source,StringComparison.OrdinalIgnoreCase)&&v.ReferencedConfiguration==cfg,"Drawing source/configuration reference mismatch");for(var dd=(IDisplayDimension)v.GetFirstDisplayDimension5();dd!=null;dd=(IDisplayDimension)dd.GetNext5()){double value=((IDimension)dd.GetDimension2(0)).SystemValue*1000;actual.Add(value);dc++;}}
  Require(vc-1==Rows(p["views"]).Length+Rows(p["sections"]).Length&&dc==dims,"Reopened views/dimensions mismatch");var expected=new List<double>();foreach(var o in Rows(p["dimensions"]))expected.Add(N(Map(o),"expected_mm"));foreach(var o in OptionalRows(p,"diameters"))expected.Add(N(Map(o),"expected_mm"));var measured=new List<double>();foreach(var o in actual)measured.Add(Convert.ToDouble(o));expected.Sort();measured.Sort();for(int i=0;i<expected.Count;i++)Require(Math.Abs(expected[i]-measured[i])<1e-5,"Reopened dimension value mismatch");
  report["reopen"]=D("status","PASS","annotation_counts",counts,"dimension_values_mm",actual,"model_references","PASS","view_count",vc-1);
  sw.Visible=true;sw.ActivateDoc3(drawing.GetTitle(),false,0,ref e);dr.EditSheet();dr.ActivateView("");drawing.ClearSelection2(true);drawing.ViewZoomtofit2();drawing.GraphicsRedraw2();
 }
 public static Dictionary<string,object> VerifySaved(string planPath){
  var report=D("status","FAILED","plan",planPath,"visual_review","NOT_CHECKED","manufacturing_release","NOT_APPROVED");
  try{
   var p=Read(planPath);var f=Read(S(p,"facts"));string path=S(p,"output_drawing");Require(Path.IsPathRooted(path)&&File.Exists(path),"Saved native drawing required");Require(SharedReadHash(S(f,"path"))==S(f,"sha256"),"Source changed since inspection");string nativeBefore=SharedReadHash(path);
   sw=(ISldWorks)Marshal.GetActiveObject("SldWorks.Application");drawing=sw.GetOpenDocumentByName(path) as IModelDoc2;int e=0,w=0;if(drawing==null)drawing=sw.OpenDoc6(path,3,1,"",ref e,ref w) as IModelDoc2;Require(drawing!=null&&!drawing.GetSaveFlag(),"Cannot verify an unsaved/modified drawing");dr=(IDrawingDoc)drawing;
   var sourceDoc=sw.GetOpenDocumentByName(S(f,"path")) as IModelDoc2;Require(sourceDoc==null||!sourceDoc.GetSaveFlag(),"Referenced source has unsaved changes");ReopenCheck(p,report);Require(SharedReadHash(path)==nativeBefore,"Input native drawing changed");Require(SharedReadHash(S(f,"path"))==S(f,"sha256"),"Source changed during verification");report["source_hash_unchanged"]=true;report["native_hash_unchanged"]=true;report["status"]="NATIVE_REOPEN_VERIFIED";
  }catch(Exception ex){report["error"]=ex.Message;}
  return report;
 }
 public static Dictionary<string,object> Visibility(string planPath){
  var report=D("status","FAILED","plan",planPath);IModelDoc2 model=null;bool wasOpen=false;string oldCfg="",source="",before="",created="";
  try {
   var p=Read(planPath);var f=Read(S(p,"facts"));Validate(p,f);source=S(f,"path");before=Hash(source);Require(before==S(f,"sha256"),"Source changed: inspect again");
   sw=(ISldWorks)Marshal.GetActiveObject("SldWorks.Application");model=Source(source,S(f,"configuration"),out wasOpen,out oldCfg);
   var sh=Map(p["sheet"]);drawing=(IModelDoc2)sw.NewDocument(S(p,"template"),12,0,0);Require(drawing!=null,"Create preview drawing failed");created=drawing.GetTitle();dr=(IDrawingDoc)drawing;
   var sheet=(ISheet)dr.GetCurrentSheet();Require(dr.SetupSheet5(sheet.GetName(),12,13,1,1,Convert.ToBoolean(sh["first_angle"]),"",N(sh,"width_mm")/1000,N(sh,"height_mm")/1000,"",true),"Preview sheet setup failed");
   int err=0;sw.ActivateDoc3(created,false,0,ref err);dr.EditSheet();views=new Dictionary<string,IView>();
   foreach(var o in Rows(p["views"])){var v=Map(o);views.Add(S(v,"id"),ModelView(v,source,S(f,"configuration")));}
   Require(drawing.ForceRebuild3(false),"Preview rebuild failed");
   var results=new List<object>();
   foreach(var kv in views){
    var circles=new List<double[]>();
    foreach(var co in A(kv.Value.GetVisibleComponents()))foreach(var eo in A(kv.Value.GetVisibleEntities2((Component2)co,1))){var e=eo as IEdge;if(e==null)continue;var c=(ICurve)e.GetCurve();if(c.IsCircle())circles.Add((double[])c.CircleParams);}
    var matches=new List<object>();
    foreach(var eo in Rows(f["edges"])){var e=Map(eo);if(S(e,"type")!="circle")continue;var target=Vec(e["parameters_si"]);int count=0;
     foreach(var cp in circles){bool same=true;foreach(int i in new[]{0,1,2,6})if(Math.Abs(cp[i]-target[i])>1e-8)same=false;double dot=cp[3]*target[3]+cp[4]*target[4]+cp[5]*target[5];if(Math.Abs(Math.Abs(dot)-1)>1e-7)same=false;if(same)count++;}
     if(count>0)matches.Add(D("edge_id",e["id"],"geometric_matches",count));
    }
    results.Add(D("view",kv.Key,"visible_circle_count",circles.Count,"source_circle_matches",matches));
   }
   report["views"]=results;report["source_hash_unchanged"]=Hash(source)==before;Require(Convert.ToBoolean(report["source_hash_unchanged"]),"Source changed during visibility preview");report["status"]="VISIBILITY_MEASURED";
  }catch(Exception ex){report["error"]=ex.Message;}
  finally{if(!String.IsNullOrEmpty(created)&&sw!=null)try{sw.CloseDoc(created);}catch(Exception ex){report["cleanup_error"]=ex.Message;}try{Restore(model,wasOpen,oldCfg);}catch(Exception ex){report["restore_error"]=ex.Message;report["status"]="FAILED";}ReleaseSessionReferences();}
  return report;
 }
 public static Dictionary<string,object> Execute(string planPath){
  var report=D("status","FAILED","plan",planPath,"visual_review","NOT_CHECKED","manufacturing_release","NOT_APPROVED");IModelDoc2 model=null;bool wasOpen=false;string oldCfg="",before="",source="";
  try{
   var p=Read(planPath);var f=Read(S(p,"facts"));Validate(p,f);source=S(f,"path");before=Hash(source);Require(before==S(f,"sha256"),"Source changed: inspect again");sw=(ISldWorks)Marshal.GetActiveObject("SldWorks.Application");model=Source(source,S(f,"configuration"),out wasOpen,out oldCfg);
   var sh=Map(p["sheet"]);drawing=(IModelDoc2)sw.NewDocument(S(p,"template"),12,0,0);Require(drawing!=null,"Create drawing failed");dr=(IDrawingDoc)drawing;report["created_document_title"]=drawing.GetTitle();var sheet=(ISheet)dr.GetCurrentSheet();Require(dr.SetupSheet5(sheet.GetName(),12,13,1,1,Convert.ToBoolean(sh["first_angle"]),"",N(sh,"width_mm")/1000,N(sh,"height_mm")/1000,"",true),"Sheet setup failed");int e=0,w=0;sw.ActivateDoc3(drawing.GetTitle(),false,0,ref e);dr.EditSheet();views=new Dictionary<string,IView>();
   foreach(var o in Rows(p["views"])){var v=Map(o);views.Add(S(v,"id"),ModelView(v,source,S(f,"configuration")));}
   foreach(var o in Rows(p["sections"])){var s=Map(o);views.Add(S(s,"id"),Section(s));}
   var checks=new List<object>();foreach(var o in Rows(p["dimensions"]))checks.Add(Dimension(Map(o)));report["dimensions"]=checks;
   var edges=Edges(f);foreach(var o in OptionalRows(p,"diameters"))checks.Add(Diameter(Map(o),edges,model));foreach(var o in Rows(p["labels"]))Label(Map(o),edges,model);
   foreach(var o in Rows(p["tables"]))Table(Map(o));dr.ActivateView("");foreach(var o in Rows(p["notes"])){var n=Map(o);Note(S(n,"text"),Vec(n["position_mm"]),N(n,"font_mm"));}drawing.ClearSelection2(true);Require(drawing.ForceRebuild3(false),"Drawing rebuild failed");
   var boxes=new List<object>();var flags=new List<object>();var extents=new Dictionary<string,double[]>();
   foreach(var kv in views){var box=(double[])kv.Value.GetOutline();var mm=new double[4];for(int i=0;i<4;i++)mm[i]=box[i]*1000;extents.Add(kv.Key,mm);boxes.Add(D("view",kv.Key,"outline_mm",mm));if(mm[0]<0||mm[1]<0||mm[2]>N(sh,"width_mm")||mm[3]>N(sh,"height_mm"))flags.Add("Out-of-sheet view: "+kv.Key);}
   var names=new List<string>(extents.Keys);for(int i=0;i<names.Count;i++)for(int j=i+1;j<names.Count;j++){var a=extents[names[i]];var b=extents[names[j]];if(Math.Min(a[2],b[2])>Math.Max(a[0],b[0])&&Math.Min(a[3],b[3])>Math.Max(a[1],b[1]))flags.Add("Possible view overlap: "+names[i]+" / "+names[j]);}
   report["view_bounds"]=boxes;report["layout_flags"]=flags;report["unresolved"]=p["unresolved"];
   string output=S(p,"output_drawing");Directory.CreateDirectory(Path.GetDirectoryName(output));bool saved=drawing.Extension.SaveAs(output,0,1,null,ref e,ref w);Require(saved&&e==0&&File.Exists(output),"Native drawing save failed: "+e);report["drawing"]=D("path",output,"status","PASS","warnings",w);
   report["pdf"]=D("status","NOT_REQUESTED");if(p.ContainsKey("output_pdf")){string pdf=S(p,"output_pdf");Directory.CreateDirectory(Path.GetDirectoryName(pdf));e=0;w=0;bool ok=drawing.Extension.SaveAs(pdf,0,1,null,ref e,ref w);report["pdf"]=D("path",pdf,"api_success",ok,"error",e,"warning",w,"file_exists",File.Exists(pdf),"status",ok&&e==0&&File.Exists(pdf)?"EXPORTED_REVIEW_REQUIRED":"FAILED");}
   ReopenCheck(p,report);Require(Hash(source)==before,"Source hash changed");report["source_integrity"]="PASS";report["status"]=flags.Count==0?"NATIVE_VERIFIED_REVIEW_REQUIRED":"NATIVE_VERIFIED_LAYOUT_REVIEW_REQUIRED";
  }catch(Exception ex){report["error"]=ex.Message;}
  finally{try{Restore(model,wasOpen,oldCfg);}catch(Exception ex){report["restore_error"]=ex.Message;report["status"]="FAILED";}if(!string.IsNullOrEmpty(before)&&File.Exists(source))report["source_hash_unchanged"]=Hash(source)==before;}
  return report;
 }
}
