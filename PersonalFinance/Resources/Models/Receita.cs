using SQLite;

namespace PersonalFinance.Resources.Models
{
    public class Receita
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public DateTime MesReferencia { get; set; } = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 01);

        [MaxLength(100)]
        public string FontePagadora { get; set; }

        [MaxLength(500)]
        public string Descricao { get; set; }

        public string Tipo { get; set; }

        public decimal Valor { get; set; }
    }
}
