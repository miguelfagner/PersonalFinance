using SQLite;

namespace PersonalFinance.Resources.Models
{
    [Table("Receitas")]

    public class Receita
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [MaxLength(100)]
        public string FontePagadora { get; set; } 

        [MaxLength(500)]
        public string Descricao { get; set; } 

        public string Tipo { get; set; }

        //// Propriedade de navegação (não mapeada pelo SQLite-net)
        //[Ignore]
        //public List<Transacao> Transacoes { get; set; }
    }
}
