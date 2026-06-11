using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using PersonalFinance.Resources.Services;

namespace PersonalFinance.Resources.Activities
{
    [Activity(Label = "Importar Backup")]
    public class BackupImportActivity : Activity
    {
        private const int RequestImportBackup = 1235;
        private Button? _btnImport;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.activity_backup_import);

            _btnImport = FindViewById<Button>(Resource.Id.btnImportarArquivo);
            _btnImport!.Click += (s, e) => ConfirmImport();
        }

        private void ConfirmImport()
        {
            new AlertDialog.Builder(this)
                .SetTitle("Importar backup")
                .SetMessage("A importação substituirá todos os dados atuais pelos dados do backup selecionado. Deseja continuar?")
                .SetNegativeButton("Cancelar", (s, e) => { })
                .SetPositiveButton("Escolher arquivo", (s, e) => OpenFilePicker())
                .Show();
        }

        private void OpenFilePicker()
        {
            var intent = new Intent(Intent.ActionOpenDocument);
            intent.AddCategory(Intent.CategoryOpenable);
            intent.SetType("*/*");

            StartActivityForResult(intent, RequestImportBackup);
        }

        protected override async void OnActivityResult(int requestCode, Result resultCode, Intent? data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            if (requestCode != RequestImportBackup || resultCode != Result.Ok || data?.Data == null)
                return;

            await ImportBackupAsync(data.Data);
        }

        private async Task ImportBackupAsync(Android.Net.Uri source)
        {
            SetImportingState(true);

            try
            {
                await DatabaseBackupService.ImportAsync(ContentResolver!, source);

                Toast.MakeText(this, "Backup importado com sucesso!", ToastLength.Long)?.Show();

                var intent = new Intent(this, typeof(MainActivity));
                intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTask);
                StartActivity(intent);
                Finish();
            }
            catch (InvalidDataException exception)
            {
                ShowError("Backup inválido", exception.Message);
            }
            catch
            {
                ShowError(
                    "Não foi possível importar",
                    "A importação não foi concluída. Feche e abra o aplicativo antes de tentar novamente.");
            }
            finally
            {
                SetImportingState(false);
            }
        }

        private void SetImportingState(bool importing)
        {
            if (_btnImport == null)
                return;

            _btnImport.Enabled = !importing;
            _btnImport.Text = importing ? "Importando..." : "Escolher backup";
        }

        private void ShowError(string title, string message)
        {
            new AlertDialog.Builder(this)
                .SetTitle(title)
                .SetMessage(message)
                .SetPositiveButton("OK", (s, e) => { })
                .Show();
        }
    }
}
