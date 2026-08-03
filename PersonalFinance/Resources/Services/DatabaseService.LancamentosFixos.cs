using PersonalFinance.Resources.Models;
using SQLite;

namespace PersonalFinance.Resources.Services
{
    public partial class DatabaseService
    {
        public Task<List<ReceitaFixa>> ListarReceitasFixasAsync(bool incluirInativas = false)
        {
            var consulta = _db.Table<ReceitaFixa>();
            if (!incluirInativas)
                consulta = consulta.Where(item => item.Ativa);

            return consulta
                .OrderBy(item => item.FontePagadora)
                .ThenBy(item => item.Descricao)
                .ToListAsync();
        }

        public async Task<ReceitaFixa?> PegarReceitaFixaAsync(int id)
        {
            return await _db.FindAsync<ReceitaFixa>(id);
        }

        public async Task<int> SalvarReceitaFixaAsync(ReceitaFixa receitaFixa)
        {
            ArgumentNullException.ThrowIfNull(receitaFixa);
            ValidarReceitaFixa(receitaFixa);

            var alteracoes = 0;
            await _db.RunInTransactionAsync(conexao =>
            {
                if (receitaFixa.Id == 0)
                {
                    alteracoes = conexao.Insert(receitaFixa);
                    return;
                }

                if (conexao.Find<ReceitaFixa>(receitaFixa.Id) is null)
                    throw new InvalidOperationException("A receita fixa nao existe mais.");

                alteracoes = conexao.Update(receitaFixa);
                if (!receitaFixa.Ativa)
                {
                    conexao.Execute(
                        "UPDATE DespesaFixa SET Ativa = 0 WHERE ReceitaFixaId = ?",
                        receitaFixa.Id);
                }
            });

            return alteracoes;
        }

        public async Task<int> PausarReceitaFixaAsync(int id, bool pausada = true)
        {
            var alteracoes = 0;
            await _db.RunInTransactionAsync(conexao =>
            {
                var receita = conexao.Find<ReceitaFixa>(id)
                    ?? throw new InvalidOperationException("A receita fixa nao foi encontrada.");

                receita.Ativa = !pausada;
                alteracoes = conexao.Update(receita);

                // Reativar uma receita nao reativa automaticamente suas despesas.
                if (pausada)
                {
                    conexao.Execute(
                        "UPDATE DespesaFixa SET Ativa = 0 WHERE ReceitaFixaId = ?",
                        id);
                }
            });

            return alteracoes;
        }

        public async Task<int> ExcluirReceitaFixaAsync(int id)
        {
            var alteracoes = 0;
            await _db.RunInTransactionAsync(conexao =>
            {
                var receita = conexao.Find<ReceitaFixa>(id)
                    ?? throw new InvalidOperationException("A receita fixa nao foi encontrada.");
                var dependencias = conexao.Table<DespesaFixa>()
                    .Count(item => item.ReceitaFixaId == id);

                if (dependencias > 0)
                {
                    throw new InvalidOperationException(
                        "Esta receita fixa possui despesas vinculadas. Exclua as despesas antes de continuar.");
                }

                alteracoes = conexao.Delete(receita);
            });

            return alteracoes;
        }

        public async Task<List<DespesaFixa>> ListarDespesasFixasAsync(bool incluirInativas = false)
        {
            var consulta = _db.Table<DespesaFixa>();
            if (!incluirInativas)
                consulta = consulta.Where(item => item.Ativa);

            var despesas = await consulta
                .OrderBy(item => item.DiaVencimento)
                .ThenBy(item => item.Descricao)
                .ToListAsync();
            var receitas = await _db.Table<ReceitaFixa>().ToListAsync();
            var receitasPorId = receitas.ToDictionary(item => item.Id);

            foreach (var despesa in despesas)
            {
                if (receitasPorId.TryGetValue(despesa.ReceitaFixaId, out var receita))
                    despesa.ReceitaFixa = receita;
            }

            return despesas;
        }

        public async Task<DespesaFixa?> PegarDespesaFixaAsync(int id)
        {
            return await _db.FindAsync<DespesaFixa>(id);
        }

