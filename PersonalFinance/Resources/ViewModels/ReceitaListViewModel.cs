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
            var lista = await _db.GetReceitasAsync();
           
            Receitas.Clear();
            foreach (var item in lista)
                Receitas.Add(item);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
