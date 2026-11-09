using Android.Content;
using Android.Views;

namespace PersonalFinance.Resources.Activities;

/// <summary>
/// Base comum das telas internas do aplicativo.
/// </summary>
public abstract class FinanceActivity : Activity
{
    protected override void OnResume() { base.OnResume(); Services.AndroidSync.Updated += RemoteUpdated; Services.SyncLifecycle.Resume(); }
    protected override void OnPause() { Services.AndroidSync.Updated -= RemoteUpdated; Services.SyncLifecycle.Pause(); base.OnPause(); }
    private void RemoteUpdated() => RunOnUiThread(async () => { if(!IsFinishing&&!IsDestroyed)try{await RefreshAfterSyncAsync();}catch(Exception e){Android.Util.Log.Error("Sync",e.Message);} });
    protected virtual Task RefreshAfterSyncAsync() => Task.CompletedTask;
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        if (ActionBar == null)
            return;

        ActionBar.SetDisplayHomeAsUpEnabled(true);
        ActionBar.SetHomeButtonEnabled(true);
        ActionBar.SetHomeAsUpIndicator(Resource.Drawable.ic_home);
        ActionBar.SetHomeActionContentDescription(Resource.String.ir_para_inicio);
    }

    public override bool OnOptionsItemSelected(IMenuItem item)
    {
        if (item.ItemId != Android.Resource.Id.Home)
            return base.OnOptionsItemSelected(item);

        IrParaInicio();
        return true;
    }

    protected void IrParaInicio()
    {
        var intent = new Intent(this, typeof(global::PersonalFinance.MainActivity));
        intent.AddFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
        StartActivity(intent);
        Finish();
    }
}
