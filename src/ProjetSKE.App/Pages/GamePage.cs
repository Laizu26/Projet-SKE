using ProjetSKE.App.Ui;
using ProjetSKE.App.Views;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Pages;

public enum GameTab { Camp, Map, Quests, Encyclopedia, Shop, Journal, Menu }

/// <summary>
/// Écran principal : en-tête (lieu, or), contenu de l'onglet, barre d'onglets en bas.
/// Le combat et les dialogues s'affichent par-dessus tout, dans une couche dédiée.
/// </summary>
public sealed class GamePage : ContentPage
{
    public GameSession Session { get; }
    public int Slot { get; }

    // Mémoire de navigation des onglets (conservée entre deux rafraîchissements).
    public GameTab Tab { get; private set; } = GameTab.Map;
    public bool CampShowBag { get; set; }
    public int? SelectedCharacter { get; set; }
    public bool MapShowCountry { get; set; }
    public EncyclopediaCategory EncyclopediaCategory { get; set; } = EncyclopediaCategory.Characters;
    public bool ShopSelling { get; set; }
    public bool QuestsShowDone { get; set; }
    public bool MenuShowDevTools { get; set; }

    /// <summary>Partie de test lancée depuis le mode développeur : jamais sauvegardée.</summary>
    public bool IsTestGame => Slot < 0;

    private readonly Label _title;
    private readonly Label _subtitle;
    private readonly Label _message;
    private readonly Border _toast;
    private readonly View _testBadge;
    private readonly ContentView _body = new();
    private readonly Grid _tabBar = new() { ColumnSpacing = 0, Padding = new Thickness(4, 6, 4, 8), BackgroundColor = Theme.Surface };
    private readonly ContentView _overlay = new() { IsVisible = false, ZIndex = 10, BackgroundColor = Theme.Overlay };
    private string? _pendingMessage;
    private bool _saveDisabled;

