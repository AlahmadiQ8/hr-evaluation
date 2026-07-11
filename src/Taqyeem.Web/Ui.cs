using System.Globalization;

namespace Taqyeem.Web;

/// <summary>Small display helpers shared by the Razor components.</summary>
public static class Ui
{
    /// <summary>Two-letter language of the current UI culture ("en" or "ar").</summary>
    public static string Lang => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    /// <summary>Bootstrap badge class for a rating band.</summary>
    public static string BandBadge(string? band) => band switch
    {
        "Outstanding" => "text-bg-success",
        "Exceeds" => "text-bg-primary",
        "Meets" => "text-bg-secondary",
        "PartiallyMeets" => "text-bg-warning",
        "Unsatisfactory" => "text-bg-danger",
        _ => "text-bg-light",
    };
}
