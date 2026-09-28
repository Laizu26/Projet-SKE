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
    public bool CampShowPeople { get; set; }
    public string? SelectedCampMember { get; set; }
    public int? SelectedCharacter { get; set; }
    /// <summary>Carte : true = vue du royaume, false = vue du lieu actuel.</summary>
    public bool MapShowCountry { get; set; }
    public string? MapSelectedLocation { get; set; }
    public string? MapSelectedBuilding { get; set; }
    public Core.Systems.Hex MapPartyHex { get; set; }
    public EncyclopediaCategory EncyclopediaCategory { get; set; } = EncyclopediaCategory.Characters;
    public bool ShopSelling { get; set; }
    public bool QuestsShowDone { get; set; }
    public bool MenuShowDevTools { get; set; }

    /// <summary>Partie de test lancée depuis le mode développeur : jamais sauvegardée.</summary>
    public bool IsTestGame => Slot < 0;

    private readonly Label _title;
    private readonly Label _subtitle;
    private readonly Label _message;
    private readonly Label _clockTime;
    private readonly Label _clockDate;
    private readonly View _clock;
    private readonly Border _toast;
    private readonly View _testBadge;
    private readonly ContentView _avatarHost;
    private readonly ContentView _body = new();
    private readonly Grid _tabBar = new() { ColumnSpacing = 2, Padding = new Thickness(6, 6, 6, 10), BackgroundColor = Theme.Stone950 };
    private readonly ContentView _overlay = new() { IsVisible = false, ZIndex = 10, BackgroundColor = Theme.Overlay };
    private string? _pendingMessage;
    private bool _saveDisabled;

    public GamePage(GameSession session, int slot, bool playIntro)
    {
        Session = session;
        Slot = slot;
        BackgroundColor = Theme.Bg;

        _title = Txt("", 16, Theme.Stone100, bold: true);
        _subtitle = Caps("", 9, Theme.Stone500);
        _message = Txt("", 13, Theme.Stone100, bold: true);
        _toast = new Border
        {
            BackgroundColor = Theme.Stone900,
            Stroke = Theme.Gold600,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            Padding = new Thickness(14, 10),
            Margin = new Thickness(14, 10, 14, 0),
            Shadow = new Shadow { Brush = Colors.Black, Offset = new Point(0, 4), Radius = 10, Opacity = 0.3f },
            Content = IconRow(Icon(Ico.Bell, 18, Theme.Gold500), _message),
        };
        _testBadge = Badge("Test", Theme.Red500);
        _clockTime = new Label { FontFamily = "serif", FontSize = 17, FontAttributes = FontAttributes.Bold, TextColor = Theme.Gold500, HorizontalTextAlignment = TextAlignment.End };
        _clockDate = Caps("", 8, Theme.Stone500);
        _clockDate.HorizontalTextAlignment = TextAlignment.End;
        _clock = new VerticalStackLayout { Spacing = 0, VerticalOptions = LayoutOptions.Center, Children = { _clockTime, _clockDate } };
        _avatarHost = new ContentView();

        var header = new Grid
        {
            BackgroundColor = Theme.Stone900,
            Padding = new Thickness(16, 12),
            ColumnSpacing = 12,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
        };
        header.Add(_avatarHost, 0, 0);
        header.Add(new VerticalStackLayout { Spacing = 1, VerticalOptions = LayoutOptions.Center, Children = { _title, _subtitle } }, 1, 0);
        header.Add(new HorizontalStackLayout { Spacing = 8, VerticalOptions = LayoutOptions.Center, Children = { _testBadge, _clock } }, 2, 0);

        var root = new Grid
        {
            BackgroundColor = Theme.Parchment,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
            },
        };
        root.Add(header, 0, 0);
        root.Add(GoldLine(2), 0, 1);
        root.Add(_toast, 0, 2);
        root.Add(_body, 0, 3);
        root.Add(_tabBar, 0, 4);
        root.Add(_overlay, 0, 0);
        Grid.SetRowSpan(_overlay, 5);
        var eyes = BuildEyelids();
        root.Add(eyes, 0, 0);
        Grid.SetRowSpan(eyes, 5);
        Content = root;
        BackgroundColor = Theme.Stone900;

        Render();
        if (playIntro && session.Db.StartById(session.State.StartId).IntroDialogueId is { } intro && session.Db.Dialogues.ContainsKey(intro)) ShowDialogue(intro);
    }

    // ------------------------------------------------------------------ Réveil : les yeux s'ouvrent

    /// <summary>
    /// Deux paupières noires en amande qui s'entrouvrent, clignent, puis s'ouvrent en grand,
    /// avec un voile sombre qui se dissipe (vision encore floue au réveil).
    /// </summary>
    private static Grid BuildEyelids()
    {
        var eyes = new Grid
        {
            ZIndex = 20,
            InputTransparent = true,
            RowSpacing = 0,
            RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Star) },
        };
        var veil = new BoxView { Color = Theme.Stone950, Opacity = 0.85 };
        eyes.Add(veil, 0, 0);
        Grid.SetRowSpan(veil, 2);

        Border Lid(bool top) => new()
        {
            BackgroundColor = Colors.Black,
            StrokeThickness = 0,
            Margin = top ? new Thickness(-60, -2, -60, -40) : new Thickness(-60, -40, -60, -2),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
            {
                CornerRadius = top ? new CornerRadius(0, 0, 400, 400) : new CornerRadius(400, 400, 0, 0),
            },
        };
        var upper = Lid(top: true);
        var lower = Lid(top: false);
        eyes.Add(upper, 0, 0);
        eyes.Add(lower, 0, 1);

        var started = false;
        eyes.Loaded += async (_, _) =>
        {
            if (started) return;
            started = true;
            try
            {
                await Task.Delay(250);
                async Task Open(double fraction, uint ms, Easing easing)
                {
                    var h = eyes.Height / 2 + 60;
                    await Task.WhenAll(
                        upper.TranslateTo(0, -h * fraction, ms, easing),
                        lower.TranslateTo(0, h * fraction, ms, easing));
                }
                await Open(0.18, 450, Easing.SinOut);
                await Task.WhenAll(Open(0, 220, Easing.SinIn), veil.FadeTo(0.7, 220));
                await Task.Delay(180);
                await Task.WhenAll(Open(0.45, 500, Easing.SinOut), veil.FadeTo(0.45, 500));
                await Open(0.3, 250, Easing.SinInOut);
                await Task.WhenAll(Open(1, 800, Easing.CubicOut), veil.FadeTo(0, 1100, Easing.SinOut));
            }
            catch (Exception) { }
            eyes.IsVisible = false;
        };
        return eyes;
    }

    // ------------------------------------------------------------------ Affichage

    private static readonly (GameTab Tab, string Icon, string Key)[] Tabs =
    [
        (GameTab.Camp, Ico.Tent, "tab.camp"),
        (GameTab.Map, Ico.Map, "tab.map"),
        (GameTab.Quests, Ico.ScrollText, "tab.quests"),
        (GameTab.Encyclopedia, Ico.Library, "tab.encyclopedia"),
        (GameTab.Shop, Ico.Store, "tab.shop"),
        (GameTab.Journal, Ico.Feather, "tab.journal"),
        (GameTab.Menu, Ico.Settings, "tab.menu"),
    ];

    /// <summary>Texte de l'interface (renommable dans le mode développeur).</summary>
    public string T(string key) => Session.Db.T(key);

    public void Render()
    {
        var loc = Session.CurrentLocation;
        if (Tab == GameTab.Shop && !Session.InCity) Tab = GameTab.Map;
        if (Tab == GameTab.Quests && !Session.Db.Content.World.ShowQuestTab) Tab = GameTab.Map;
        var hero = Session.State.Party.FirstOrDefault(c => c.DefId == Session.State.HeroId) ?? Session.State.Party.FirstOrDefault();
        _avatarHost.Content = hero is null
            ? Emblem(Ico.Shield, 38)
            : Avatar(Session.DefOf(hero).Name, Theme.Gold600, 38);
        _title.Text = loc.Name;
        _subtitle.Text = $"{Session.LocationTypeLabel(loc.Type)} · {TabTitle(Tab)}".ToUpperInvariant();
        _testBadge.IsVisible = IsTestGame;
        var time = Session.Db.Content.Time;
        _clock.IsVisible = time.Enabled;
        if (time.Enabled)
        {
            var clock = Session.Clock;
            _clockTime.Text = clock.TimeText;
            _clockDate.Text = string.Join(" · ", new[] { clock.Period, clock.WeekDay.Length > 0 ? clock.WeekDay : $"Jour {clock.Day}" }.Where(x => x.Length > 0));
        }

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
        _body.Content = new ScrollView { Content = new ContentView { Content = view, Padding = new Thickness(14, 16, 14, 28) } };
    }

    private string TabTitle(GameTab tab) => tab switch
    {
        GameTab.Camp => T("title.camp"),
        GameTab.Quests => T("tab.quests"),
        GameTab.Encyclopedia => T("tab.encyclopedia"),
        GameTab.Shop => T("title.shop"),
        GameTab.Journal => T("tab.journal"),
        GameTab.Menu => T("tab.menu"),
        _ => T("title.map"),
    };

    private void BuildTabBar()
    {
        _tabBar.Children.Clear();
        _tabBar.ColumnDefinitions.Clear();
        var tabs = Tabs.Where(t => t.Tab != GameTab.Quests || Session.Db.Content.World.ShowQuestTab).ToArray();
        for (var i = 0; i < tabs.Length; i++)
        {
            var t = tabs[i];
            var label = T(t.Key);
            var enabled = t.Tab != GameTab.Shop || Session.InCity;
            var selected = Tab == t.Tab;
            _tabBar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            var cell = new Border
            {
                BackgroundColor = selected ? Theme.Stone800 : Colors.Transparent,
                Stroke = selected ? Theme.Stone700 : Colors.Transparent,
                StrokeThickness = 1,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                Padding = new Thickness(0, 7, 0, 6),
                Opacity = enabled ? 1 : 0.25,
                Content = new VerticalStackLayout
                {
                    Spacing = 3,
                    Children =
                    {
                        Icon(t.Icon, 20, selected ? Theme.Gold500 : Theme.Stone500),
                        new Label
                        {
                            Text = label.ToUpperInvariant(),
                            FontSize = label.Length > 8 ? 6.5 : 8,
                            LineBreakMode = LineBreakMode.NoWrap,
                            FontAttributes = FontAttributes.Bold,
                            CharacterSpacing = 1,
                            TextColor = selected ? Theme.Gold500 : Theme.Stone500,
                            HorizontalTextAlignment = TextAlignment.Center,
                        },
                    },
                },
            };
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
        // Le dialogue assombrit lui-même l'écran (en fondu) : la couche reste transparente.
        _overlay.BackgroundColor = view is DialogueView ? Colors.Transparent : Theme.Overlay;
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
            // Dialogue d'après-combat (combat fixe).
            var fb = fixedBattleId is null ? null
                : Session.Db.Content.Locations.Select(l => l.FixedBattle).FirstOrDefault(f => f?.Id == fixedBattleId);
            var after = battle.Outcome switch
            {
                BattleOutcome.Victory => fb?.VictoryDialogueId,
                BattleOutcome.Defeat => fb?.DefeatDialogueId,
                _ => null,
            };
            if (after is not null) ShowDialogue(after);
        }));
    }

    /// <summary>
    /// Parler à un PNJ. Si l'équipe compte plusieurs PJ, on choisit d'abord qui prend la parole :
    /// le dialogue (et la réaction du PNJ) peut en dépendre.
    /// </summary>
    public void TalkTo(string npcId)
    {
        if (Session.Db.Content.World.AskSpeaker && Session.State.Party.Count > 1)
        {
            ShowOverlay(SpeakerPicker(npcId));
            return;
        }
        TalkAs(npcId, null);
    }

    private void TalkAs(string npcId, string? speakerId)
    {
        HideOverlay();
        var dialogue = Session.Talk(npcId, speakerId);
        if (dialogue is not null) ShowDialogue(dialogue);
        else Notify("Cette personne n'a rien à dire.");
        Render();
    }

    private View SpeakerPicker(string npcId)
    {
        var npc = Session.Db.Npcs[npcId];
        var list = new VerticalStackLayout { Spacing = 8 };
        foreach (var c in Session.State.Party)
        {
            var def = Session.DefOf(c);
            var id = c.DefId;
            var info = new VerticalStackLayout { Spacing = 1, VerticalOptions = LayoutOptions.Center };
            info.Add(Txt(def.Name, 15, Theme.Stone100, bold: true));
            var details = new List<string>();
            if (def.Title.Length > 0) details.Add(def.Title);
            var karma = Session.Db.Content.Karma;
            if (karma.Enabled && karma.Visible) details.Add($"{karma.Name} {c.Karma} {karma.TierName(c.Karma)}".Trim());
            var friendship = Session.Db.Content.Friendship;
            if (friendship.Enabled && friendship.Visible)
            {
                var f = Session.GetFriendship(npcId, id);
                details.Add($"{friendship.Name} {f} {friendship.TierName(f)}".Trim());
            }
            info.Add(Caps(string.Join(" · ", details), 8, Theme.Stone400));
            var card = Card(IconRow(Avatar(def.Name, id == Session.SpeakerId ? Theme.Gold500 : Theme.AvatarColor(id), 40), info),
                Theme.Stone800, id == Session.SpeakerId ? Theme.Gold500 : Theme.Stone700, 12);
            card.Padding = new Thickness(12, 10);
            list.Add(OnTap(card, () => TalkAs(npcId, id)));
        }
        var cancel = Btn("Annuler", () => { HideOverlay(); Render(); });
        var panel = new Border
        {
            BackgroundColor = Theme.Stone900,
            Stroke = Theme.Gold600,
            StrokeThickness = 1.5,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
            Padding = new Thickness(16, 18),
            Margin = new Thickness(20),
            VerticalOptions = LayoutOptions.Center,
            Content = new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    IconCaps(Ico.MessageCircle, npc.Name, Theme.Gold500, 10),
                    new Label { Text = T("speaker.ask"), FontFamily = "serif", FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = Theme.Stone100 },
                    list,
                    cancel,
                },
            },
        };
        return new ScrollView { Content = panel };
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
        MapSelectedLocation = null;
        MapSelectedBuilding = null;
        MapPartyHex = default;
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
