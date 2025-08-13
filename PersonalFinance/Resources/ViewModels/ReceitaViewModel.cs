using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;
using Android.Widget;

namespace PersonalFinance.Resources.ViewModels
{
    public class ReceitaViewModel : INotifyPropertyChanged
    {
        private string _fontePagadora;
        private string _descricao;
        private string _tipo;
        private decimal _valor;
        private readonly DatabaseService _db;

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
        }

        public async Task<bool> SalvarReceita()
        {
            if (string.IsNullOrWhiteSpace(FontePagadora))
                return false;

            var receita = new Receita
            {
                FontePagadora = this.FontePagadora,
                Descricao = this.Descricao,
                Tipo = this.Tipo,
                Valor = this.Valor
            };

            await _db.SalvarReceitaAsync(receita);

            FontePagadora = string.Empty;
            Descricao = string.Empty;
            Tipo = string.Empty;

            return true;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
