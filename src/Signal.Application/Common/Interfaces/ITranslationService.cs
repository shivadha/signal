namespace Signal.Application.Common.Interfaces;

public interface ITranslationService
{
    Task<string> TranslateToEnglishAsync(string text, string? sourceLanguage = null, CancellationToken cancellationToken = default);
    bool NeedsTranslation(string text);
}
