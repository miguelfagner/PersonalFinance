using Android.Views;
using PersonalFinance.Resources.Services;
using System.Globalization;

namespace PersonalFinance.Resources.Activities;

/// <summary>
/// Navegação e carregamento comuns às listas de lançamentos mensais.
/// </summary>
public abstract class ListaMensalActivity : FinanceActivity
{
        protected override Task RefreshAfterSyncAsync() => CarregarAsync();

    private const string EstadoAno = "lista_mensal_ano";
    private const string EstadoMes = "lista_mensal_mes";
    private static readonly CultureInfo CulturaPtBr = CultureInfo.GetCultureInfo("pt-BR");
    // A consulta também precisa representar o primeiro dia do mês seguinte.
    private static readonly DateTime UltimaCompetencia = new(9999, 11, 1);

    private FinanceRepository? _db;
    private TextView _mesAtual = null!;
    private TextView _mesAnterior = null!;
    private TextView _proximoMes = null!;
    private View _estado = null!;
    private ProgressBar _progresso = null!;
    private TextView _mensagem = null!;
    private Button _tentarNovamente = null!;
    private GestureDetector _gestos = null!;
    private int _versaoCarregamento;
    private bool _destruida;

    protected abstract int LayoutLista { get; }
    protected abstract int IdLista { get; }
    protected abstract string TituloLista { get; }
    protected abstract int MensagemSemLancamentos { get; }
    protected ListView Lista { get; private set; } = null!;
    protected DateTime Competencia { get; private set; }
    protected FinanceRepository Database => _db ??= new FinanceRepository();

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(LayoutLista);

        Competencia = ObterCompetenciaInicial(savedInstanceState);
        Lista = FindViewById<ListView>(IdLista)!;
        _mesAtual = FindViewById<TextView>(Resource.Id.tvMesAtual)!;
        _mesAnterior = FindViewById<TextView>(Resource.Id.btnMesAnterior)!;
        _proximoMes = FindViewById<TextView>(Resource.Id.btnProximoMes)!;
        _estado = FindViewById(Resource.Id.estadoListaMensal)!;
        _progresso = FindViewById<ProgressBar>(Resource.Id.progressoListaMensal)!;
        _mensagem = FindViewById<TextView>(Resource.Id.mensagemListaMensal)!;
        _tentarNovamente = FindViewById<Button>(Resource.Id.btnTentarNovamente)!;

        _mesAnterior.Click += async (_, _) => await NavegarMesAsync(-1);
        _proximoMes.Click += async (_, _) => await NavegarMesAsync(1);
        _tentarNovamente.Click += async (_, _) => await CarregarAsync();

        var areaGesto = FindViewById<View>(Resource.Id.areaGestoMes)!;
        var densidade = Resources?.DisplayMetrics?.Density ?? 1f;
        _gestos = new GestureDetector(this, new GestoMesListener(
            deslocamento => { _ = NavegarMesAsync(deslocamento); }, densidade));
        areaGesto.Touch += (_, e) =>
        {
            if (e.Event == null)
                return;

            var reconhecido = _gestos.OnTouchEvent(e.Event);
            if (e.Event.ActionMasked == MotionEventActions.Up && !reconhecido)
                areaGesto.PerformClick();

            // Somente o centro do cabeçalho recebe gestos; a lista mantém seus toques.
            e.Handled = true;
        };

