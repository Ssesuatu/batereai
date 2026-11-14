using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
namespace T3Usage {
public sealed class UsageWindow {
 public readonly string Id,Label,Kind,Detail; public readonly double UsedPercent; public readonly DateTime? ResetsAtUtc; public readonly bool DateOnly,Monthly;
 public UsageWindow(string id,string label,string kind,double used,DateTime? reset,string detail,bool dateOnly,bool monthly){Id=id;Label=label;Kind=kind;UsedPercent=used;ResetsAtUtc=reset;Detail=detail;DateOnly=dateOnly;Monthly=monthly;}
}
public sealed class UsageModel {
 public readonly string Slug,Name,DescriptorsJson;
 public UsageModel(string slug,string name,string descriptors){Slug=slug;Name=name;DescriptorsJson=descriptors;}
}
public sealed class Provider {
 public readonly string Id,Driver,Label,Badge,Color;public readonly int Order;public readonly DateTime? CheckedAtUtc; public readonly bool IsStale;public bool Enabled=true;
 public readonly ReadOnlyCollection<UsageModel> Models;public readonly ReadOnlyCollection<UsageWindow> Windows;
 public Provider(string id,string driver,string label,string color,int order,DateTime? time,IEnumerable<UsageModel> models,IEnumerable<UsageWindow> windows){Id=id;Driver=driver;Label=label;Badge=driver=="antigravity"?label.Substring(0,Math.Min(2,label.Length)).ToUpperInvariant():"";Color=color;Order=order;CheckedAtUtc=time;IsStale=!time.HasValue||DateTime.UtcNow-time.Value>TimeSpan.FromMinutes(30);Models=new List<UsageModel>(models).AsReadOnly();Windows=new List<UsageWindow>(windows).AsReadOnly();}
}
public sealed class Active {
 public readonly string ThreadId,Title,ProviderId,ModelSlug,ModelName,Options,RuntimeMode,Source;
 public Active(string thread,string title,string provider,string slug,string name,string options,string runtime,string source){ThreadId=thread;Title=title;ProviderId=provider;ModelSlug=slug;ModelName=name;Options=options;RuntimeMode=runtime;Source=source;}
}
public sealed class Snapshot {
 public readonly DateTime LoadedAtUtc;public readonly ReadOnlyCollection<Provider> Providers;public readonly Active Active;
 // Provider instance id -> model slug of its most recently visited eligible thread (the card's "last model").
 public readonly ReadOnlyDictionary<string,string> LastModels;
 // Prompt cache and context window of the active thread's selected model; null until read.
 public readonly CacheState Cache;
 public Snapshot(IEnumerable<Provider> providers,Active active):this(providers,active,null,null){}
 public Snapshot(IEnumerable<Provider> providers,Active active,IDictionary<string,string> lastModels):this(providers,active,lastModels,null){}
 public Snapshot(IEnumerable<Provider> providers,Active active,IDictionary<string,string> lastModels,CacheState cache){LoadedAtUtc=DateTime.UtcNow;Providers=new List<Provider>(providers).AsReadOnly();Active=active;LastModels=new ReadOnlyDictionary<string,string>(lastModels==null?new Dictionary<string,string>():new Dictionary<string,string>(lastModels));Cache=cache;}
}
// Facts read from T3's provider turns. ExpiresAtUtc is null when no turn on this model reported cache usage.
public sealed class CacheState {
 public readonly bool Running,Estimated;public readonly DateTime? ExpiresAtUtc;public readonly double? TtlMs;public readonly long? ContextUsed,ContextMax;
 public CacheState(bool running,DateTime? expires,double? ttlMs,bool estimated,long? used,long? max){Running=running;ExpiresAtUtc=expires;TtlMs=ttlMs;Estimated=estimated;ContextUsed=used;ContextMax=max;}
 public string Key(){return Running+"|"+(ExpiresAtUtc.HasValue?ExpiresAtUtc.Value.Ticks.ToString():"")+"|"+TtlMs+"|"+Estimated+"|"+ContextUsed+"|"+ContextMax;}
}
// Mirrors T3 Code's derivePromptCacheWarmth: a cache belongs to one model, so only that model's turns count.
// Claude reports when its cache expires; providers that only report cached token counts get an assumed lifetime.
public static class CacheMath {
 // Anthropic's default lifetime and the low end of OpenAI's 5-10 minute idle window.
 public const double EstimatedTtlMs=5*60000;
 // Row: status, tokenUsage.updatedAt, reportsCache ("1"/"0"), promptCacheExpiresAt, promptCacheTtlMs, completedAt.
 public static CacheState Warmth(IEnumerable<string[]> rows,long? used,long? max){
  bool running=false;DateTime? latest=null;string[] latestRow=null;DateTime? ttlAt=null;double? ttl=null;
  foreach(var r in rows){
   if(r[0]=="running")running=true;
   if(r[2]!="1")continue;
   DateTime? at=DataCore.Date(r[1]);if(!at.HasValue)at=DataCore.Date(r[5]);if(!at.HasValue)continue;
   if(!latest.HasValue||at.Value>latest.Value){latest=at;latestRow=r;}
   double? t=Number(r[4]);if(t.HasValue&&(!ttlAt.HasValue||at.Value>ttlAt.Value)){ttl=t;ttlAt=at;}
  }
  // A streaming turn only means a warm cache once this model has reported cache usage.
  if(latestRow==null)return new CacheState(false,null,null,false,used,max);
  DateTime? exp=DataCore.Date(latestRow[3]);
  if(exp.HasValue)return new CacheState(running,exp,Number(latestRow[4])??ttl,false,used,max);
  // A request served fully from cache reports no lifetime, though it still refreshes the cache.
  double life=ttl??EstimatedTtlMs;return new CacheState(running,latest.Value.AddMilliseconds(life),life,true,used,max);
 }
 internal static double? Number(string s){double d;return !string.IsNullOrEmpty(s)&&double.TryParse(s,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out d)?(double?)d:null;}
}
public sealed class UsageResult {
 public readonly UsageWindow Window; public readonly int? Left; public bool AwaitingRefresh{get{return !Left.HasValue;}}
 public UsageResult(UsageWindow w,DateTime now){Window=w;Left=UsageMath.LeftPercent(w,now);}
}
public static class UsageMath {
 public static int? LeftPercent(UsageWindow w,DateTime now){if(w.ResetsAtUtc.HasValue&&w.ResetsAtUtc.Value<=now)return null;return (int)Math.Round(Math.Max(0,Math.Min(100,100-w.UsedPercent)),MidpointRounding.AwayFromZero);}
 public static IEnumerable<UsageWindow> ModelWindows(Provider p,string slug){if(p.Driver!="antigravity"||string.IsNullOrEmpty(slug))return p.Windows;string fam=Regex.Match(slug,@"^(gemini|claude|gpt)",RegexOptions.IgnoreCase).Value.ToLowerInvariant();return p.Windows.Where(w=>{string id=w.Id.ToLowerInvariant();if(id.Contains("argon")&&!slug.ToLowerInvariant().Contains("argon"))return false;string f=Regex.Match(id,@"^(gemini|claude|gpt|3p)").Value;return f==""||fam==""||f==fam||(f=="3p"&&(fam=="claude"||fam=="gpt"));});}
 public static UsageResult ModelUsage(Provider p,string slug,DateTime now){var wins=ModelWindows(p,slug).ToList();if(wins.Count==0)return null;var w=wins.OrderBy(x=>LeftPercent(x,now)??-1).ThenBy(x=>x.ResetsAtUtc??DateTime.MaxValue).ThenBy(x=>x.Id,StringComparer.Ordinal).First();return new UsageResult(w,now);}
 // Card's model: the active model on the provider in use, else the provider's last used model, else null.
 public static string CardModel(Snapshot s,Provider p,Active active){if(active!=null&&active.ProviderId==p.Id&&!string.IsNullOrEmpty(active.ModelSlug))return active.ModelSlug;string slug;return s!=null&&s.LastModels.TryGetValue(p.Id,out slug)&&!string.IsNullOrEmpty(slug)?slug:null;}
 // Most restrictive window of the card's model; without a known model, the provider headline.
 public static UsageResult CardHeadline(Provider p,string slug,Active active,DateTime now){return slug!=null?ModelUsage(p,slug,now):ProviderHeadline(p,active,now);}
 public static UsageResult ProviderHeadline(Provider p,Active active,DateTime now){if(active!=null&&active.ProviderId==p.Id)return ModelUsage(p,active.ModelSlug,now);return p.Models.Select(m=>ModelUsage(p,m.Slug,now)).Where(u=>u!=null).OrderByDescending(u=>u.Left??-1).FirstOrDefault()??ModelUsage(p,null,now);}
}}
