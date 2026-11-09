using SQLite;

namespace PersonalFinance.Resources.Models
{
    public class ReceitaFixa
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public Guid ClientId { get; set; } = Guid.NewGuid();
        [Ignore] public long? LoadedRevision { get; set; }

        [MaxLength(100)]
        public string FontePagadora { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Descricao { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Tipo { get; set; } = string.Empty;

        public decimal Valor { get; set; }

        public bool Ativa { get; set; } = true;
    }
}
