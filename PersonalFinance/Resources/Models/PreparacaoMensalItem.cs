using SQLite;

namespace PersonalFinance.Resources.Models
{
    public static class TiposModeloFixo
    {
        public const string Receita = "RECEITA";
        public const string Despesa = "DESPESA";
    }

    public static class EstadosPreparacaoMensal
    {
        public const string Gerado = "GERADO";
        public const string Ignorado = "IGNORADO";
    }

    /// <summary>
    /// Registra a decisao tomada para um modelo em uma competencia e tambem e usado
    /// como DTO editavel na tela de revisao. Os campos marcados com Ignore sao
    /// preenchidos a partir do cadastro mestre e nao fazem parte do historico.
    /// </summary>
    public class PreparacaoMensalItem
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed(Name = "UX_Preparacao_Modelo_Competencia", Order = 1, Unique = true)]
        [MaxLength(20)]
        public string TipoModelo { get; set; } = string.Empty;

        [Indexed(Name = "UX_Preparacao_Modelo_Competencia", Order = 2, Unique = true)]
        public int ModeloId { get; set; }

        [Indexed(Name = "UX_Preparacao_Modelo_Competencia", Order = 3, Unique = true)]
        public DateTime Competencia { get; set; }

        [MaxLength(20)]
        public string Estado { get; set; } = string.Empty;

        public DateTime AtualizadoEm { get; set; }

        [Ignore]
        public int? ReceitaFixaId { get; set; }

        [Ignore]
        public string FontePagadora { get; set; } = string.Empty;

        [Ignore]
        public string Descricao { get; set; } = string.Empty;

        [Ignore]
        public string Tipo { get; set; } = string.Empty;

        [Ignore]
        public string Categoria { get; set; } = string.Empty;

        [Ignore]
        public decimal Valor { get; set; }

        [Ignore]
        public int DiaVencimento { get; set; }

        [Ignore]
        public bool Selecionado { get; set; } = true;

        [Ignore]
        public bool JaLancado { get; set; }

        [Ignore]
        public bool Ignorado { get; set; }

        [Ignore]
        public bool AtualizacaoPendente { get; set; }

        [Ignore]
        public string Motivo { get; set; } = string.Empty;
    }
}
