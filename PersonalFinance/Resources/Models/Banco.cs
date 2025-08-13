using SQLite;

namespace PersonalFinance.Resources.Models
{
    [Table("Bancos")]

    public class Banco
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [MaxLength(100)]
        public string Nome { get; set; } 
        public string Tipo { get; set; }

        // Propriedade de navegação (não mapeada pelo SQLite-net)
        [Ignore]
        public List<Transacao> Transacoes { get; set; }
    }
}
