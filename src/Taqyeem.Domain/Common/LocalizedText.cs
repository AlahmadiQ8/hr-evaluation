namespace Taqyeem.Domain.Common;

/// <summary>
/// Bilingual (English/Arabic) text. The domain is bilingual end-to-end, so most
/// human-readable names are stored as <see cref="LocalizedText"/>.
/// </summary>
public sealed record LocalizedText(string En, string Ar)
{
    /// <summary>Returns the Arabic value for the <c>ar</c> culture, otherwise English.</summary>
    public string For(string twoLetterIsoLanguageName) =>
        string.Equals(twoLetterIsoLanguageName, "ar", StringComparison.OrdinalIgnoreCase) ? Ar : En;

    public override string ToString() => En;
}
