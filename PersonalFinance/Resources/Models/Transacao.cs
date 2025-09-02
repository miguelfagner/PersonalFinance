using SQLite;
using System.ComponentModel.DataAnnotations.Schema;

namespace PersonalFinance.Resources.Models
{
    public class Transacao
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [ForeignKey(nameof(Despesa))]
        public int DespesaId { get; set; } // Referência ao banco

        [Ignore]
        public Despesa Despesa { get; set; }

        [MaxLength(100)]
        public string Observacao { get; set; }
        
        public decimal Valor { get; set; }

        public DateTime Data { get; set; }

    }

    public class TransacaoItem
    {
        public bool IsHeader { get; set; }
        public string HeaderTitle { get; set; }
        public Transacao Transacao { get; set; }
    }
}
