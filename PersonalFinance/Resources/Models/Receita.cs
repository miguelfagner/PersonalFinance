using SQLite;

namespace PersonalFinance.Resources.Models
{
    public class Receita
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [MaxLength(100)]
        public string FontePagadora { get; set; } 

        [MaxLength(500)]
        public string Descricao { get; set; } 

        public string Tipo { get; set; }

        public decimal Valor { get; set; }
    }
}
