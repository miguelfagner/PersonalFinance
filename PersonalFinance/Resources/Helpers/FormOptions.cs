using Android.App;
using Android.Widget;

namespace PersonalFinance.Resources.Helpers
{
    internal static class FormOptions
    {
        internal static readonly string[] CategoriasDespesa =
        {
            "PESSOAL",
            "CASA",
            "GASTOS DOMESTICOS",
            "ALIMENTACAO",
            "TRANSPORTE",
            "SAUDE",
            "EDUCACAO",
            "LAZER",
            "SERVICOS",
            "OUTROS"
        };

        internal static ArrayAdapter<string> CreateSpinnerAdapter(Activity activity, IEnumerable<string> items)
        {
            var adapter = new ArrayAdapter<string>(
                activity,
                Android.Resource.Layout.SimpleSpinnerItem,
                items.ToList());

            adapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
            return adapter;
        }

        internal static List<string> WithCurrentOption(IEnumerable<string> options, string? currentValue)
        {
            var values = options.ToList();

            if (!string.IsNullOrWhiteSpace(currentValue) &&
                !values.Any(x => string.Equals(x, currentValue, StringComparison.OrdinalIgnoreCase)))
            {
                values.Add(currentValue.Trim().ToUpperInvariant());
            }

            return values;
        }
    }
}
