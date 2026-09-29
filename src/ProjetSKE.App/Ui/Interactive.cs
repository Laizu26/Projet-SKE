namespace ProjetSKE.App.Ui;

/// <summary>
/// Retour visuel de tous les éléments cliquables (boutons, cartes, pastilles...) : légèrement doré au survol
/// de la souris, entièrement doré pendant l'appui (clic ou toucher). Branché une fois dans <see cref="UiKit.Btn"/>
/// et <see cref="UiKit.OnTap{T}"/> : tout nouvel élément cliquable en profite, sur téléphone comme sur PC.
/// </summary>
public static class Interactive
{
    private static readonly Color HoverGold = Color.FromArgb("#EAB308");
    private static readonly Color PressGold = Color.FromArgb("#EAB308");
    private static readonly Color PressText = Color.FromArgb("#1C1917");

    /// <summary>Couleurs « au repos », pour revenir en arrière après le survol ou l'appui.</summary>
    private sealed class Rest
    {
        public Color? Background;
        public Color? Border;
        public Color? Text;
        public bool Hovered;
        public bool Pressed;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<VisualElement, Rest> States = new();

    /// <summary>Doré léger : la couleur d'origine mélangée à un peu d'or (ou un voile doré sur un fond transparent).</summary>
    private static Color Tint(Color? baseColor, float amount)
    {
        if (baseColor is null || baseColor.Alpha < 0.05f) return HoverGold.WithAlpha(amount);
        return Color.FromRgba(
            baseColor.Red + (HoverGold.Red - baseColor.Red) * amount,
            baseColor.Green + (HoverGold.Green - baseColor.Green) * amount,
            baseColor.Blue + (HoverGold.Blue - baseColor.Blue) * amount,
            Math.Max(baseColor.Alpha, amount));
    }

    public static void Attach(Button button)
    {
        var rest = States.GetValue(button, _ => new Rest());
        void Save()
        {
            if (rest.Hovered || rest.Pressed) return;
            rest.Background = button.BackgroundColor;
            rest.Border = button.BorderColor;
            rest.Text = button.TextColor;
        }
        void Apply()
        {
            if (!button.IsEnabled) return;
            if (rest.Pressed)
            {
                button.BackgroundColor = PressGold;
                button.BorderColor = PressGold;
                button.TextColor = PressText;
            }
            else if (rest.Hovered)
            {
                button.BackgroundColor = Tint(rest.Background, 0.22f);
                button.BorderColor = HoverGold;
                button.TextColor = rest.Text;
            }
            else
            {
                button.BackgroundColor = rest.Background;
                button.BorderColor = rest.Border;
                button.TextColor = rest.Text;
            }
        }

#if WINDOWS
        // Survol : seulement avec une souris (PC). Sur téléphone, un détecteur de pointeur sur un bouton
        // capterait les touchers et empêcherait le clic.
        var pointer = new PointerGestureRecognizer();
        pointer.PointerEntered += (_, _) => { Save(); rest.Hovered = true; Apply(); };
        pointer.PointerExited += (_, _) => { rest.Hovered = false; Apply(); };
        button.GestureRecognizers.Add(pointer);
#endif
        // Appui (souris ou doigt) : événements natifs du bouton, sans rien intercepter.
        button.Pressed += (_, _) => { Save(); rest.Pressed = true; Apply(); };
        button.Released += (_, _) => { rest.Pressed = false; Apply(); };
    }

    /// <summary>Élément cliquable quelconque (carte, pastille, tuile...).</summary>
    public static void Attach(View view)
    {
        if (view is Button button)
        {
            Attach(button);
            return;
        }
        var rest = States.GetValue(view, _ => new Rest());
        var border = view as Border;
        void Save()
        {
            if (rest.Hovered || rest.Pressed) return;
            rest.Background = view.BackgroundColor;
            rest.Border = border?.Stroke is SolidColorBrush s ? s.Color : null;
        }
        void Apply()
        {
            if (!view.IsEnabled) return;
            if (rest.Pressed)
            {
                view.BackgroundColor = Tint(rest.Background, 0.85f);
                if (border is not null) border.Stroke = PressGold;
            }
            else if (rest.Hovered)
            {
                view.BackgroundColor = Tint(rest.Background, 0.16f);
                if (border is not null) border.Stroke = HoverGold;
            }
            else
            {
                view.BackgroundColor = rest.Background;
                if (border is not null && rest.Border is not null) border.Stroke = rest.Border;
            }
        }

#if WINDOWS
        var pointer = new PointerGestureRecognizer();
        pointer.PointerEntered += (_, _) => { Save(); rest.Hovered = true; Apply(); };
        pointer.PointerExited += (_, _) => { rest.Hovered = false; rest.Pressed = false; Apply(); };
        pointer.PointerPressed += (_, _) => { Save(); rest.Pressed = true; Apply(); };
        pointer.PointerReleased += (_, _) => { rest.Pressed = false; Apply(); };
        view.GestureRecognizers.Add(pointer);
#endif
    }

    /// <summary>
    /// Survol de la souris (PC) : <paramref name="show"/> reçoit true en entrant (si <paramref name="wanted"/>), false en sortant.
    /// Sur téléphone il n'y a pas de survol : l'écran concerné prévoit un équivalent au toucher.
    /// </summary>
    public static void AttachHover(View view, Func<bool> wanted, Action<bool> show)
    {
#if WINDOWS
        var pointer = new PointerGestureRecognizer();
        pointer.PointerEntered += (_, _) => { if (wanted()) show(true); };
        pointer.PointerExited += (_, _) => show(false);
        view.GestureRecognizers.Add(pointer);
#endif
    }

    /// <summary>
    /// Éclat doré bref au toucher d'un élément cliquable (appelé par <see cref="UiKit.OnTap{T}"/> avant l'action).
    /// Sur téléphone, c'est le seul retour visuel (pas de survol) ; sur PC l'appui est déjà doré.
    /// </summary>
    public static async void Flash(View view)
    {
#if !WINDOWS
        if (view is Button) return; // les boutons ont déjà leur appui doré
        var before = view.BackgroundColor;
        var border = view as Border;
        var stroke = border?.Stroke;
        try
        {
            view.BackgroundColor = Tint(before, 0.85f);
            if (border is not null) border.Stroke = PressGold;
            await Task.Delay(140);
        }
        catch (Exception) { }
        view.BackgroundColor = before;
        if (border is not null) border.Stroke = stroke;
#else
        await Task.CompletedTask;
#endif
    }
}
