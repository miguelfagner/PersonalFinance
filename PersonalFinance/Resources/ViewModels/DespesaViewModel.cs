using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PersonalFinance.Resources.ViewModels
{
    public class DespesaViewModel : INotifyPropertyChanged
    {
        private int _receitaId;
        private DateTime _dataCadastro = DateTime.Now;
        private DateTime _vencimento = DateTime.Now;
        private int _nParcela;
        private string _descricao;
        private string _categoria;
        private decimal _valor;

        private readonly DatabaseService _db;

        public int ReceitaId
        {
            get => _receitaId;
            set { _receitaId = value; OnPropertyChanged(); }
        }

        public DateTime DataCadastro
        {
            get => _dataCadastro;
            set { _dataCadastro = value; OnPropertyChanged(); }
        }

        public DateTime Vencimento
        {
            get => _vencimento;
            set { _vencimento = value; OnPropertyChanged(); }
        }

        public int NParcela
        {
            get => _nParcela;
            set { _nParcela = value; OnPropertyChanged(); }
        }

        public string Descricao
        {
            get => _descricao;
            set { _descricao = value; OnPropertyChanged(); }
        }

        public string Categoria
        {
            get => _categoria;
            set { _categoria = value; OnPropertyChanged(); }
        }

        public decimal Valor
        {
            get => _valor;
            set { _valor = value; OnPropertyChanged(); }
        }

        public DespesaViewModel()
        {
            _db = new DatabaseService();
        }

        public async Task<bool> SalvarDespesa()
        {
            if (ReceitaId <= 0)
                return false;

            var despesa = new Despesa
            {
                ReceitaId = this.ReceitaId,
                DataCadastro = this.DataCadastro,
                Vencimento = this.Vencimento,
                NParcela = this.NParcela,
                Descricao = this.Descricao,
                Categoria = this.Categoria,
                Valor = this.Valor
            };

            await _db.SalvarDespesaAsync(despesa);

            ReceitaId = 0;
            DataCadastro = DateTime.Now;
            Vencimento = DateTime.Now;
            NParcela = 0;
            Descricao = string.Empty;
            Categoria = string.Empty;
            Valor = 0;

            return true;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