        AtualizarCabecalho();
    }

    protected override async void OnResume()
    {
        base.OnResume();
        // Também faz a carga inicial, evitando uma consulta duplicada em OnCreate.
        await CarregarAsync();
    }

    protected override void OnPause()
    {
        _versaoCarregamento++;
        base.OnPause();
    }

    protected override void OnSaveInstanceState(Bundle outState)
    {
        outState.PutInt(EstadoAno, Competencia.Year);
        outState.PutInt(EstadoMes, Competencia.Month);
        base.OnSaveInstanceState(outState);
    }

    protected override void OnDestroy()
    {
        _destruida = true;
        _versaoCarregamento++;
        _gestos?.Dispose();
        base.OnDestroy();
    }

    protected abstract Task<IListAdapter> CarregarAdapterAsync(DateTime inicio, DateTime fim);

    private DateTime ObterCompetenciaInicial(Bundle? estado)
    {
        if (estado != null &&
            TentarObterCompetencia(estado.GetInt(EstadoAno), estado.GetInt(EstadoMes), out var salva))
            return salva;

        var hoje = DateTime.Today;
        if (TentarObterCompetencia(
            Intent?.GetIntExtra("Ano", hoje.Year) ?? hoje.Year,
            Intent?.GetIntExtra("Mes", hoje.Month) ?? hoje.Month,
            out var recebida))
            return recebida;

        return new DateTime(hoje.Year, hoje.Month, 1);
    }

    private static bool TentarObterCompetencia(int ano, int mes, out DateTime competencia)
    {
        competencia = default;
        if (ano < 1 || ano > 9999 || mes < 1 || mes > 12)
            return false;

        competencia = new DateTime(ano, mes, 1);
        return competencia <= UltimaCompetencia;
    }

    private async Task NavegarMesAsync(int deslocamento)
    {
        if (_destruida || IsFinishing || IsDestroyed ||
            (deslocamento < 0 && Competencia == DateTime.MinValue) ||
            (deslocamento > 0 && Competencia == UltimaCompetencia))
            return;

        Competencia = Competencia.AddMonths(deslocamento);
        AtualizarCabecalho();
        await CarregarAsync();
    }

    private void AtualizarCabecalho()
    {
        Title = $"{TituloLista} - {Competencia:MM/yyyy}";
        _mesAtual.Text = Competencia.ToString("MMMM 'de' yyyy", CulturaPtBr).ToUpper(CulturaPtBr);
        _mesAnterior.Enabled = Competencia > DateTime.MinValue;
        _proximoMes.Enabled = Competencia < UltimaCompetencia;
        _mesAnterior.Alpha = _mesAnterior.Enabled ? 1f : 0.45f;
        _proximoMes.Alpha = _proximoMes.Enabled ? 1f : 0.45f;
    }

    private async Task CarregarAsync()
    {
        if (_destruida || IsFinishing || IsDestroyed)
            return;

        var versao = ++_versaoCarregamento;
        var inicio = Competencia;
        Lista.Visibility = ViewStates.Gone;
        Lista.Adapter = null;
        MostrarEstado(Resource.String.lista_mensal_carregando, carregando: true);

        try
        {
            var adapter = await CarregarAdapterAsync(inicio, inicio.AddMonths(1));
            if (!PodeAtualizar(versao))
                return;

            Lista.Adapter = adapter;
            if (adapter.Count == 0)
            {
                MostrarEstado(MensagemSemLancamentos);
                return;
            }

            _estado.Visibility = ViewStates.Gone;
            _progresso.Visibility = ViewStates.Gone;
            Lista.Visibility = ViewStates.Visible;
        }
        catch (Exception ex)
        {
            if (!PodeAtualizar(versao))
                return;

            Android.Util.Log.Error(nameof(ListaMensalActivity), ex.ToString());
            MostrarEstado(Resource.String.lista_mensal_erro, erro: true);
        }
    }

    private bool PodeAtualizar(int versao) =>
        versao == _versaoCarregamento && !_destruida && !IsFinishing && !IsDestroyed;

    private void MostrarEstado(int mensagem, bool carregando = false, bool erro = false)
    {
        _estado.Visibility = ViewStates.Visible;
        _mensagem.SetText(mensagem);
        _progresso.Visibility = carregando ? ViewStates.Visible : ViewStates.Gone;
        _tentarNovamente.Visibility = erro ? ViewStates.Visible : ViewStates.Gone;
    }

    private sealed class GestoMesListener(Action<int> navegar, float densidade)
        : GestureDetector.SimpleOnGestureListener
    {
        public override bool OnDown(MotionEvent? e) => true;

        public override bool OnFling(MotionEvent? inicio, MotionEvent? fim, float velocidadeX, float velocidadeY)
        {
            if (inicio == null || fim == null)
                return false;

            var deltaX = fim.GetX() - inicio.GetX();
            var deltaY = fim.GetY() - inicio.GetY();
            if (Math.Abs(deltaX) <= Math.Abs(deltaY) ||
                Math.Abs(deltaX) < 64 * densidade || Math.Abs(velocidadeX) < 100 * densidade)
                return false;

            navegar(deltaX > 0 ? -1 : 1);
            return true;
        }
    }
}
