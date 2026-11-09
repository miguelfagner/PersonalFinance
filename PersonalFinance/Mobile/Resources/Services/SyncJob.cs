using Android.App;
using Android.App.Job;
using Android.Content;

namespace PersonalFinance.Resources.Services;

[Service(Permission="android.permission.BIND_JOB_SERVICE",Exported=true)]
public sealed class SyncJob : JobService
{
    public override bool OnStartJob(JobParameters? parameters)
    {
        _=RunAsync(parameters);return true;
    }
    private async Task RunAsync(JobParameters? parameters){try{await AndroidSync.SyncAsync();}finally{JobFinished(parameters,false);}}
    public override bool OnStopJob(JobParameters? parameters)=>true;
    public static void Schedule()
    {
        var context=Application.Context;
        var scheduler=(JobScheduler?)context.GetSystemService(Context.JobSchedulerService);
        var component=new ComponentName(context,Java.Lang.Class.FromType(typeof(SyncJob)));
        var job=new JobInfo.Builder(1045,component).SetRequiredNetworkType(NetworkType.Any)!.SetPeriodic(15*60*1000)!.Build();
        scheduler?.Schedule(job!);
    }
}