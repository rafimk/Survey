namespace SurveyApp.Services;

public enum ToastKind { Success, Error }

public sealed record ToastMessage(Guid Id, ToastKind Kind, string Text);

/// <summary>Scoped (per circuit) notifier; <c>ToastHost</c> subscribes and renders.</summary>
public sealed class ToastService
{
    public event Action<ToastMessage>? OnShow;

    public void Success(string text) => Show(ToastKind.Success, text);

    public void Error(string text) => Show(ToastKind.Error, text);

    private void Show(ToastKind kind, string text) =>
        OnShow?.Invoke(new ToastMessage(Guid.NewGuid(), kind, text));
}