        public async Task<int> SalvarDespesaFixaAsync(DespesaFixa despesaFixa)
        {
            ArgumentNullException.ThrowIfNull(despesaFixa);
            ValidarDespesaFixa(despesaFixa);

            var alteracoes = 0;
            await _db.RunInTransactionAsync(conexao =>
            {
                var receita = conexao.Find<ReceitaFixa>(despesaFixa.ReceitaFixaId)
                    ?? throw new InvalidOperationException("Selecione uma receita fixa valida.");

                if (despesaFixa.Ativa && !receita.Ativa)
                    throw new InvalidOperationException("Reative a receita fixa antes de ativar esta despesa.");

                if (despesaFixa.Id == 0)
                {
                    alteracoes = conexao.Insert(despesaFixa);
                    return;
                }

                if (conexao.Find<DespesaFixa>(despesaFixa.Id) is null)
                    throw new InvalidOperationException("A despesa fixa nao existe mais.");

                alteracoes = conexao.Update(despesaFixa);
            });

            return alteracoes;
        }

        public async Task<int> PausarDespesaFixaAsync(int id, bool pausada = true)
        {
            var alteracoes = 0;
            await _db.RunInTransactionAsync(conexao =>
            {
                var despesa = conexao.Find<DespesaFixa>(id)
                    ?? throw new InvalidOperationException("A despesa fixa nao foi encontrada.");

                if (!pausada)
                {
                    var receita = conexao.Find<ReceitaFixa>(despesa.ReceitaFixaId);
                    if (receita is null || !receita.Ativa)
                        throw new InvalidOperationException("Reative a receita fixa antes de reativar esta despesa.");
                }

                despesa.Ativa = !pausada;
                alteracoes = conexao.Update(despesa);
            });

            return alteracoes;
        }

        public async Task<int> ExcluirDespesaFixaAsync(int id)
        {
            var despesa = await _db.FindAsync<DespesaFixa>(id)
                ?? throw new InvalidOperationException("A despesa fixa nao foi encontrada.");
            return await _db.DeleteAsync(despesa);
        }

