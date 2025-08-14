using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;

namespace PersonalFinance.Resources.ViewModels
{
    public class ReceitaViewModel : INotifyPropertyChanged
    {
        private DateTime _mesReferencia;
        private string _fontePagadora;
        private string _descricao;
        private string _tipo;
        private decimal _valor;
        private readonly DatabaseService _db;

        public DateTime MesReferencia
        {
            get => _mesReferencia;
            set { _mesReferencia = value; OnPropertyChanged(); }
        }

        public string FontePagadora
        {
            get => _fontePagadora;
            set { _fontePagadora = value; OnPropertyChanged(); }
        }

        public string Descricao
        {
            get => _descricao;
            set { _descricao = value; OnPropertyChanged(); }
        }

        public string Tipo
        {
            get => _tipo;
            set { _tipo = value; OnPropertyChanged(); }
        }

        public decimal Valor
        {
            get => _valor;
            set { _valor = value; OnPropertyChanged(); }
        }

        public ReceitaViewModel()
        {
            _db = new DatabaseService();
            // Padrão: mês atual, dia 1
            MesReferencia = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        }

        public async Task<bool> SalvarReceita()
        {
            if (string.IsNullOrWhiteSpace(FontePagadora))
                return false;

            var receita = new Receita
            {
                MesReferencia = this.MesReferencia == default
                    ? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
                    : this.MesReferencia,
                FontePagadora = this.FontePagadora,
                Descricao = this.Descricao,
                Tipo = this.Tipo,
                Valor = this.Valor
            };

            await _db.SalvarReceitaAsync(receita);

            // Limpa propriedades
            MesReferencia = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            FontePagadora = string.Empty;
            Descricao = string.Empty;
            Tipo = string.Empty;
            Valor = 0;

            return true;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
