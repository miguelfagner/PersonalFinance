using System.Globalization;

namespace PersonalFinance.Resources.Helpers
{
    internal static class MoneyInputFormatter
    {
        private static readonly CultureInfo PtBr = new("pt-BR");

        internal static void Configure(EditText campo)
        {
            bool atualizando = false;

            campo.TextChanged += (s, e) =>
            {
                if (atualizando)
                    return;

                atualizando = true;
                campo.Text = Format(campo.Text);
                campo.SetSelection(campo.Text.Length);
                atualizando = false;
            };

            SetValue(campo, Parse(campo.Text));
        }

        internal static void SetValue(EditText campo, decimal valor)
        {
            campo.Text = Math.Max(0m, valor).ToString("N2", PtBr);
            campo.SetSelection(campo.Text.Length);
        }

        internal static decimal Parse(string? texto)
        {
            string digitos = string.Concat((texto ?? string.Empty)
                .Where(c => c >= '0' && c <= '9'));

            return decimal.TryParse(digitos, NumberStyles.None, CultureInfo.InvariantCulture, out decimal centavos)
                ? centavos / 100m
                : 0m;
        }

        private static string Format(string? texto)
        {
            return Parse(texto).ToString("N2", PtBr);
        }
    }
}
