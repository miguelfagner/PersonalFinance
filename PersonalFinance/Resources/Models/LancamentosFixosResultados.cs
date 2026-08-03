namespace PersonalFinance.Resources.Models
{
    public class ImportacaoFixosResultado
    {
        public int ReceitasCriadas { get; set; }
        public int ReceitasReutilizadas { get; set; }
        public int DespesasCriadas { get; set; }
        public int DespesasReutilizadas { get; set; }
        public int Ignorados { get; set; }
    }

    public class PreparacaoMensalResultado
    {
        public DateTime Competencia { get; set; }
        public int ReceitasCriadas { get; set; }
        public int ReceitasExistentes { get; set; }
        public int DespesasCriadas { get; set; }
        public int DespesasExistentes { get; set; }
        public int Ignorados { get; set; }
    }

    public class ResumoPessoal
    {
        public DateTime Competencia { get; set; }
        public decimal Orcamento { get; set; }
        public decimal Realizado { get; set; }
        public decimal Saldo => Orcamento - Realizado;
        public int QuantidadeDespesas { get; set; }
    }
}
