using ProjetSKE.App.Ui;
using ProjetSKE.Core.Data;
using ProjetSKE.Core.Models;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Dev;

/// <summary>Listes des réglages du monde (variables, banque d'images).</summary>
public static class WorldLists
{
    private static GameContent C => DevState.Draft;

    public static Page VariableList() => new EntityListPage<VariableDef>(
        "Variables", C.Variables, x => x.Id, x => x.Name,
        (id, name) => new VariableDef { Id = id, Name = name },
        x => new VariableEditor(x),
        subtitle: x => $"départ {x.Initial}" + (x.Visible ? " · affichée" : ""),
        help: "Valeurs libres du scénario (réputation, dette, nombre de loups tués...). Les effets les modifient, les conditions les testent, %var:id% les affiche.");

    public static Page ImageList() => new EntityListPage<PortraitDef>(
        "Banque d'images", C.Portraits, x => x.Id, x => x.Name,
        (id, name) => new PortraitDef { Id = id, Name = name },
        x => new ImageEditor(x),
        subtitle: x => x.Url.Length > 0 ? (x.Aspect > 0 ? "cadrée" : "à mesurer") : "sans lien",
        help: "Images par lien (https) : rien n'est stocké, seule l'adresse est enregistrée. Chaque image se cadre une fois, puis sert de portrait aux PJ, PNJ, monstres et répliques.");
}

// ====================================================================== Monde et vocabulaire

public sealed class WorldEditor : EditorPage
{
    public WorldEditor() => Render();
    protected override string PageTitle => "Monde et textes";
    protected override void GoBack() => SkeApp.GoTo(new DevHomePage());

    protected override void Build(Form f)
    {
        var w = DevState.Draft.World;
        f.TextField("Titre du jeu", DevState.Draft.Title, v => DevState.Draft.Title = v);
        f.TextField("Nom du pays (carte, %pays%)", w.CountryName, v => w.CountryName = v);
        f.BoolField("Demander quel PJ parle aux PNJ", w.AskSpeaker, v => w.AskSpeaker = v);
        f.Note("Tous les textes de l'interface se renomment ici. Laisser vide = texte par défaut.");
        foreach (var group in Vocabulary.Entries.GroupBy(e => e.Group))
        {
            f.Header(group.Key);
            foreach (var entry in group)
            {
                var key = entry.Key;
                var view = f.TextField($"{entry.Default}  ·  {key}", w.Texts.GetValueOrDefault(key, ""), v =>
                {
                    if (string.IsNullOrWhiteSpace(v)) w.Texts.Remove(key);
                    else w.Texts[key] = v;
                });
            }
        }
    }
}

// ====================================================================== Temps

public sealed class TimeEditor : EditorPage
{
    public TimeEditor() => Render();
    protected override string PageTitle => "Temps et calendrier";
    protected override void GoBack() => SkeApp.GoTo(new DevHomePage());

    protected override void Build(Form f)
    {
        var t = DevState.Draft.Time;
        f.BoolField("Le temps s'écoule (horloge affichée)", t.Enabled, v => t.Enabled = v, rerender: true);
        if (!t.Enabled) return;

        f.Header("Départ");
        f.IntField("Jour de départ (1 = premier jour)", t.StartDay, v => t.StartDay = v);
        f.IntField("Heure de départ", t.StartHour, v => t.StartHour = v);
        f.IntField("Année de départ", t.StartYear, v => t.StartYear = v);
        f.TextField("Mot pour l'année (ex : An, Ère)", t.YearLabel, v => t.YearLabel = v);

        f.Header("Calendrier");
        f.IntField("Heures par jour", t.HoursPerDay, v => t.HoursPerDay = v);
        f.IntField("Jours par mois", t.DaysPerMonth, v => t.DaysPerMonth = v);
        f.Lines("Jours de la semaine", t.WeekDays);
        f.Lines("Mois", t.Months);
        f.ObjectList("Moments de la journée", t.Periods, () => new DayPeriod("Nouveau", 12), (pf, p, _) =>
        {
            pf.TextField("Nom", p.Name, v => p.Name = v);
            pf.IntField("À partir de (heure)", p.FromHour, v => p.FromHour = v);
        }, "+ Moment");

        f.Header("Durées (minutes)");
        f.IntField("Voyage (par défaut, modifiable par lieu)", t.TravelMinutes, v => t.TravelMinutes = v);
        f.IntField("Explorer", t.ExploreMinutes, v => t.ExploreMinutes = v);
        f.IntField("Combat", t.BattleMinutes, v => t.BattleMinutes = v);
        f.IntField("Parler à un PNJ", t.TalkMinutes, v => t.TalkMinutes = v);
        f.IntField("Réveil après l'auberge (heure)", t.InnWakeHour, v => t.InnWakeHour = v);
    }
}

