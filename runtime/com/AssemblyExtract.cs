using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
public static class AssemblyExtract {
 static List<object> components = new List<object>();
 static List<object> mates = new List<object>();
 static List<string> warnings = new List<string>();
 static HashSet<string> seen = new HashSet<string>();
 static Dictionary<string,object> D(params object[] a) { var d=new Dictionary<string,object>(); for(int i=0;i<a.Length;i+=2)d[(string)a[i]]=a[i+1]; return d; }
 static object Safe(Func<object> f,string context) { try{return f();}catch(Exception e){warnings.Add(context+": "+e.Message);return null;} }
 static object[] Arr(object o){return o as object[] ?? new object[0];}
 static void Features(IFeature first,string owner,IModelDoc2 document,bool sub) {
   var f=first; int guard=0;
   while(f!=null && guard++<10000) {
     string name=f.Name, type=f.GetTypeName2();
     string key=owner+"|"+name+"|"+type;
     if(seen.Add(key)) {
       if(type.StartsWith("Mate") && type!="MateGroup") {
         var specific=Safe(()=>f.GetSpecificFeature2(),key+" specific");
         var m=specific as IMate2;
         if(m!=null) {
           var entities=new List<object>();
           int count=m.GetMateEntityCount();
           for(int j=0;j<count;j++) {
             int index=j;
             var e=(IMateEntity2)m.MateEntity(j);
             var c=(IComponent2)e.ReferenceComponent;
             object persistent=Safe(()=>{var bytes=document.Extension.GetPersistReference3(e.Reference) as byte[];return bytes==null?null:Convert.ToBase64String(bytes);},key+" reference "+index);
             entities.Add(D("index",j,"component",c==null?null:c.Name2,"component_path",c==null?null:c.GetPathName(),"reference_type",e.ReferenceType2,"entity_parameters",Safe(()=>e.EntityParams,key+" params"),"original_document_persistent_reference",persistent));
           }
           object dimension=Safe(()=> {var dd=(IDisplayDimension)m.DisplayDimension;if(dd==null)return null;return ((IDimension)dd.GetDimension2(0)).SystemValue;},key+" dimension");
           mates.Add(D("owner",owner,"name",name,"feature_type",type,"mate_type",m.Type,"mate_type_name",Enum.GetName(typeof(swMateType_e),m.Type),"alignment",m.Alignment,"suppressed",f.IsSuppressed(),"error_code",f.GetErrorCode(),"dimension_si",dimension,"entities",entities));
         } else warnings.Add(key+": feature has no IMate2 interface");
       }
       var child=(IFeature)f.GetFirstSubFeature();
       if(child!=null) Features(child,owner,document,true);
     }
     f=(IFeature)(sub?f.GetNextSubFeature():f.GetNextFeature());
   }
 }
 public static object Run(string expectedPath) {
   var sw=(ISldWorks)Marshal.GetActiveObject("SldWorks.Application");
   var doc=(IModelDoc2)sw.ActiveDoc;
   if(doc==null || doc.GetType()!=2)throw new Exception("Active document must be an assembly");
   if(!String.Equals(Path.GetFullPath(doc.GetPathName()),Path.GetFullPath(expectedPath),StringComparison.OrdinalIgnoreCase))throw new Exception("Unexpected active assembly: "+doc.GetTitle());
   var ass=(IAssemblyDoc)doc;
   var cfg=(IConfiguration)doc.ConfigurationManager.ActiveConfiguration;
   foreach(object obj in Arr(ass.GetComponents(false))) {
     var c=(IComponent2)obj;
     string name=c.Name2,path=c.GetPathName();
     var parent=(IComponent2)c.GetParent();
     var model=(IModelDoc2)c.GetModelDoc2();
     components.Add(D("instance",name,"parent",parent==null?"$root":parent.Name2,"path",path,"file_exists",File.Exists(path),"configuration",c.ReferencedConfiguration,"suppression_state",c.GetSuppression(),"suppression_name",Enum.GetName(typeof(swComponentSuppressionState_e),c.GetSuppression()),"hidden",c.IsHidden(false),"fixed",c.IsFixed(),"model_loaded",model!=null,"transform2_raw",Safe(()=>((IMathTransform)c.Transform2).ArrayData,name+" transform"),"children",Safe(()=>{var names=new List<string>();foreach(object child in Arr(c.GetChildren()))names.Add(((IComponent2)child).Name2);return names;},name+" children")));
     if(path.EndsWith(".sldasm",StringComparison.OrdinalIgnoreCase)) {
       if(model!=null)Features((IFeature)c.FirstFeature(),name,model,false);
       else warnings.Add(name+": subassembly model unavailable; internal mates not extracted");
     }
   }
   Features((IFeature)doc.FirstFeature(),"$root",doc,false);
   return D("source",doc.GetPathName(),"title",doc.GetTitle(),"configuration",cfg.Name,"revision",sw.RevisionNumber(),"exported_at",DateTime.Now.ToString("o"),"api_component_count",ass.GetComponentCount(false),"lightweight_count",ass.GetLightWeightComponentCount(),"has_unloaded_components",ass.HasUnloadedComponents(),"components",components,"mates",mates,"warnings",warnings);
 }
}
