using SQLite;
using System.ComponentModel.DataAnnotations.Schema;

namespace PersonalFinance.Resources.Models
{
    public class Transacao
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        
        //[ForeignKey(nameof(Banco))]
        //public int BancoId { get; set; } // Referência ao banco

        //[Ignore]
        //public Banco Banco { get; set; }


        [ForeignKey(nameof(Despesa))]
        public int DespesaId { get; set; } // Referência ao banco

        [Ignore]
        public Despesa Despesa { get; set; }


        //[ForeignKey(nameof(Receita))]
        //public int ReceitaId { get; set; } // Referência ao banco

        //[Ignore]
        //public Receita Receita { get; set; }

        [MaxLength(100)]
        public string Observacao { get; set; }
        
        public decimal Valor { get; set; }

        public DateTime Data { get; set; }

    }
}
