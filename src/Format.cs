using System;
namespace T3Usage {
public static class Format {
 public static string Duration(TimeSpan d){if(d<TimeSpan.Zero)d=TimeSpan.Zero;if(d.TotalDays>=1)return (int)d.TotalDays+"d "+d.Hours+"h";if(d.TotalHours>=1)return (int)d.TotalHours+"h "+d.Minutes+"m";return (int)d.TotalMinutes+"m";}
 public static string ResetShort(UsageWindow w,DateTime now){if(w==null||!w.ResetsAtUtc.HasValue)return "";if(w.ResetsAtUtc<=now)return "awaiting new data";var d=w.ResetsAtUtc.Value-now;return d.TotalMinutes<1?"↻ soon":"↻ "+Duration(d);}
 public static string ResetLong(UsageWindow w,DateTime now){if(!w.ResetsAtUtc.HasValue)return "";if(w.ResetsAtUtc<=now)return "Awaiting new data";var t=w.ResetsAtUtc.Value.ToLocalTime();var today=now.ToLocalTime().Date;string day=t.Date==today?"today":t.Date==today.AddDays(1)?"tomorrow":t.ToString(t.Year==today.Year?"dd/MM":"dd/MM/yyyy");return "Reset "+day+(w.DateOnly?"":" "+t.ToString("HH:mm"))+" · in "+Duration(w.ResetsAtUtc.Value-now)+(w.Monthly?" (monthly)":"");}
 public static string Left(UsageResult u){return u==null?"Quota not available yet":u.AwaitingRefresh?"Awaiting new data":u.Left.Value+"% left";}
}}
