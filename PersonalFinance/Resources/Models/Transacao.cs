using SQLite;
using System.ComponentModel.DataAnnotations.Schema;

namespace PersonalFinance.Resources.Models
{
    public class Transacao
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [ForeignKey(nameof(Banco))]
        public int BancoId { get; set; } // Referência ao banco

        [Ignore]
        public Banco Banco { get; set; }

        public string Tipo { get; set; } // Receita Despesa Cofre

        public string Categoria { get; set; } // Ex: Alimentação, Transporte, etc

        public string Descricao { get; set; }
        public string Origem { get; set; } // Elza Extra Beneficio governo...

        public decimal Valor { get; set; }

        public DateTime DataCadastro { get; set; }

        public bool Pago { get; set; }

        public DateTime DataPgmto { get; set; }

    }
}
