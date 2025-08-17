using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;
// Usado para permitir que o ViewModel avise quando alguma propriedade muda
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PersonalFinance.Resources.ViewModels
{
    public class DespesaViewModel : INotifyPropertyChanged
    {
        // Atributos privados (armazenam os valores das propriedades abaixo)
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
            set { _receitaId = value; OnPropertyChanged(); } // Atualiza o valor e notifica a interface
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
            _db = new DatabaseService(); // Inicializa o serviço do banco de dados
        }

        // Método assíncrono que salva a despesa no banco de dados
        public async Task<bool> SalvarDespesa()
        {
            // Se o ID da receita não for válido, não salva
            if (ReceitaId <= 0)
                return false;

            // Cria um novo objeto de despesa com os dados preenchidos na interface
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

            // Após salvar, limpa os campos (zera ou reinicia os valores)
            ReceitaId = 0;
            DataCadastro = DateTime.Now;
            Vencimento = DateTime.Now;
            NParcela = 0;
            Descricao = string.Empty;
            Categoria = string.Empty;
            Valor = 0;

            // Retorna true indicando que salvou com sucesso
            return true;
        }

        // Evento usado para notificar a interface quando alguma propriedade muda
        public event PropertyChangedEventHandler PropertyChanged;

        // Método que dispara o evento de notificação quando uma propriedade é alterada
        protected void OnPropertyChanged([CallerMemberName] string name = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