// ====================================================================== Karma et amitié

public sealed class ScaleEditor : EditorPage
{
    private readonly bool _karma;
    public ScaleEditor(bool karma) { _karma = karma; Render(); }
    private ScaleSettings S => _karma ? DevState.Draft.Karma : DevState.Draft.Friendship;
    protected override string PageTitle => _karma ? "Karma" : "Amitié";
    protected override void GoBack() => SkeApp.GoTo(new DevHomePage());

    protected override void Build(Form f)
    {
        var s = S;
        f.Note(_karma
            ? "Chaque PJ a son propre karma, qui évolue avec les effets « Karma : ajouter ». Les conditions « Karma » font réagir PNJ et dialogues."
            : "Amitié d'un PNJ (ou PJ) envers l'équipe ou envers un PJ précis. Elle évolue avec les effets « Amitié : ajouter ».");
        f.BoolField("Activé", s.Enabled, v => s.Enabled = v);
        f.TextField("Nom affiché", s.Name, v => s.Name = v);
        f.BoolField("Visible par le joueur", s.Visible, v => s.Visible = v);
        f.IntField(_karma ? "Valeur de départ (sauf PJ réglé à part)" : "Valeur de départ (sauf PNJ/PJ réglé à part)", s.Default, v => s.Default = v);
        f.IntField("Minimum", s.Min, v => s.Min = v);
        f.IntField("Maximum", s.Max, v => s.Max = v);
        f.Note("Paliers : le nom du plus haut palier atteint est affiché (ex : ≥ 50 → « Vertueux »).");
        f.ObjectList("Paliers", s.Tiers, () => new ScaleTier("Nouveau", 0), (tf, t, _) =>
        {
            tf.TextField("Nom", t.Name, v => t.Name = v);
            tf.IntField("À partir de", t.Min, v => t.Min = v);
        }, "+ Palier");
    }
}

// ====================================================================== Variables

public sealed class VariableEditor : EditorPage
{
    private readonly VariableDef _x;
    public VariableEditor(VariableDef x) { _x = x; Render(); }
    protected override string PageTitle => "Variable : " + _x.Name;
    protected override void GoBack() => SkeApp.GoTo(WorldLists.VariableList());
    protected override Action Delete => () => DevState.Draft.Variables.Remove(_x);

    protected override void Build(Form f)
    {
        f.Note($"Identifiant : {_x.Id} — dans un texte : %var:{_x.Id}%");
        f.TextField("Nom", _x.Name, v => _x.Name = v);
        f.TextField("Description", _x.Description, v => _x.Description = v, multiline: true);
        f.IntField("Valeur de départ", _x.Initial, v => _x.Initial = v);
        Form.OptionalInt(f, "Minimum", _x.Min, v => _x.Min = v, "aucun");
        Form.OptionalInt(f, "Maximum", _x.Max, v => _x.Max = v, "aucun");
        f.BoolField("Afficher au joueur (campement)", _x.Visible, v => _x.Visible = v);
    }
}

// ====================================================================== Banque d'images

