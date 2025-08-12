using SQLite;

namespace PersonalFinance.Resources.Models
{
    public class Transacao
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Tipo { get; set; } // Receita ou Despesa
        public string Descricao { get; set; }
        public decimal Valor { get; set; }
        public DateTime Data { get; set; }
        public bool Pago { get; set; }
    }
}
