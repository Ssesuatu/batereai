using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using System.Text.RegularExpressions;
namespace T3Usage {
public static class Kiro {
 static int running;static DateTime attempt=DateTime.MinValue;
 static string PathName{get{return Path.Combine(Log.DirectoryPath,"kiro.json");}}
 internal static JsonObj Load(){try{return File.Exists(PathName)?DataCore.Read(PathName):null;}catch(Exception e){Log.Error("Kiro cache",e);return null;}}
 // T3's own kiro cache carries no usage windows, so credits come only from kiro-cli.
 // Refresh in the background every 15 minutes so the pill never falls back to "no quota" or "stale".
 static Timer background;
 public static void StartBackground(Action complete){
  if(background!=null)return;
  background=new Timer(_=>{try{Task.Run(()=>RefreshAsync(complete));}catch(Exception e){Log.Error("Kiro background",e);}},null,TimeSpan.FromSeconds(20),TimeSpan.FromMinutes(5));
 }
 // Used share from the exact credit numbers (532.9 of 693 -> 76.9), like Kiro's own page; CLI's rounded % as fallback.
 static double Percent(Match m){var inv=CultureInfo.InvariantCulture;double u,t;
  if(double.TryParse(m.Groups[1].Value.Replace(",",""),NumberStyles.Number,inv,out u)&&double.TryParse(m.Groups[2].Value.Replace(",",""),NumberStyles.Number,inv,out t)&&t>0)return Math.Round(u*100/t,2);
  return double.Parse(m.Groups[3].Value,inv);}
 public static async Task RefreshAsync(Action complete){
 if(Interlocked.CompareExchange(ref running,1,0)!=0)return;
 try{var cached=Load();DateTime? stamp=cached==null?null:DataCore.Date(cached.GetString("checkedAt"));if(DateTime.UtcNow-attempt<TimeSpan.FromMinutes(2)||(stamp.HasValue&&DateTime.UtcNow-stamp.Value<TimeSpan.FromMinutes(3)))return;attempt=DateTime.UtcNow;
 string exe=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Kiro-Cli","kiro-cli.exe");if(!File.Exists(exe))return;
 var si=new ProcessStartInfo(exe,"chat --no-interactive /usage"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Log.DirectoryPath};Directory.CreateDirectory(Log.DirectoryPath);
 using(var p=Process.Start(si)){var stdout=p.StandardOutput.ReadToEndAsync();var stderr=p.StandardError.ReadToEndAsync();var exited=Task.Run(()=>p.WaitForExit(60000));
 if(await Task.WhenAny(exited,Task.Delay(60000)).ConfigureAwait(false)!=exited||!await exited.ConfigureAwait(false)){using(var kill=Process.Start(new ProcessStartInfo("taskkill.exe","/PID "+p.Id+" /T /F"){UseShellExecute=false,CreateNoWindow=true})){await Task.Run(()=>kill.WaitForExit(5000)).ConfigureAwait(false);}throw new TimeoutException("kiro-cli took longer than 60 seconds");}
 var reads=Task.WhenAll(stdout,stderr);if(await Task.WhenAny(reads,Task.Delay(2000)).ConfigureAwait(false)!=reads)throw new TimeoutException("Kiro output did not close");await reads.ConfigureAwait(false);
 var m=Regex.Match(stdout.Result,@"Credits \(([\d.,]+) of ([\d.,]+) covered in plan\),\s*([\d.]+)%");if(!m.Success)throw new InvalidDataException("Unrecognized Kiro usage: "+stderr.Result);
 var r=Regex.Match(stdout.Result,@"resets on (\d{4}-\d{2}-\d{2})");DateTime d;string reset=r.Success&&DateTime.TryParseExact(r.Groups[1].Value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out d)?d.ToUniversalTime().ToString("o"):null;
 string json=DataCore.Serialize(new {checkedAt=DateTime.UtcNow.ToString("o"),percent=Percent(m),detail=m.Groups[1].Value+"/"+m.Groups[2].Value,resetsAt=reset});
 string tmp=PathName+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllText(tmp,json);for(int i=0;;i++){try{if(File.Exists(PathName))File.Replace(tmp,PathName,null);else File.Move(tmp,PathName);break;}catch(IOException){if(i>=4)throw;}await Task.Delay(100).ConfigureAwait(false);}}finally{if(File.Exists(tmp))File.Delete(tmp);}if(complete!=null)complete();
 }}catch(Exception e){Log.Error("Kiro",e);}finally{Interlocked.Exchange(ref running,0);}
 }
}}

