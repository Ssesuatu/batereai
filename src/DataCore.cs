using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace T3Usage {
public sealed class DataCore:IDisposable {
 readonly object gate=new object();volatile Snapshot current=new Snapshot(new Provider[0],null);string signature="";volatile bool disposed;volatile string threadId="";
 readonly List<FileSystemWatcher> watchers=new List<FileSystemWatcher>();readonly BoundedRefresh fullTimer,activeTimer;
 public readonly string Home; public event EventHandler SnapshotChanged;
 public Snapshot Current{get{return current;}}
 public double LastModelsMs;
 public DataCore(){Home=Environment.GetEnvironmentVariable("T3CODE_HOME");if(string.IsNullOrWhiteSpace(Home))Home=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".t3");fullTimer=new BoundedRefresh(500,()=>Reload(false));activeTimer=new BoundedRefresh(900,()=>Reload(true));}
 internal static JsonObj Read(string p){using(var f=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var s=new StreamReader(f))return SimpleJson.ParseObject(s.ReadToEnd().TrimStart('\uFEFF'));}
 internal static DateTime? Date(string s){DateTime d;return DateTime.TryParse(s,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out d)?(DateTime?)d:null;}
 internal static string Serialize(object o){return Json.Encode(o);}
  static object Normalize(object o){var j=o as JsonObj;if(j!=null)return j.Map.ToDictionary(k=>k.Key,k=>Normalize(k.Value));var a=o as System.Collections.Generic.List<object>;if(a!=null)return a.Select(Normalize).ToArray();return o;}
 static IEnumerable<JsonObj> Objects(JsonObj o,string key){return (o==null?new List<object>():o.GetArray(key)??new List<object>()).OfType<JsonObj>();}
 public void Start(){Watch(Path.Combine(Home,"caches"),"*.json",false);Watch(Path.Combine(Home,"userdata"),"*",true);Refresh();}
 void Watch(string path,string filter,bool user){if(!Directory.Exists(path))return;var w=new FileSystemWatcher(path,filter);w.NotifyFilter=NotifyFilters.LastWrite|NotifyFilters.FileName|NotifyFilters.Size;FileSystemEventHandler h=(s,e)=>Schedule(e.Name,user);w.Changed+=h;w.Created+=h;w.Deleted+=h;w.Renamed+=(s,e)=>{Schedule(e.Name,user);Schedule(e.OldName,user);};w.Error+=(s,e)=>{Log.Error("Watcher",e.GetException());Refresh();};w.EnableRaisingEvents=true;watchers.Add(w);}
 void Schedule(string name,bool user){if(disposed)return;try{if(!user||name=="settings.json")fullTimer.Signal();else if(name=="statev2.sqlite-wal"||name=="statev2.sqlite")activeTimer.Signal();}catch(ObjectDisposedException){}}
 public void Refresh(){Refresh(null);}
 public void Refresh(Action completed){if(!disposed)ThreadPool.QueueUserWorkItem(o=>{try{Reload(false);}finally{if(completed!=null)completed();}});}
 public void SetThreadId(string id){if(threadId==id)return;threadId=id;if(!disposed)activeTimer.Signal();}
 public void RefreshActive(){if(!disposed)activeTimer.Signal();}
 public void SetProviderEnabled(string id,bool enabled){lock(gate){string path=Path.Combine(Home,"userdata","settings.json");var stamp=File.GetLastWriteTimeUtc(path);var settings=Read(path);var instances=settings.GetObj("providerInstances")??new JsonObj();var cfg=instances.GetObj(id)??new JsonObj();cfg.Set("enabled",enabled);instances.Set(id,cfg);settings.Set("providerInstances",instances);string temp=path+".t3usage.tmp";File.WriteAllText(temp,Serialize(Normalize(settings)));if(File.GetLastWriteTimeUtc(path)!=stamp){File.Delete(temp);throw new IOException("T3 settings changed. Try again.");}File.Replace(temp,path,path+".t3usage.bak");}Refresh();}
 public void SetSelectedModel(string provider,string slug){lock(gate){var a=current.Active;var p=current.Providers.FirstOrDefault(x=>x.Id==provider);var m=p==null?null:p.Models.FirstOrDefault(x=>x.Slug==slug);if(a==null||p==null||m==null)return;current=new Snapshot(current.Providers,new Active(a.ThreadId,a.Title,provider,slug,m.Name,a.Options,a.RuntimeMode,"picker"),current.LastModels);}var h=SnapshotChanged;if(h!=null)h(this,EventArgs.Empty);RefreshActive();}
 public Snapshot LoadOnce(){lock(gate){return Load();}}
 void Reload(bool activeOnly){if(disposed)return;try{lock(gate){if(disposed)return;Snapshot next;if(activeOnly){var act=LoadActive(current.Providers);next=new Snapshot(current.Providers,act,SameActive(act,current.Active)?(IDictionary<string,string>)current.LastModels:LoadLastModels(),LoadCache(act));}else next=Load();string sig=Serialize(new {next.Providers,next.Active,next.LastModels,Cache=next.Cache==null?"":next.Cache.Key()});if(sig==signature)return;signature=sig;current=next;}var h=SnapshotChanged;if(h!=null)h(this,EventArgs.Empty);}catch(Exception e){Log.Error("Reload",e);if(!disposed)try{fullTimer.Signal();}catch(ObjectDisposedException){}}}
 readonly Dictionary<string,Tuple<long,long,JsonObj>> jsonCache=new Dictionary<string,Tuple<long,long,JsonObj>>();
 JsonObj ReadCached(string path){var f=new FileInfo(path);Tuple<long,long,JsonObj> old;if(jsonCache.TryGetValue(path,out old)&&old.Item1==f.LastWriteTimeUtc.Ticks&&old.Item2==f.Length)return old.Item3;var value=Read(path);jsonCache[path]=Tuple.Create(f.LastWriteTimeUtc.Ticks,f.Length,value);return value;}
 Snapshot Load(){
 var settings=ReadCached(Path.Combine(Home,"userdata","settings.json"));var instances=settings.GetObj("providerInstances")??new JsonObj();var prefs=settings.GetObj("providerModelPreferences")??new JsonObj();
 var order=new List<string>{"codex","claudeAgent"};order.AddRange(instances.OrderedKeys.Where(k=>!order.Contains(k)));var providers=new List<Provider>();
 foreach(var file in Directory.GetFiles(Path.Combine(Home,"caches"),"*.json")){
 try{var raw=ReadCached(file);var d=raw.GetObj("data")??raw;string id=d.GetString("instanceId")??Path.GetFileNameWithoutExtension(file),driver=d.GetString("driver")??id;
 var cfg=instances.GetObj(id);if(!order.Contains(id))continue;
 var ul=raw.GetObj("usageLimits")??d.GetObj("usageLimits");DateTime? stamp=Date(ul==null?null:ul.GetString("checkedAt"))??Date(d.GetString("checkedAt"));
 var wins=Objects(ul,"windows").Select(w=>new UsageWindow(w.GetString("id")??"",w.GetString("label")??w.GetString("id"),w.GetString("kind")??"other",w.GetDouble("usedPercent")??0,Date(w.GetString("resetsAt")),w.GetString("detail"),w.GetBool("dateOnly")==true,w.GetBool("monthly")==true)).ToList();
 if(id=="kiro"){var k=Kiro.Load();if(k!=null){var kt=Date(k.GetString("checkedAt"));if(kt.HasValue&&(wins.Count==0||!stamp.HasValue||kt>stamp)){stamp=kt;wins=new List<UsageWindow>{new UsageWindow("credits","Credits","monthly",k.GetDouble("percent")??0,Date(k.GetString("resetsAt")),k.GetString("detail"),true,true)};}}}
 var hidden=new HashSet<string>(((prefs.GetObj(id)??new JsonObj()).GetArray("hiddenModels")??new List<object>()).OfType<string>(),StringComparer.OrdinalIgnoreCase);
 var models=Objects(d,"models").Where(m=>!hidden.Contains(m.GetString("slug")??"")).Select(m=>new UsageModel(m.GetString("slug"),m.GetString("name")??m.GetString("slug"),Serialize(m.GetObj("capabilities")))).ToList();
 string label=(cfg==null?null:cfg.GetString("displayName"))??d.GetString("displayName")??(driver=="claudeAgent"?"Claude":id=="codex"?"Codex":id);
 string[] ds={"codex","claudeAgent","antigravity","kiro","cursor","opencode","pi","muse"},colors={"#F5F5F5","#D97757","#8C7BD1","#9046FF","#8B8B8B","#5B9BBD","#4D9F8A","#0866FF"};int ix=Array.IndexOf(ds,driver);
 var provider=new Provider(id,driver,label,ix<0?"#F5F5F5":colors[ix],order.IndexOf(id),stamp,models,wins);provider.Enabled=cfg!=null&&cfg.GetBool("enabled").HasValue?cfg.GetBool("enabled").Value:d.GetBool("enabled")==true;providers.Add(provider);
 }catch(Exception e){Log.Error("Cache "+Path.GetFileName(file),e);}}
 providers=providers.OrderBy(p=>p.Order).ThenBy(p=>p.Id).ToList();var active=LoadActive(providers);return new Snapshot(providers,active,LoadLastModels(),LoadCache(active));
 }
 static string Q(string s){return "'"+(s??"").Replace("'","''")+"'";}
 static long? Long(string s){var d=CacheMath.Number(s);return d.HasValue?(long?)Math.Round(d.Value):null;}
 // The active reload runs every second; re-read the turns only when the selection or the database changed.
 string cacheKey="";CacheState cacheValue;public double LastCacheMs;
 CacheState LoadCache(Active a){
  if(a==null||string.IsNullOrEmpty(a.ThreadId)||string.IsNullOrEmpty(a.ProviderId)||string.IsNullOrEmpty(a.ModelSlug))return null;
  string db=Path.Combine(Home,"userdata","statev2.sqlite");
  var f=new FileInfo(db);var w=new FileInfo(db+"-wal");
  string key=a.ThreadId+"|"+a.ProviderId+"|"+a.ModelSlug+"|"+(f.Exists?f.LastWriteTimeUtc.Ticks+":"+f.Length:"")+"|"+(w.Exists?w.LastWriteTimeUtc.Ticks+":"+w.Length:"");
  if(key==cacheKey)return cacheValue;
  var sw=System.Diagnostics.Stopwatch.StartNew();CacheState result=null;
  try{
   string th=Q(a.ThreadId);
   // Turns from runs on this instance and model only; t.thread_id keeps the scan on the thread index.
   var rows=Sqlite.QueryRows(db,"SELECT t.status,json_extract(t.payload_json,'$.tokenUsage.updatedAt'),(json_extract(t.payload_json,'$.tokenUsage.cachedInputTokens') IS NOT NULL OR json_extract(t.payload_json,'$.tokenUsage.promptCacheExpiresAt') IS NOT NULL OR json_extract(t.payload_json,'$.turnTokenUsage.cachedInputTokens') IS NOT NULL OR json_extract(t.payload_json,'$.turnTokenUsage.cacheCreationTokens') IS NOT NULL),json_extract(t.payload_json,'$.tokenUsage.promptCacheExpiresAt'),json_extract(t.payload_json,'$.tokenUsage.promptCacheTtlMs'),t.completed_at FROM orchestration_v2_projection_provider_turns t JOIN orchestration_v2_projection_run_attempts a ON a.attempt_id=t.run_attempt_id JOIN orchestration_v2_projection_runs r ON r.run_id=a.run_id WHERE t.thread_id="+th+" AND r.provider_instance_id="+Q(a.ProviderId)+" AND json_extract(r.payload_json,'$.modelSelection.model')="+Q(a.ModelSlug)+" ORDER BY t.completed_at IS NULL DESC,t.completed_at DESC LIMIT 200",6,200);
   // Context is the current conversation's; a send to another instance starts a new one, so it has none yet.
   long? used=null,max=null;
   var pt=Sqlite.QuerySingleRow(db,"SELECT p.provider_thread_id,p.provider_instance_id,json_extract(p.payload_json,'$.contextUsage.usedTokens'),json_extract(p.payload_json,'$.contextUsage.maxTokens') FROM orchestration_v2_projection_threads th JOIN orchestration_v2_projection_provider_threads p ON p.provider_thread_id=th.active_provider_thread_id WHERE th.thread_id="+th+" LIMIT 1",4);
   if(pt!=null&&pt[1]==a.ProviderId){
    var u=Sqlite.QuerySingleRow(db,"SELECT json_extract(payload_json,'$.tokenUsage.usedTokens'),json_extract(payload_json,'$.tokenUsage.maxTokens') FROM orchestration_v2_projection_provider_turns WHERE thread_id="+th+" AND provider_thread_id="+Q(pt[0])+" AND json_extract(payload_json,'$.tokenUsage') IS NOT NULL ORDER BY julianday(json_extract(payload_json,'$.tokenUsage.updatedAt')) DESC LIMIT 1",2);
    if(u!=null&&u[0]!=""){used=Long(u[0]);max=Long(u[1]);}else if(pt[2]!=""){used=Long(pt[2]);max=Long(pt[3]);}
   }
   result=CacheMath.Warmth(rows,used,max);
  }catch(Exception e){Log.Error("Cache query",e);}
  LastCacheMs=sw.Elapsed.TotalMilliseconds;if(LastCacheMs>100)Log.Info("Cache query "+LastCacheMs.ToString("0",CultureInfo.InvariantCulture)+" ms");
  cacheKey=key;cacheValue=result;return result;
 }
 static bool SameActive(Active a,Active b){if(a==null||b==null)return a==b;return a.ThreadId==b.ThreadId&&a.ProviderId==b.ProviderId&&a.ModelSlug==b.ModelSlug;}
 // Model of each provider instance's most recently visited eligible thread (same eligibility as LoadActive).
 // Re-read on full reloads and when the active thread or model changes, not on every WAL write.
 Dictionary<string,string> LoadLastModels(){
  var map=new Dictionary<string,string>();var sw=System.Diagnostics.Stopwatch.StartNew();
  try{
   string db=Path.Combine(Home,"userdata","statev2.sqlite");
   var rows=Sqlite.QueryRows(db,"SELECT coalesce(json_extract(payload_json,'$.modelSelection.instanceId'),provider_instance_id) AS inst,json_extract(payload_json,'$.modelSelection.model'),max(coalesce(json_extract(payload_json,'$.lastVisitedAt'),json_extract(payload_json,'$.updatedAt'),updated_at)) FROM orchestration_v2_projection_threads WHERE deleted_at IS NULL AND archived_at IS NULL AND thread_id NOT LIKE 'thread:delegated-task:%' AND coalesce(json_extract(payload_json,'$.lineage.relationshipToParent'),'')!='subagent' GROUP BY inst ORDER BY inst",3,500);
   foreach(var r in rows)if(!string.IsNullOrEmpty(r[0])&&!string.IsNullOrEmpty(r[1])&&!map.ContainsKey(r[0]))map[r[0]]=r[1];
  }catch(Exception e){Log.Error("Last models",e);}
  LastModelsMs=sw.Elapsed.TotalMilliseconds;if(LastModelsMs>100)Log.Info("Last models query "+LastModelsMs.ToString("0",CultureInfo.InvariantCulture)+" ms");
  return map;
 }
 Active LoadActive(IEnumerable<Provider> providers){
 string db=Path.Combine(Home,"userdata","statev2.sqlite");var rows=new List<ThreadRow>();string source="lastVisitedAt";
 // Explicit URL id bypasses ranking while retaining archived/deleted/subagent filtering.
 if(!string.IsNullOrEmpty(threadId)){var a=Sqlite.QuerySingleRow(db,"SELECT thread_id,title,provider_instance_id,updated_at,payload_json FROM orchestration_v2_projection_threads WHERE deleted_at IS NULL AND archived_at IS NULL AND thread_id NOT LIKE 'thread:delegated-task:%' AND coalesce(json_extract(payload_json, '$.lineage.relationshipToParent'), '') != 'subagent' AND thread_id='"+threadId.Replace("'","''")+"' LIMIT 1",5);if(a!=null){rows=new List<ThreadRow>{new ThreadRow{ThreadId=a[0],Title=a[1],ProviderInstanceId=a[2],UpdatedAt=a[3],PayloadJson=a[4]}};source="url";}}
 // The visible T3 window's URL identifies its thread. Another window may have a
 // newer lastVisitedAt, but that must not replace the model shown in this picker.
 if(rows.Count==0){rows=Sqlite.QueryRecentThreads(db,1);source="lastVisitedAt";}
 foreach(var r in rows){var payload=SimpleJson.ParseObject(r.PayloadJson)??new JsonObj();if(r.ThreadId.StartsWith("thread:delegated-task:")||(payload.GetObj("lineage")??new JsonObj()).GetString("relationshipToParent")=="subagent")continue;var sel=payload.GetObj("modelSelection")??new JsonObj();string id=sel.GetString("instanceId")??r.ProviderInstanceId,slug=sel.GetString("model");var p=providers.FirstOrDefault(x=>x.Id==id);var m=p==null?null:p.Models.FirstOrDefault(x=>x.Slug==slug);return new Active(r.ThreadId,r.Title,id,slug,m==null?slug:m.Name,Serialize(sel.GetArray("options")??new List<object>()),payload.GetString("runtimeMode"),source);}
 return null;
 }
 public void Dispose(){disposed=true;fullTimer.Dispose();activeTimer.Dispose();foreach(var w in watchers)w.Dispose();}
}}





