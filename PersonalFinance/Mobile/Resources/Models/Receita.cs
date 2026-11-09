using SQLite;

namespace PersonalFinance.Resources.Models
{
    public class Receita
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public Guid ClientId { get; set; } = Guid.NewGuid();
        [Ignore] public long? LoadedRevision { get; set; }

        public DateTime MesReferencia { get; set; } = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 01);

        [MaxLength(100)]
        public string FontePagadora { get; set; }

        [MaxLength(500)]
        public string Descricao { get; set; }

        public string Tipo { get; set; }

        public decimal Valor { get; set; }

        [Indexed(Name = "UX_Receita_Modelo_Competencia", Order = 1, Unique = true)]
        public int? ReceitaFixaId { get; set; }

        [Indexed(Name = "UX_Receita_Modelo_Competencia", Order = 2, Unique = true)]
        public DateTime? CompetenciaGeracao { get; set; }
    }
}
