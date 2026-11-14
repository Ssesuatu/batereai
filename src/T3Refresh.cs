using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace T3Usage {
// Ask the running T3 server to refresh its own providers. Never probe provider
// credentials ourselves or change T3's provider cache files.
public static class T3Refresh {
 static readonly string[] ProcessNames={"T3 Code","T3 Code (Nightly)","T3 Code (Beta)","T3 Code (Dev)"};
 static string Asar(string exe){return Path.Combine(Path.GetDirectoryName(exe),"resources","server.asar");}
 // Electron resolves paths inside the archive; .NET only sees server.asar as one file.
 static string Entry(string exe){return Path.Combine(Asar(exe),"apps","server","dist","bin.mjs");}
 // Any release channel. The running T3 wins so the CLI matches the server it talks to.
 static string FindExe(){
  foreach(string n in ProcessNames)foreach(var p in Process.GetProcessesByName(n)){using(p){try{string f=p.MainModule.FileName;if(File.Exists(Asar(f)))return f;}catch{}}}
  string programs=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs");
  if(!Directory.Exists(programs))return null;
  foreach(string dir in Directory.GetDirectories(programs,"t3code*"))foreach(string f in Directory.GetFiles(dir,"T3 Code*.exe",SearchOption.TopDirectoryOnly))if(File.Exists(Asar(f)))return f;
  return null;
 }
 static string Cli(string home,string command){
  string exe=FindExe();
  if(exe==null)throw new IOException("Installed T3 Code not found");
  string entry=Entry(exe);
  if(home.Contains("\""))throw new IOException("Invalid T3 location");
  var si=new ProcessStartInfo(exe,"\""+entry+"\" auth session "+command+" --base-dir \""+home+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
  si.EnvironmentVariables["ELECTRON_RUN_AS_NODE"]="1";
  using(var p=Process.Start(si)){
   var stdout=p.StandardOutput.ReadToEndAsync();var stderr=p.StandardError.ReadToEndAsync();
   if(!p.WaitForExit(15000)){p.Kill();throw new TimeoutException("T3 CLI did not respond");}
   if(!Task.WaitAll(new Task[]{stdout,stderr},2000))throw new TimeoutException("T3 CLI output did not finish");
   if(p.ExitCode!=0)throw new IOException("T3 refresh authentication failed");
   return stdout.Result;
  }
 }
 public static async Task RefreshAsync(string home){
  var runtime=DataCore.Read(Path.Combine(home,"userdata","server-runtime.json"));
  Uri origin;
  if(!Uri.TryCreate(runtime.GetString("origin"),UriKind.Absolute,out origin)||!origin.IsLoopback||origin.Scheme!="http")throw new IOException("Local T3 server not found");
  string sessionId=null;
  try{
   var session=SimpleJson.ParseObject(Cli(home,"issue --scope orchestration:read --ttl 2m --label Batereai-refresh --json"));
   sessionId=session.GetString("sessionId");string token=session.GetString("token");
   if(string.IsNullOrEmpty(token)||string.IsNullOrEmpty(sessionId))throw new IOException("T3 refresh session unavailable");
   using(var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(90)))using(var ws=new ClientWebSocket()){
    ws.Options.SetRequestHeader("Authorization","Bearer "+token);
    await ws.ConnectAsync(new Uri("ws://"+origin.Authority+"/ws?orchestrationProtocol=2"),timeout.Token).ConfigureAwait(false);
    var bytes=Encoding.UTF8.GetBytes("{\"_tag\":\"Request\",\"id\":\"1\",\"tag\":\"server.refreshProviders\",\"payload\":{\"fresh\":true},\"headers\":[]}");
    await ws.SendAsync(new ArraySegment<byte>(bytes),WebSocketMessageType.Text,true,timeout.Token).ConfigureAwait(false);
    var buffer=new byte[16384];
    for(;;){
     using(var message=new MemoryStream()){
      WebSocketReceiveResult part;
      do{part=await ws.ReceiveAsync(new ArraySegment<byte>(buffer),timeout.Token).ConfigureAwait(false);if(part.MessageType==WebSocketMessageType.Close)throw new IOException("T3 refresh connection closed");message.Write(buffer,0,part.Count);if(message.Length>16*1024*1024)throw new IOException("T3 response too large");}while(!part.EndOfMessage);
      var reply=SimpleJson.ParseObject(Encoding.UTF8.GetString(message.ToArray()));
      if(reply==null)continue;
      if(reply.GetString("_tag")=="Ping"){var pong=Encoding.UTF8.GetBytes("{\"_tag\":\"Pong\"}");await ws.SendAsync(new ArraySegment<byte>(pong),WebSocketMessageType.Text,true,timeout.Token).ConfigureAwait(false);continue;}
      if(reply.GetString("_tag")!="Exit"||reply.GetString("requestId")!="1")continue;
      var exit=reply.GetObj("exit");if(exit==null||exit.GetString("_tag")!="Success")throw new IOException("T3 failed to refresh providers");
      return;
     }
    }
   }
  }finally{if(!string.IsNullOrEmpty(sessionId)&&System.Text.RegularExpressions.Regex.IsMatch(sessionId,@"^[a-zA-Z0-9:_-]+$"))try{Cli(home,"revoke --id "+sessionId);}catch(Exception e){Log.Error("Refresh session cleanup",e);}}
 }
}
}
