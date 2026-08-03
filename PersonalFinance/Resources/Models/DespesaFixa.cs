using SQLite;

namespace PersonalFinance.Resources.Models
{
    public class DespesaFixa
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int ReceitaFixaId { get; set; }

        [Ignore]
        public ReceitaFixa? ReceitaFixa { get; set; }

        [MaxLength(500)]
        public string Descricao { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Categoria { get; set; } = string.Empty;

        public decimal Valor { get; set; }

        public int DiaVencimento { get; set; } = 1;

        public bool Ativa { get; set; } = true;
    }
}
