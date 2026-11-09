using Android.App;
using Android.OS;
using Android.Text;
using Android.Views;
using Android.Widget;
using PersonalFinance.Resources.Services;

namespace PersonalFinance.Resources.Activities;

[Activity(Label="Sincronização")]
public sealed class SyncActivity : FinanceActivity
{
    private LinearLayout root=null!;
    private TextView status=null!;
    private EditText url=null!,email=null!,password=null!;
    private bool busy;
    protected override Task RefreshAfterSyncAsync() => RefreshAsync();
    protected override async void OnCreate(Bundle? state)
    {
        base.OnCreate(state);
        var scroll=new ScrollView(this);root=new LinearLayout(this){Orientation=Orientation.Vertical};root.SetPadding(24,24,24,24);scroll.AddView(root);SetContentView(scroll);
        Text("Entre com a mesma conta do Blazor. Na primeira sincronização, o histórico deste celular atualiza o Web automaticamente, sem revisão dos lançamentos. Uma cópia dos dados anteriores é preservada. Depois, as alterações são sincronizadas entre os dois aplicativos.",18);
        url=Input("Endereço HTTPS do servidor",AndroidSync.Url);
        email=Input("E-mail",AndroidSync.Email);email.InputType=InputTypes.ClassText|InputTypes.TextVariationEmailAddress;
        password=Input("Senha","");password.InputType=InputTypes.ClassText|InputTypes.TextVariationPassword;
        Button("Entrar",async()=>{await AndroidSync.LoginAsync(url.Text!.Trim(),email.Text!.Trim(),password.Text!);password.Text="";await RefreshAsync();});
        Button("Sincronizar agora",async()=>{await AndroidSync.SyncAsync();await RefreshAsync();});
        Button("Ver alterações rejeitadas",ShowRejectedAsync);
        Button("Exportar backup",()=>{StartActivity(typeof(BackupExportActivity));return Task.CompletedTask;});
        Button("Sair da conta",()=>{AndroidSync.Logout();status.Text="Conta desconectada. Dados locais e pendências preservados.";return Task.CompletedTask;});
        status=Text("",15);await RefreshAsync();
    }
    private TextView Text(string value,int size=15){var view=new TextView(this){Text=value,TextSize=size};view.SetPadding(0,12,0,12);root.AddView(view);return view;}
    private EditText Input(string hint,string value){var view=new EditText(this){Hint=hint,Text=value};root.AddView(view);return view;}
    private void Button(string label,Func<Task> action)
    {
        var button=new Button(this){Text=label};root.AddView(button);
        button.Click+=async(_,_)=>{if(busy)return;busy=true;button.Enabled=false;try{await action();}catch(Exception e){status.Text=e.Message;}finally{busy=false;button.Enabled=true;}};
    }
    private async Task RefreshAsync()
    {
        var db=new DatabaseService();var state=await db.GetSyncStateAsync();var count=await db.PendingSyncCountAsync()+await db.PendingCommandCountAsync();
        var rejected=await db.RejectedSyncCountAsync();
        var mode=state.NeedsReconciliation||!state.Initialized?"Aguardando primeira sincronização":
            AndroidSync.Connected && TokenVault.Read() is not null && AndroidSync.LastError is null?"Online · API principal":"Offline · cache local";
        status.Text=$"Modo: {mode}\n{count} alteração(ões) pendente(s). {rejected} rejeitada(s) preservada(s).\n"+(state.NeedsReconciliation||!state.Initialized?"Entre na conta ou toque em Sincronizar agora. Os dados do celular serão usados na primeira carga.":"Sincronização automática ativa.")+"\nÚltima sincronização: "+(state.LastSyncUtc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm")??"Ainda não realizada")+"\n"+AndroidSync.LastError;
    }
    private async Task ShowRejectedAsync()
    {
        var rejected=await new DatabaseService().RejectedSyncAsync();
        var content=rejected.Count==0?"Nenhuma alteração rejeitada.":string.Join("\n\n",rejected.Select(x=>$"{x.RejectedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm} · {x.EntityType}\n{x.Reason}\n{x.Payload}"));
        new AlertDialog.Builder(this).SetTitle("Alterações locais preservadas").SetMessage(content).SetPositiveButton("Fechar",(_,_)=>{}).Show();
    }
}