        public async Task<ImportacaoFixosResultado> ImportarLancamentosComoFixosAsync(
            DateTime competencia,
            IReadOnlyCollection<int> receitaIds,
            IReadOnlyCollection<int> despesaIds)
        {
            ArgumentNullException.ThrowIfNull(receitaIds);
            ArgumentNullException.ThrowIfNull(despesaIds);

            var inicio = NormalizarCompetencia(competencia);
            var fim = inicio.AddMonths(1);
            var receitasSelecionadas = receitaIds.Where(id => id > 0).ToHashSet();
            var despesasSelecionadas = despesaIds.Where(id => id > 0).ToHashSet();
            var resultado = new ImportacaoFixosResultado();

            await _db.RunInTransactionAsync(conexao =>
            {
                var despesasMensais = new List<Despesa>();
                foreach (var id in despesasSelecionadas)
                {
                    var despesa = conexao.Find<Despesa>(id);
                    if (despesa is null || despesa.Vencimento < inicio || despesa.Vencimento >= fim)
                    {
                        resultado.Ignorados++;
                        continue;
                    }

                    despesasMensais.Add(despesa);
                    receitasSelecionadas.Add(despesa.ReceitaId);
                }

                var receitasMensais = new Dictionary<int, Receita>();
                foreach (var id in receitasSelecionadas)
                {
                    var receita = conexao.Find<Receita>(id);
                    var foiIncluidaPorDespesa = despesasMensais.Any(item => item.ReceitaId == id);
                    if (receita is null ||
                        (!foiIncluidaPorDespesa &&
                         (receita.MesReferencia < inicio || receita.MesReferencia >= fim)))
                    {
                        resultado.Ignorados++;
                        continue;
                    }

                    receitasMensais[id] = receita;
                }

                var modelosReceita = new Dictionary<int, ReceitaFixa>();
                var receitasDestino = new Dictionary<int, Receita>();
                foreach (var receita in receitasMensais.Values.OrderBy(item => item.Id))
                {
                    var modelo = receita.ReceitaFixaId.HasValue
                        ? conexao.Find<ReceitaFixa>(receita.ReceitaFixaId.Value)
                        : null;

                    if (modelo is null)
                    {
                        modelo = new ReceitaFixa
                        {
                            FontePagadora = receita.FontePagadora ?? string.Empty,
                            Descricao = receita.Descricao ?? string.Empty,
                            Tipo = receita.Tipo ?? string.Empty,
                            Valor = receita.Valor,
                            Ativa = true
                        };
                        ValidarReceitaFixa(modelo);
                        conexao.Insert(modelo);
                        resultado.ReceitasCriadas++;
                    }
                    else
                    {
                        resultado.ReceitasReutilizadas++;
                    }

                    modelosReceita[receita.Id] = modelo;
                    var competenciaOriginal = NormalizarCompetencia(receita.MesReferencia);
                    var receitaOriginalAssociada = AssociarReceitaImportada(
                        conexao,
                        receita,
                        modelo,
                        competenciaOriginal,
                        resultado);
                    var receitaDoDestino = competenciaOriginal == inicio
                        ? receitaOriginalAssociada
                        : ObterOuCriarReceitaMensal(conexao, modelo, inicio);
                    receitasDestino[receita.Id] = receitaDoDestino;
                    RegistrarDecisao(
                        conexao,
                        TiposModeloFixo.Receita,
                        modelo.Id,
                        inicio,
                        EstadosPreparacaoMensal.Gerado);
                }

                foreach (var despesa in despesasMensais.OrderBy(item => item.Id))
                {
                    var receitaOriginalId = despesa.ReceitaId;
                    if (!modelosReceita.TryGetValue(receitaOriginalId, out var receitaFixa) ||
                        !receitasDestino.TryGetValue(receitaOriginalId, out var receitaDoDestino))
                    {
                        resultado.Ignorados++;
                        continue;
                    }

                    // Corrige o fluxo legado que permitia uma despesa apontar para uma
                    // receita de outro mes. A receita original permanece no mes dela.
                    despesa.ReceitaId = receitaDoDestino.Id;

                    var modelo = despesa.DespesaFixaId.HasValue
                        ? conexao.Find<DespesaFixa>(despesa.DespesaFixaId.Value)
                        : null;

                    if (modelo is null)
                    {
                        modelo = new DespesaFixa
                        {
                            ReceitaFixaId = receitaFixa.Id,
                            Descricao = despesa.Descricao ?? string.Empty,
                            Categoria = despesa.Categoria ?? string.Empty,
                            Valor = despesa.Valor,
                            DiaVencimento = despesa.Vencimento.Day,
                            Ativa = receitaFixa.Ativa
                        };
                        ValidarDespesaFixa(modelo);
                        conexao.Insert(modelo);
                        resultado.DespesasCriadas++;
                    }
                    else
                    {
                        var receitaDoModelo = conexao.Find<ReceitaFixa>(modelo.ReceitaFixaId)
                            ?? throw new InvalidOperationException(
                                "A despesa fixa importada esta sem uma receita fixa valida.");
                        if (!receitaDoModelo.Ativa && modelo.Ativa)
                        {
                            modelo.Ativa = false;
                            conexao.Update(modelo);
                        }

                        resultado.DespesasReutilizadas++;
                    }

                    AssociarDespesaImportada(conexao, despesa, modelo, inicio, resultado);
                    RegistrarDecisao(
                        conexao,
                        TiposModeloFixo.Despesa,
                        modelo.Id,
                        inicio,
                        EstadosPreparacaoMensal.Gerado);
                }
            });

            return resultado;
        }

