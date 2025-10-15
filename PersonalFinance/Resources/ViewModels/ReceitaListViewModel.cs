using PersonalFinance.Resources.Models;
using PersonalFinance.Resources.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PersonalFinance.Resources.ViewModels
{
    public class ReceitaListViewModel : INotifyPropertyChanged
    {
        private readonly DatabaseService _db;
        private ObservableCollection<Receita> _receitas;

        public ObservableCollection<Receita> Receitas
        {
            get => _receitas;
            set { _receitas = value; OnPropertyChanged(); }
        }

        public ReceitaListViewModel(DatabaseService db)
        {
            _db = db;
            _receitas = new ObservableCollection<Receita>();
        }

        public async Task CarregarReceitasAsync()
        {
            var mesRef = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var dtInicio = new DateTime(mesRef.Year, mesRef.Month, 1);
            var dtFinal = new DateTime(mesRef.Year, mesRef.Month, DateTime.DaysInMonth(mesRef.Year, mesRef.Month));

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
