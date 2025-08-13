using SQLite;
using System.ComponentModel.DataAnnotations.Schema;

namespace PersonalFinance.Resources.Models
{
    public class Despesa
    {

        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [ForeignKey(nameof(Receita))]
        public int ReceitaId { get; set; } // Referência ao banco

        [Ignore]
        public Receita Receita { get; set; }

        public DateTime DataCadastro { get; set; }

        public DateTime Vencimento { get; set; }

        public int NParcela { get; set; }

        [MaxLength(500)]
        public string Descricao { get; set; }

        public string Categoria { get; set; } // Ex: Alimentação, Transporte, etc

        public decimal Valor { get; set; }

    }
}