        public async Task<List<PreparacaoMensalItem>> PreverPreparacaoMensalAsync(DateTime competencia)
        {
            var mes = NormalizarCompetencia(competencia);
            var receitasFixas = await _db.Table<ReceitaFixa>()
                .Where(item => item.Ativa)
                .OrderBy(item => item.FontePagadora)
                .ThenBy(item => item.Descricao)
                .ToListAsync();
            var despesasFixas = await _db.Table<DespesaFixa>()
                .Where(item => item.Ativa)
                .OrderBy(item => item.DiaVencimento)
                .ThenBy(item => item.Descricao)
                .ToListAsync();
            var decisoes = await _db.Table<PreparacaoMensalItem>()
                .Where(item => item.Competencia == mes)
                .ToListAsync();
            var receitasGeradas = await _db.Table<Receita>()
                .Where(item => item.CompetenciaGeracao == mes)
                .ToListAsync();
            var despesasGeradas = await _db.Table<Despesa>()
                .Where(item => item.CompetenciaGeracao == mes)
                .ToListAsync();

            var decisoesPorModelo = decisoes.ToDictionary(
                item => ChaveModelo(item.TipoModelo, item.ModeloId),
                item => item);
            var receitasGeradasPorModelo = receitasGeradas
                .Where(item => item.ReceitaFixaId.HasValue)
                .ToDictionary(item => item.ReceitaFixaId!.Value);
            var despesasGeradasPorModelo = despesasGeradas
                .Where(item => item.DespesaFixaId.HasValue)
                .ToDictionary(item => item.DespesaFixaId!.Value);
            var mesJaPreparado = decisoes.Count > 0 ||
                receitasGeradas.Count > 0 ||
                despesasGeradas.Count > 0;
            var idsReceitasAtivas = receitasFixas.Select(item => item.Id).ToHashSet();
            var itens = new List<PreparacaoMensalItem>();

            foreach (var receita in receitasFixas)
            {
                decisoesPorModelo.TryGetValue(ChaveModelo(TiposModeloFixo.Receita, receita.Id), out var decisao);
                receitasGeradasPorModelo.TryGetValue(receita.Id, out var lancamento);
                var jaLancado = lancamento is not null;
                var ignorado = !jaLancado && decisao?.Estado == EstadosPreparacaoMensal.Ignorado;
                itens.Add(new PreparacaoMensalItem
                {
                    Id = decisao?.Id ?? 0,
                    TipoModelo = TiposModeloFixo.Receita,
                    ModeloId = receita.Id,
                    Competencia = mes,
                    Estado = decisao?.Estado ?? string.Empty,
                    AtualizadoEm = decisao?.AtualizadoEm ?? default,
                    FontePagadora = lancamento?.FontePagadora ?? receita.FontePagadora ?? string.Empty,
                    Descricao = lancamento?.Descricao ?? receita.Descricao ?? string.Empty,
                    Tipo = lancamento?.Tipo ?? receita.Tipo ?? string.Empty,
                    Valor = lancamento?.Valor ?? receita.Valor,
                    Selecionado = !jaLancado && !ignorado,
                    JaLancado = jaLancado,
                    Ignorado = ignorado,
                    AtualizacaoPendente = !jaLancado && decisao is null && mesJaPreparado,
                    Motivo = jaLancado
                        ? "JA LANCADO"
                        : string.Empty
                });
            }

            foreach (var despesa in despesasFixas.Where(item => idsReceitasAtivas.Contains(item.ReceitaFixaId)))
            {
                decisoesPorModelo.TryGetValue(ChaveModelo(TiposModeloFixo.Despesa, despesa.Id), out var decisao);
                despesasGeradasPorModelo.TryGetValue(despesa.Id, out var lancamento);
                var jaLancado = lancamento is not null;
                var ignorado = !jaLancado && decisao?.Estado == EstadosPreparacaoMensal.Ignorado;
                itens.Add(new PreparacaoMensalItem
                {
                    Id = decisao?.Id ?? 0,
                    TipoModelo = TiposModeloFixo.Despesa,
                    ModeloId = despesa.Id,
                    ReceitaFixaId = despesa.ReceitaFixaId,
                    Competencia = mes,
                    Estado = decisao?.Estado ?? string.Empty,
                    AtualizadoEm = decisao?.AtualizadoEm ?? default,
                    Descricao = lancamento?.Descricao ?? despesa.Descricao ?? string.Empty,
                    Categoria = lancamento?.Categoria ?? despesa.Categoria ?? string.Empty,
                    Valor = lancamento?.Valor ?? despesa.Valor,
                    DiaVencimento = lancamento?.Vencimento.Day ?? despesa.DiaVencimento,
                    Selecionado = !jaLancado && !ignorado,
                    JaLancado = jaLancado,
                    Ignorado = ignorado,
                    AtualizacaoPendente = !jaLancado && decisao is null && mesJaPreparado,
                    Motivo = jaLancado
                        ? "JA LANCADO"
                        : string.Empty
                });
            }

            return itens;
        }

