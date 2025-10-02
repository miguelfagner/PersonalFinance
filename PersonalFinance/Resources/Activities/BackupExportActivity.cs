using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using Android.Net;
using System.IO;
using System.Threading.Tasks;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Exportar Backup")]
    public class BackupExportActivity : Activity
    {
        private const int RequestExportBackup = 1234;
        private readonly string _dbPath = Path.Combine(Application.Context.FilesDir.AbsolutePath, "financeiro.db");

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            var btnExport = new Button(this) { Text = "Exportar Backup para Downloads" };
            btnExport.Click += (s, e) => ExportToDownloads();

            SetContentView(btnExport);
        }

        // Abre seletor para salvar o arquivos
        private void ExportToDownloads()
        {
            var intent = new Intent(Intent.ActionCreateDocument);
            intent.AddCategory(Intent.CategoryOpenable);
            intent.SetType("application/octet-stream");
            intent.PutExtra(Intent.ExtraTitle, $"financeiro_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db");

            StartActivityForResult(intent, RequestExportBackup);
        }

        // Resultado do seletor
        protected override async void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            if (requestCode == RequestExportBackup && resultCode == Result.Ok && data != null)
            {
                await HandleExportResultAsync(data.Data);
                Toast.MakeText(this, "Backup exportado com sucesso!", ToastLength.Long).Show();
            }
        }

        // Copia o banco para o local escolhido
        private async Task HandleExportResultAsync(Android.Net.Uri uri)
        {
            using (var input = File.OpenRead(_dbPath))
            using (var output = ContentResolver.OpenOutputStream(uri))
            {
                await input.CopyToAsync(output);
            }
        }
    }
}
