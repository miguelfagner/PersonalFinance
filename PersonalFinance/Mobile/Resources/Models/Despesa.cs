using SQLite;
using System.ComponentModel.DataAnnotations.Schema;

namespace PersonalFinance.Resources.Models
{
    public class Despesa
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public Guid ClientId { get; set; } = Guid.NewGuid();
        [Ignore] public long? LoadedRevision { get; set; }

        [ForeignKey(nameof(Receita))]
        public int ReceitaId { get; set; } // Referência ao banco

        [Ignore]
        public Receita Receita { get; set; }

        public DateTime DataCadastro { get; set; }

        public DateTime Vencimento { get; set; }

        public int NParcela { get; set; }

        [MaxLength(500)]
        public string Descricao { get; set; }

        public string Categoria { get; set; } // Ex: Alimentação, Transporte, etc

        public decimal Valor { get; set; }

        public bool? Sttatus { get; set; } //true quitado, null pendente, false parcialmente quitado
        public bool replicavel { get; set; }

        [Indexed(Name = "UX_Despesa_Modelo_Competencia", Order = 1, Unique = true)]
        public int? DespesaFixaId { get; set; }

        [Indexed(Name = "UX_Despesa_Modelo_Competencia", Order = 2, Unique = true)]
        public DateTime? CompetenciaGeracao { get; set; }

    }
}
