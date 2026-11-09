using Android.App;
using Android.Content;
using Android.Net;
using Android.OS;

namespace PersonalFinance.Resources.Services;

public static class SyncLifecycle
{
    private static CancellationTokenSource? foreground;
    private static ConnectivityReceiver? receiver;
    public static void Resume()
    {
        Pause();
        foreground=new();var token=foreground.Token;
        _=RunAsync(token);
        receiver=new();
        var filter=new IntentFilter(ConnectivityManager.ConnectivityAction);
        if(Build.VERSION.SdkInt>=BuildVersionCodes.Tiramisu)Application.Context.RegisterReceiver(receiver,filter,ReceiverFlags.NotExported);
        else Application.Context.RegisterReceiver(receiver,filter);
    }
    public static void Pause()
    {
        foreground?.Cancel();foreground?.Dispose();foreground=null;
        if(receiver is not null){try{Application.Context.UnregisterReceiver(receiver);}catch(Java.Lang.IllegalArgumentException){}receiver=null;}
        _=AndroidSync.SyncAsync();
    }
    private static async Task RunAsync(CancellationToken token)
    {
        try{await AndroidSync.SyncAsync();using var timer=new PeriodicTimer(TimeSpan.FromSeconds(30));while(await timer.WaitForNextTickAsync(token))await AndroidSync.SyncAsync();}catch(System.OperationCanceledException){}
    }
    private sealed class ConnectivityReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context? context,Intent? intent){_=AndroidSync.SyncAsync();}
    }
}