        public async Task<PreparacaoMensalResultado> PrepararMesAsync(
            DateTime competencia,
            IReadOnlyCollection<PreparacaoMensalItem> itens)
        {
            ArgumentNullException.ThrowIfNull(itens);

            var mes = NormalizarCompetencia(competencia);
            var resultado = new PreparacaoMensalResultado { Competencia = mes };
            var itensInformados = itens
                .Where(item => item.ModeloId > 0)
                .GroupBy(item => ChaveModelo(item.TipoModelo, item.ModeloId))
                .ToDictionary(grupo => grupo.Key, grupo => grupo.Last());

            await _db.RunInTransactionAsync(conexao =>
            {
                var receitasFixas = conexao.Table<ReceitaFixa>()
                    .Where(item => item.Ativa)
                    .OrderBy(item => item.Id)
                    .ToList();
                var despesasFixas = conexao.Table<DespesaFixa>()
                    .Where(item => item.Ativa)
                    .OrderBy(item => item.Id)
                    .ToList();
                var idsReceitasAtivas = receitasFixas.Select(item => item.Id).ToHashSet();
                var idsModelosDespesasExistentes = conexao.Table<Despesa>()
                    .Where(item => item.CompetenciaGeracao == mes)
                    .ToList()
                    .Where(item => item.DespesaFixaId.HasValue)
                    .Select(item => item.DespesaFixaId!.Value)
                    .ToHashSet();
                var receitasObrigatorias = despesasFixas
                    .Where(item =>
                        idsModelosDespesasExistentes.Contains(item.Id) &&
                        idsReceitasAtivas.Contains(item.ReceitaFixaId))
                    .Select(item => item.ReceitaFixaId)
                    .ToHashSet();
                var receitasMensais = new Dictionary<int, Receita>();

                // Receitas sao sempre resolvidas antes das despesas para preservar os vinculos.
                foreach (var modelo in receitasFixas)
                {
                    itensInformados.TryGetValue(
                        ChaveModelo(TiposModeloFixo.Receita, modelo.Id),
                        out var revisao);
                    var existente = conexao.Table<Receita>().FirstOrDefault(item =>
                        item.ReceitaFixaId == modelo.Id && item.CompetenciaGeracao == mes);

                    if (existente is not null)
                    {
                        receitasMensais[modelo.Id] = existente;
                        resultado.ReceitasExistentes++;
                        RegistrarDecisao(
                            conexao,
                            TiposModeloFixo.Receita,
                            modelo.Id,
                            mes,
                            EstadosPreparacaoMensal.Gerado);
                        continue;
                    }

                    if (revisao is null && !receitasObrigatorias.Contains(modelo.Id))
                    {
                        // A Activity pode ter carregado a previa antes da criacao deste
                        // modelo. Ausencia no payload significa pendente, nao ignorado.
                        continue;
                    }

                    if (revisao?.Selecionado == false && !receitasObrigatorias.Contains(modelo.Id))
                    {
                        resultado.Ignorados++;
                        RegistrarDecisao(
                            conexao,
                            TiposModeloFixo.Receita,
                            modelo.Id,
                            mes,
                            EstadosPreparacaoMensal.Ignorado);
                        continue;
                    }

                    var fontePagadora = revisao?.FontePagadora ?? modelo.FontePagadora;
                    var descricao = revisao?.Descricao ?? modelo.Descricao;
                    var tipo = revisao?.Tipo ?? modelo.Tipo;
                    var valor = revisao?.Valor ?? modelo.Valor;
                    ValidarValor(valor, "O valor da receita deve ser maior que zero.");
                    var receita = new Receita
                    {
                        MesReferencia = mes,
                        FontePagadora = fontePagadora ?? string.Empty,
                        Descricao = descricao ?? string.Empty,
                        Tipo = tipo ?? string.Empty,
                        Valor = valor,
                        ReceitaFixaId = modelo.Id,
                        CompetenciaGeracao = mes
                    };
                    conexao.Insert(receita);
                    receitasMensais[modelo.Id] = receita;
                    resultado.ReceitasCriadas++;
                    RegistrarDecisao(
                        conexao,
                        TiposModeloFixo.Receita,
                        modelo.Id,
                        mes,
                        EstadosPreparacaoMensal.Gerado);
                }

                foreach (var modelo in despesasFixas)
                {
                    itensInformados.TryGetValue(
                        ChaveModelo(TiposModeloFixo.Despesa, modelo.Id),
                        out var revisao);
                    var existente = conexao.Table<Despesa>().FirstOrDefault(item =>
                        item.DespesaFixaId == modelo.Id && item.CompetenciaGeracao == mes);

                    if (existente is not null)
                    {
                        // Se a receita mensal foi excluida e recriada, restaura apenas o
                        // vinculo tecnico; os dados do snapshot existente nao sao alterados.
                        if (receitasMensais.TryGetValue(modelo.ReceitaFixaId, out var receitaRestaurada) &&
                            existente.ReceitaId != receitaRestaurada.Id)
                        {
                            existente.ReceitaId = receitaRestaurada.Id;
                            conexao.Update(existente);
                        }
                        else if (receitaRestaurada is null)
                        {
                            var receitaVinculada = conexao.Find<Receita>(existente.ReceitaId);
                            if (receitaVinculada is null ||
                                NormalizarCompetencia(receitaVinculada.MesReferencia) != mes)
                            {
                                throw new InvalidOperationException(
                                    "Uma despesa mensal existente esta sem uma receita valida na mesma competencia.");
                            }
                        }

                        resultado.DespesasExistentes++;
                        RegistrarDecisao(
                            conexao,
                            TiposModeloFixo.Despesa,
                            modelo.Id,
                            mes,
                            EstadosPreparacaoMensal.Gerado);
                        continue;
                    }

                    if (revisao is null)
                    {
                        // Mantem modelos criados depois do carregamento como pendentes.
                        continue;
                    }

                    if (!idsReceitasAtivas.Contains(modelo.ReceitaFixaId) || !revisao.Selecionado)
                    {
                        resultado.Ignorados++;
                        RegistrarDecisao(
                            conexao,
                            TiposModeloFixo.Despesa,
                            modelo.Id,
                            mes,
                            EstadosPreparacaoMensal.Ignorado);
                        continue;
                    }

                    if (!receitasMensais.TryGetValue(modelo.ReceitaFixaId, out var receitaMensal))
                    {
                        throw new InvalidOperationException(
                            "Uma despesa fixa nao pode ser gerada sem sua receita mensal.");
                    }

                    ValidarValor(revisao.Valor, "O valor da despesa deve ser maior que zero.");
                    ValidarDiaVencimento(revisao.DiaVencimento);
                    var dia = Math.Min(revisao.DiaVencimento, DateTime.DaysInMonth(mes.Year, mes.Month));
                    var despesa = new Despesa
                    {
                        ReceitaId = receitaMensal.Id,
                        DataCadastro = DateTime.Now,
                        Vencimento = new DateTime(mes.Year, mes.Month, dia),
                        NParcela = 0,
                        Descricao = revisao.Descricao ?? string.Empty,
                        Categoria = revisao.Categoria ?? string.Empty,
                        Valor = revisao.Valor,
                        Sttatus = null,
                        replicavel = false,
                        DespesaFixaId = modelo.Id,
                        CompetenciaGeracao = mes
                    };
                    conexao.Insert(despesa);
                    resultado.DespesasCriadas++;
                    RegistrarDecisao(
                        conexao,
                        TiposModeloFixo.Despesa,
                        modelo.Id,
                        mes,
                        EstadosPreparacaoMensal.Gerado);
                }
            });

            return resultado;
        }