public sealed class ImageEditor : EditorPage
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    private readonly PortraitDef _x;
    private readonly List<FramedImage> _previews = [];
    private string? _status;

    public ImageEditor(PortraitDef x) { _x = x; Render(); }
    protected override string PageTitle => "Image : " + _x.Name;
    protected override void GoBack() => SkeApp.GoTo(WorldLists.ImageList());
    protected override Action Delete => () => DevState.Draft.Portraits.Remove(_x);

    protected override void Build(Form f)
    {
        _previews.Clear();
        f.Note("Identifiant : " + _x.Id);
        f.TextField("Nom", _x.Name, v => _x.Name = v);
        f.TextField("Lien de l'image (https://...)", _x.Url, v => _x.Url = v.Trim());
        f.Add(Btn("Charger l'image et mesurer sa taille", async () => await Measure()));
        if (_status is not null) f.Note(_status);
        if (_x.Url.Length == 0) return;

        f.Header("Cadrage");
        f.Note("Glisser l'image dans le grand cadre pour choisir la partie visible ; le curseur règle le zoom. "
            + "Le cadrage s'adapte à toutes les formes (dialogue, combat, liste).");

        var big = Preview(170, 210);
        var drag = new PanGestureRecognizer();
        double startX = 0, startY = 0;
        drag.PanUpdated += (_, e) =>
        {
            if (e.StatusType == GestureStatus.Started) { startX = _x.FocusX; startY = _x.FocusY; }
            if (e.StatusType != GestureStatus.Running) return;
            // Glisser vers la droite montre la gauche de l'image : le point central recule.
            var place = FramedImage.Placement(_x, 170, 210);
            _x.FocusX = Math.Clamp(startX - e.TotalX / Math.Max(1, place.Width), 0, 1);
            _x.FocusY = Math.Clamp(startY - e.TotalY / Math.Max(1, place.Height), 0, 1);
            Refresh();
        };
        big.GestureRecognizers.Add(drag);

        f.Add(new HorizontalStackLayout
        {
            Spacing = 12,
            HorizontalOptions = LayoutOptions.Center,
            Children = { big, new VerticalStackLayout { Spacing = 12, Children = { Preview(64, 77), Preview(56, 56), Preview(110, 60) } } },
        });
        f.Add(SliderRow("Zoom", 1, 4, _x.Zoom, v => _x.Zoom = v));
        f.Add(SliderRow("Horizontal", 0, 1, _x.FocusX, v => _x.FocusX = v));
        f.Add(SliderRow("Vertical", 0, 1, _x.FocusY, v => _x.FocusY = v));
        f.Add(Btn("Recentrer", () => { _x.FocusX = 0.5; _x.FocusY = 0.35; _x.Zoom = 1; DevState.Touch(); Render(); }));
    }

    private Border Preview(double w, double h)
    {
        var image = new FramedImage(_x);
        _previews.Add(image);
        return new Border
        {
            WidthRequest = w,
            HeightRequest = h,
            BackgroundColor = Theme.Stone900,
            Stroke = Theme.Gold600,
            StrokeThickness = 1.5,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            Content = image,
        };
    }

    private View SliderRow(string label, double min, double max, double value, Action<double> set)
    {
        var slider = new Slider(min, max, Math.Clamp(value, min, max))
        {
            MinimumTrackColor = Theme.Gold600,
            MaximumTrackColor = Theme.Stone300,
            ThumbColor = Theme.Gold500,
        };
        slider.ValueChanged += (_, e) => { set(e.NewValue); Refresh(); };
        return new VerticalStackLayout { Spacing = 0, Children = { Muted(label), slider } };
    }

    private void Refresh()
    {
        DevState.Touch();
        foreach (var p in _previews) p.Update(_x);
    }

    /// <summary>Télécharge l'image une fois pour vérifier le lien et connaître ses proportions (rien n'est gardé).</summary>
    private async Task Measure()
    {
        if (!_x.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            _status = "Le lien doit commencer par https://";
            Render();
            return;
        }
        _status = "Chargement...";
        Render();
        try
        {
            var bytes = await Http.GetByteArrayAsync(_x.Url);
            if (ImageSize.Read(bytes) is { Width: > 0, Height: > 0 } size)
            {
                _x.Aspect = (double)size.Width / size.Height;
                DevState.Touch();
                _status = $"Image trouvée : {size.Width} × {size.Height}.";
            }
            else _status = "Ce lien ne donne pas une image lisible (PNG, JPEG, GIF ou WebP). Utiliser le lien direct de l'image.";
        }
        catch (Exception e)
        {
            _status = "Impossible de charger l'image : " + e.Message;
        }
        Render();
    }
}