    public GamePage(GameSession session, int slot, bool playIntro)
    {
        Session = session;
        Slot = slot;
        BackgroundColor = Theme.Bg;

        _title = Txt("", 20, Theme.Accent, bold: true);
        _title.CharacterSpacing = 1;
        _subtitle = Muted("", 13);
        _message = Txt("", 14, Theme.Good, bold: true);
        _toast = Card(_message, Theme.Surface2, Theme.Good.WithAlpha(0.6f), 14);
        _toast.Margin = new Thickness(14, 8, 14, 0);
        _testBadge = Badge("TEST", Theme.Danger);

        var header = new Grid
        {
            Background = Theme.Vertical(Theme.Surface2, Theme.BgTop),
            Padding = new Thickness(18, 14, 18, 12),
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
        };
        header.Add(new VerticalStackLayout { Spacing = 0, Children = { _title, _subtitle } }, 0, 0);
        header.Add(_testBadge, 1, 0);

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(new GridLength(1)),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(new GridLength(1)),
                new RowDefinition(GridLength.Auto),
            },
        };
        root.Add(header, 0, 0);
        root.Add(new BoxView { Color = Theme.Accent.WithAlpha(0.4f) }, 0, 1);
        root.Add(_toast, 0, 2);
        root.Add(_body, 0, 3);
        root.Add(new BoxView { Color = Theme.Stroke }, 0, 4);
        root.Add(_tabBar, 0, 5);
        root.Add(_overlay, 0, 0);
        Grid.SetRowSpan(_overlay, 6);
        Content = root;
        Background = Theme.PageBackground;

        Render();
        if (playIntro && session.Db.Start.IntroDialogueId is { } intro && session.Db.Dialogues.ContainsKey(intro)) ShowDialogue(intro);
    }

    // ------------------------------------------------------------------ Affichage

    private static readonly (GameTab Tab, string Icon, string Label)[] Tabs =
    [
        (GameTab.Camp, "🏕️", "Camp"),
        (GameTab.Map, "🗺️", "Carte"),
        (GameTab.Quests, "📜", "Quêtes"),
        (GameTab.Encyclopedia, "📖", "Savoir"),
        (GameTab.Shop, "🛒", "Shop"),
        (GameTab.Journal, "✒️", "Journal"),
        (GameTab.Menu, "⚙️", "Menu"),
    ];

    public void Render()
    {
        var loc = Session.CurrentLocation;
        if (Tab == GameTab.Shop && !Session.InCity) Tab = GameTab.Map;
        var current = Tabs.First(t => t.Tab == Tab);
        _title.Text = Tab == GameTab.Map ? $"{Theme.LocationStyle(loc.Type).Icon}  {loc.Name}" : $"{current.Icon}  {TabTitle(Tab)}";
        _subtitle.Text = Tab == GameTab.Map ? GameSession.LocationTypeName(loc.Type) : $"📍 {loc.Name}";
        _testBadge.IsVisible = IsTestGame;

        Session.UpdateQuests();
        if (Session.Notifications.Count > 0)
        {
            var notes = string.Join("\n", Session.Notifications);
            _pendingMessage = _pendingMessage is null ? notes : _pendingMessage + "\n" + notes;
            Session.Notifications.Clear();
        }
        _message.Text = _pendingMessage ?? "";
        _toast.IsVisible = _pendingMessage is not null;
        _pendingMessage = null;

        BuildTabBar();

        View view = Tab switch
        {
            GameTab.Camp => new CampView(this),
            GameTab.Quests => new QuestsView(this),
            GameTab.Encyclopedia => new EncyclopediaView(this),
            GameTab.Shop => new ShopView(this),
            GameTab.Journal => new JournalView(this),
            GameTab.Menu => new MenuView(this),
            _ => new MapView(this),
        };
        _body.Content = new ScrollView { Content = new ContentView { Content = view, Padding = new Thickness(14, 12, 14, 24) } };
    }

    private static string TabTitle(GameTab tab) => tab switch
    {
        GameTab.Camp => "Campement",
        GameTab.Quests => "Quêtes",
        GameTab.Encyclopedia => "Encyclopédie",
        GameTab.Shop => "Boutique",
        GameTab.Journal => "Journal",
        GameTab.Menu => "Menu",
        _ => "Carte",
    };

    private void BuildTabBar()
    {
        _tabBar.Children.Clear();
        _tabBar.ColumnDefinitions.Clear();
        for (var i = 0; i < Tabs.Length; i++)
        {
            var t = Tabs[i];
            var enabled = t.Tab != GameTab.Shop || Session.InCity;
            var selected = Tab == t.Tab;
            _tabBar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            var cell = new Grid
            {
                RowDefinitions = { new RowDefinition(new GridLength(3)), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) },
                RowSpacing = 2,
                Opacity = enabled ? 1 : 0.3,
                BackgroundColor = Colors.Transparent,
            };
            cell.Add(new BoxView { Color = selected ? Theme.Accent : Colors.Transparent, CornerRadius = 2, Margin = new Thickness(12, 0) }, 0, 0);
            cell.Add(new Label { Text = t.Icon, FontSize = selected ? 24 : 20, HorizontalTextAlignment = TextAlignment.Center }, 0, 1);
            cell.Add(new Label
            {
                Text = t.Label,
                FontSize = 10,
                FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None,
                TextColor = selected ? Theme.Accent : Theme.Muted,
                HorizontalTextAlignment = TextAlignment.Center,
            }, 0, 2);
            if (enabled) OnTap(cell, () => SwitchTab(t.Tab));
            _tabBar.Add(cell, i, 0);
        }
    }

    public void SwitchTab(GameTab tab)
    {
        Tab = tab;
        AutoSave();
        Render();
    }

    /// <summary>Affiche un message court sous l'en-tête au prochain rafraîchissement.</summary>
    public void Notify(string message) => _pendingMessage = message;

    public void AutoSave()
    {
        if (!_saveDisabled && !IsTestGame) SkeApp.Saves.Save(Slot, Session.State);
    }

    // ------------------------------------------------------------------ Couches par-dessus (histoire, combat)

    private void ShowOverlay(View view)
    {
        _overlay.Content = view;
        _overlay.IsVisible = true;
    }

    private void HideOverlay()
    {
        _overlay.IsVisible = false;
        _overlay.Content = null;
    }

    public bool OverlayVisible => _overlay.IsVisible;

    public void ShowDialogue(string dialogueId, Action? onEnd = null)
    {
        if (!Session.Db.Dialogues.ContainsKey(dialogueId))
        {
            onEnd?.Invoke();
            return;
        }
        var runner = Session.StartDialogue(dialogueId);
        ShowOverlay(new DialogueView(Session, runner, () =>
        {
            HideOverlay();
            AutoSave();
            Render();
            if (runner.PendingBattle is { } monsters) StartBattle(monsters);
            else onEnd?.Invoke();
        }));
    }

    public void StartBattle(IReadOnlyList<string> monsterIds, string? fixedBattleId = null)
    {
        var battle = Session.StartBattle(monsterIds, fixedBattleId);
        ShowOverlay(new BattleView(this, battle, () =>
        {
            HideOverlay();
            AutoSave();
            Render();
        }));
    }

    /// <summary>Parler à un PNJ (le dialogue dépend de l'avancement des quêtes).</summary>
    public void TalkTo(string npcId)
    {
        var dialogue = Session.Talk(npcId);
        if (dialogue is not null) ShowDialogue(dialogue);
        else Notify("Cette personne n'a rien à dire.");
        Render();
    }

    /// <summary>Combat fixe du lieu actuel, précédé de son dialogue d'introduction.</summary>
    public void StartFixedBattle(FixedBattleDef fb)
    {
        if (fb.IntroDialogueId is { } intro) ShowDialogue(intro, () => StartBattle(fb.MonsterIds, fb.Id));
        else StartBattle(fb.MonsterIds, fb.Id);
    }

    public void Travel(string destinationId)
    {
        var result = Session.Travel(destinationId);
        if (!result.Success)
        {
            Notify(result.Error ?? "Voyage impossible.");
            Render();
            return;
        }
        MapShowCountry = false;
        AutoSave();
        Render();

        if (result.DialogueId is { } dialogue && result.BattleMonsterIds is { } fixedMonsters)
            ShowDialogue(dialogue, () => StartBattle(fixedMonsters, result.FixedBattleId));
        else if (result.DialogueId is { } onlyDialogue)
            ShowDialogue(onlyDialogue);
        else if (result.BattleMonsterIds is { } monsters)
            StartBattle(monsters, result.FixedBattleId);
    }

    /// <summary>Game over : retour au titre sans sauvegarder (on reprendra à la dernière sauvegarde).</summary>
    public void GameOver()
    {
        _saveDisabled = true;
        SkeApp.GoTo(IsTestGame ? new Dev.DevHomePage() : (Page)new TitlePage());
    }

    /// <summary>Retour au titre en sauvegardant.</summary>
    public void QuitToTitle()
    {
        AutoSave();
        _saveDisabled = true;
        SkeApp.GoTo(IsTestGame ? new Dev.DevHomePage() : (Page)new TitlePage());
    }

    protected override bool OnBackButtonPressed()
    {
        // Le bouton retour d'Android ne quitte pas une partie par accident.
        if (!OverlayVisible && Tab != GameTab.Map) SwitchTab(GameTab.Map);
        return true;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        AutoSave();
    }
}