        public async Task<List<Despesa>> ListaDespesasPessoaisAsync(DateTime competencia)
        {
            var inicio = NormalizarCompetencia(competencia);
            var fim = inicio.AddMonths(1);
            var despesas = await _db.Table<Despesa>()
                .Where(item => item.Vencimento >= inicio && item.Vencimento < fim)
                .OrderBy(item => item.Vencimento)
                .ThenBy(item => item.Descricao)
                .ToListAsync();

            return despesas
                .Where(item => string.Equals(
                    item.Categoria?.Trim(),
                    "PESSOAL",
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public async Task<ResumoPessoal> ObterResumoPessoalAsync(DateTime competencia)
        {
            var inicio = NormalizarCompetencia(competencia);
            var fim = inicio.AddMonths(1);
            var despesas = await ListaDespesasPessoaisAsync(inicio);
            var ids = despesas.Select(item => item.Id).ToHashSet();
            var transacoesDoMes = await _db.Table<Transacao>()
                .Where(item => item.Data >= inicio && item.Data < fim)
                .ToListAsync();

            return new ResumoPessoal
            {
                Competencia = inicio,
                Orcamento = despesas.Sum(item => item.Valor),
                Realizado = transacoesDoMes
                    .Where(item => ids.Contains(item.DespesaId))
                    .Sum(item => item.Valor),
                QuantidadeDespesas = despesas.Count
            };
        }

        public async Task<int> SalvarGastoPessoalAsync(Transacao transacao)
        {
            ArgumentNullException.ThrowIfNull(transacao);
            if (transacao.Valor <= 0)
                throw new ArgumentException("Informe um valor maior que zero.", nameof(transacao));
            if (string.IsNullOrWhiteSpace(transacao.Observacao))
                throw new ArgumentException("Informe a descricao do gasto.", nameof(transacao));

            var alteracoes = 0;
            await _db.RunInTransactionAsync(conexao =>
            {
                var despesa = conexao.Find<Despesa>(transacao.DespesaId)
                    ?? throw new InvalidOperationException("O orcamento pessoal selecionado nao existe mais.");
                if (!string.Equals(
                        despesa.Categoria?.Trim(),
                        "PESSOAL",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("O gasto deve ser vinculado a uma despesa PESSOAL.");
                }

                var competenciaDespesa = NormalizarCompetencia(despesa.Vencimento);
                if (NormalizarCompetencia(transacao.Data) != competenciaDespesa)
                {
                    throw new InvalidOperationException(
                        "A data do gasto deve pertencer ao mesmo mes do orcamento selecionado.");
                }

                if (transacao.Id == 0)
                {
                    alteracoes = conexao.Insert(transacao);
                }
                else
                {
                    var existente = conexao.Find<Transacao>(transacao.Id)
                        ?? throw new InvalidOperationException("O gasto nao existe mais.");
                    if (existente.DespesaId != transacao.DespesaId)
                        throw new InvalidOperationException("Nao e possivel mover o gasto para outro orcamento.");
                    alteracoes = conexao.Update(transacao);
                }

                var inicio = NormalizarCompetencia(despesa.Vencimento);
                var fim = inicio.AddMonths(1);
                var totalRealizado = conexao.Table<Transacao>()
                    .Where(item =>
                        item.DespesaId == despesa.Id &&
                        item.Data >= inicio &&
                        item.Data < fim)
                    .ToList()
                    .Sum(item => item.Valor);
                if (totalRealizado > despesa.Valor)
                    despesa.Valor = totalRealizado;

                despesa.Sttatus = totalRealizado == despesa.Valor
                    ? true
                    : totalRealizado > 0 ? false : null;
                conexao.Update(despesa);
            });

            return alteracoes;
        }

        private static Receita AssociarReceitaImportada(
            SQLiteConnection conexao,
            Receita receita,
            ReceitaFixa modelo,
            DateTime competencia,
            ImportacaoFixosResultado resultado)
        {
            var outroLancamento = conexao.Table<Receita>().FirstOrDefault(item =>
                item.Id != receita.Id &&
                item.ReceitaFixaId == modelo.Id &&
                item.CompetenciaGeracao == competencia);

            receita.ReceitaFixaId = modelo.Id;
            receita.CompetenciaGeracao = outroLancamento is null ? competencia : null;
            conexao.Update(receita);
            if (outroLancamento is not null)
                resultado.Ignorados++;

            return outroLancamento ?? receita;
        }

        private static Receita ObterOuCriarReceitaMensal(
            SQLiteConnection conexao,
            ReceitaFixa modelo,
            DateTime competencia)
        {
            var existente = conexao.Table<Receita>().FirstOrDefault(item =>
                item.ReceitaFixaId == modelo.Id &&
                item.CompetenciaGeracao == competencia);
            if (existente is not null)
                return existente;

            var receita = new Receita
            {
                MesReferencia = competencia,
                FontePagadora = modelo.FontePagadora ?? string.Empty,
                Descricao = modelo.Descricao ?? string.Empty,
                Tipo = modelo.Tipo ?? string.Empty,
                Valor = modelo.Valor,
                ReceitaFixaId = modelo.Id,
                CompetenciaGeracao = competencia
            };
            conexao.Insert(receita);
            return receita;
        }

        private static void AssociarDespesaImportada(
            SQLiteConnection conexao,
            Despesa despesa,
            DespesaFixa modelo,
            DateTime competencia,
            ImportacaoFixosResultado resultado)
        {
            var outroLancamento = conexao.Table<Despesa>().FirstOrDefault(item =>
                item.Id != despesa.Id &&
                item.DespesaFixaId == modelo.Id &&
                item.CompetenciaGeracao == competencia);

            despesa.DespesaFixaId = modelo.Id;
            despesa.CompetenciaGeracao = outroLancamento is null ? competencia : null;
            conexao.Update(despesa);
            if (outroLancamento is not null)
                resultado.Ignorados++;
        }

        private static void RegistrarDecisao(
            SQLiteConnection conexao,
            string tipoModelo,
            int modeloId,
            DateTime competencia,
            string estado)
        {
            var decisao = conexao.Table<PreparacaoMensalItem>().FirstOrDefault(item =>
                item.TipoModelo == tipoModelo &&
                item.ModeloId == modeloId &&
                item.Competencia == competencia);

            if (decisao is null)
            {
                decisao = new PreparacaoMensalItem
                {
                    TipoModelo = tipoModelo,
                    ModeloId = modeloId,
                    Competencia = competencia,
                    Estado = estado,
                    AtualizadoEm = DateTime.Now
                };
                conexao.Insert(decisao);
                return;
            }

            decisao.Estado = estado;
            decisao.AtualizadoEm = DateTime.Now;
            conexao.Update(decisao);
        }

        private static string ChaveModelo(string tipoModelo, int modeloId)
        {
            return $"{tipoModelo?.Trim().ToUpperInvariant()}:{modeloId}";
        }

        private static DateTime NormalizarCompetencia(DateTime data)
        {
            return new DateTime(data.Year, data.Month, 1);
        }

        private static void ValidarReceitaFixa(ReceitaFixa receita)
        {
            if (string.IsNullOrWhiteSpace(receita.FontePagadora))
                throw new ArgumentException("Informe a fonte pagadora.", nameof(receita));
            ValidarValor(receita.Valor, "O valor da receita deve ser maior que zero.");
        }

        private static void ValidarDespesaFixa(DespesaFixa despesa)
        {
            if (despesa.ReceitaFixaId <= 0)
                throw new ArgumentException("Selecione uma receita fixa.", nameof(despesa));
            if (string.IsNullOrWhiteSpace(despesa.Descricao))
                throw new ArgumentException("Informe a descricao da despesa.", nameof(despesa));
            if (string.IsNullOrWhiteSpace(despesa.Categoria))
                throw new ArgumentException("Informe a categoria da despesa.", nameof(despesa));
            ValidarValor(despesa.Valor, "O valor da despesa deve ser maior que zero.");
            ValidarDiaVencimento(despesa.DiaVencimento);
        }

        private static void ValidarValor(decimal valor, string mensagem)
        {
            if (valor <= 0)
                throw new ArgumentException(mensagem);
        }

        private static void ValidarDiaVencimento(int dia)
        {
            if (dia is < 1 or > 31)
                throw new ArgumentOutOfRangeException(nameof(dia), "O dia de vencimento deve estar entre 1 e 31.");
        }
    }
}
