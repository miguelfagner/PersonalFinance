using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PersonalFinance.Resources.ViewModels
{
    public class ReceitaListViewModel : INotifyPropertyChanged
    {
        private readonly FinanceRepository _db;
        private ObservableCollection<Receita> _receitas;

        public ObservableCollection<Receita> Receitas
        {
            get => _receitas;
            set { _receitas = value; OnPropertyChanged(); }
        }

        public ReceitaListViewModel(FinanceRepository db)
        {
            _db = db;
            _receitas = new ObservableCollection<Receita>();
        }

        public async Task CarregarReceitasAsync()
        {
            var mesRef = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var dtInicio = new DateTime(mesRef.Year, mesRef.Month, 1);
            var dtFinal = dtInicio.AddMonths(1);

            var lista = await _db.ListaReceitasAsync(dtInicio, dtFinal);
           
            Receitas.Clear();
            foreach (var item in lista)
                Receitas.Add(item);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
