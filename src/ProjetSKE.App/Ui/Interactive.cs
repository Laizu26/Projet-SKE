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
        Glow? glow = null;
        void Save()
        {
            if (rest.Hovered || rest.Pressed) return;
            glow = new Glow(view);
        }
        void Apply()
        {
            if (!view.IsEnabled || glow is null) return;
            if (rest.Pressed) glow.Show(0.85f, PressGold);
            else if (rest.Hovered) glow.Show(0.16f, HoverGold);
            else glow.Restore();
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
    /// Ce qui s'éclaire en doré quand on survole / touche un élément. Si l'élément est surtout une forme
    /// (case hexagonale de la carte, rond du camp, visage d'un perso), c'est cette forme qui s'éclaire : sinon le fond
    /// rectangulaire de l'élément apparaîtrait autour. Sinon (ligne de liste, carte), c'est le fond de l'élément.
    /// </summary>
    private sealed class Glow
    {
        private readonly VisualElement _target;
        private readonly Color? _background;
        private readonly Brush? _fill;
        private readonly Brush? _stroke;

        public Glow(View view)
        {
            _target = ShapeOf(view);
            _background = _target.BackgroundColor;
            _fill = (_target as Microsoft.Maui.Controls.Shapes.Shape)?.Fill;
            _stroke = _target switch
            {
                Border b => b.Stroke,
                Microsoft.Maui.Controls.Shapes.Shape s => s.Stroke,
                _ => null,
            };
        }

        public void Show(float amount, Color line)
        {
            switch (_target)
            {
                case Microsoft.Maui.Controls.Shapes.Shape shape:
                    shape.Fill = Tint(_fill is SolidColorBrush f ? f.Color : null, amount);
                    shape.Stroke = line;
                    break;
                case Border border:
                    border.BackgroundColor = Tint(_background, amount);
                    border.Stroke = line;
                    break;
                default:
                    _target.BackgroundColor = Tint(_background, amount);
                    break;
            }
        }

        public void Restore()
        {
            _target.BackgroundColor = _background;
            if (_target is Microsoft.Maui.Controls.Shapes.Shape shape) { shape.Fill = _fill; shape.Stroke = _stroke; }
            if (_target is Border border) border.Stroke = _stroke;
        }

        /// <summary>La forme qui fait l'essentiel de l'élément (au moins 60 % de sa largeur), sinon l'élément lui-même.</summary>
        private static VisualElement ShapeOf(View view)
        {
            if (view is Border) return view;
            var width = view.Width;
            foreach (var child in view.GetVisualTreeDescendants().OfType<VisualElement>())
            {
                if (child == view || child is not (Border or Microsoft.Maui.Controls.Shapes.Shape)) continue;
                if (width <= 0 || child.Width >= width * 0.6) return child;
                break; // la première forme est petite (icône d'une ligne) : on éclaire toute la ligne
            }
            return view;
        }
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
        var glow = new Glow(view);
        try
        {
            glow.Show(0.85f, PressGold);
            await Task.Delay(140);
        }
        catch (Exception) { }
        glow.Restore();
#else
        await Task.CompletedTask;
#endif
    }
}
