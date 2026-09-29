using ProjetSKE.App.Pages;
using ProjetSKE.App.Ui;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Views;

/// <summary>
/// Écran de combat, par-dessus tout le reste, organisé comme un duel sur mobile :
/// en haut l'ennemi, au milieu la narration du combat, en bas l'équipe, tout en bas les actions
/// (Attaque / Défense / Objet / Fuite).
/// </summary>
public sealed class BattleView : ContentView
{
    private enum Mode { Main, Skills, Items, Target }

    private readonly GamePage _page;
    private readonly Battle _battle;
    private readonly Action _onClose;
    private Mode _mode = Mode.Main;
    private SkillDef? _skill;
    private ItemDef? _item;
    private string? _resultTitle;
    private string? _resultText;
    private bool _victory;
    private bool _gameOver;
    /// <summary>Défaite avec écran brisé : l'écran de fin remplace le bouton « Continuer ».</summary>
    private bool _shatter;
    private bool _shatterStarted;

    // Dernières valeurs affichées, pour animer les jauges d'une valeur à l'autre.
    private readonly Dictionary<Combatant, (int Hp, int Mana)> _shown = [];

    private (int Hp, int Mana) Previous(Combatant c) => _shown.TryGetValue(c, out var v) ? v : (c.Hp, c.Mana);

    public BattleView(GamePage page, Battle battle, Action onClose)
    {
        _page = page;
        _battle = battle;
        _onClose = onClose;
        Background = Theme.Vertical(Night.Stone900, Night.Stone950);
        Render();
    }

    private void Render()
    {
        if (_battle.Outcome != BattleOutcome.Ongoing && _resultTitle is null) ResolveOutcome();

        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
            },
            RowSpacing = 10,
        };

        var header = new Grid
        {
            Padding = new Thickness(16, 12, 16, 0),
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
        };
        header.Add(new HorizontalStackLayout
        {
            Spacing = 8,
            Children =
            {
                Icon(Ico.Swords, 16, Theme.Gold500),
                new Label { Text = _page.T("battle.title").ToUpperInvariant(), FontFamily = "serif", FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Night.Stone100, CharacterSpacing = 4 },
            },
        }, 0, 0);
        header.Add(Caps($"Tour {_battle.Round}", 10, Night.Stone500), 1, 0);

        grid.Add(GoldLine(3), 0, 0);
        grid.Add(header, 0, 1);
        grid.Add(Padded(BuildEnemies()), 0, 2);
        grid.Add(Padded(BuildLog()), 0, 3);
        grid.Add(Padded(BuildAllies()), 0, 4);
        grid.Add(new ContentView { Content = BuildActions(), Padding = new Thickness(14, 0, 14, 16) }, 0, 5);
        Content = grid;

        foreach (var c in _battle.Allies.Concat(_battle.Enemies)) _shown[c] = (c.Hp, c.Mana);

        // L'écran se fissure avec les PV du héros, et éclate à la défaite.
        _page.UpdateCracks(HeroPercent());
        if (_shatter && !_shatterStarted)
        {
            _shatterStarted = true;
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(900), Shatter);
        }
    }

    /// <summary>PV du héros en combat (s'il ne combat pas : ses PV hors combat).</summary>
    private int? HeroPercent()
    {
        var hero = _battle.Allies.FirstOrDefault(a => a.Character?.DefId == _page.Session.State.HeroId);
        if (hero is null || hero.Stats.MaxHp <= 0) return null;
        return (int)Math.Ceiling(Math.Clamp(hero.Hp, 0, hero.Stats.MaxHp) * 100.0 / hero.Stats.MaxHp);
    }

    private async void Shatter()
    {
        var cracks = _page.Session.Db.Content.World.Cracks;
        try
        {
            if (_gameOver)
                await _page.ShatterAsync(cracks.GameOverTitle, cracks.GameOverText + "\n\nRetour à la dernière sauvegarde.", "Retour au titre", _page.GameOver);
            else
                await _page.ShatterAsync(_resultTitle ?? "", _resultText ?? "", "Se relever", _onClose);
        }
        catch (Exception e)
        {
            CrashReporter.Log("SKE éclatement impossible : " + e.Message);
            if (_gameOver) _page.GameOver();
            else _onClose();
        }
    }

    private static View Padded(View v) => new ContentView { Content = v, Padding = new Thickness(14, 0) };

    /// <summary>Case « portrait » : icône ou initiale dans un cadre.</summary>
    private static View Portrait(string? glyph, string name, Color accent, double size, PortraitDef? image = null)
    {
        if (image is not null)
        {
            return new Border
            {
                WidthRequest = size,
                HeightRequest = size * 1.2,
                VerticalOptions = LayoutOptions.Start,
                BackgroundColor = Night.Stone950,
                Stroke = accent,
                StrokeThickness = 1.5,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                Content = new FramedImage(image),
            };
        }
        View inner = glyph is not null
            ? Icon(glyph, size * 0.5, accent)
            : new Label
            {
                Text = string.IsNullOrWhiteSpace(name) ? "?" : name.Trim()[..1].ToUpperInvariant(),
                FontFamily = "serif", FontSize = size * 0.5, FontAttributes = FontAttributes.Bold, TextColor = accent,
                HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center,
            };
        return new Border
        {
            WidthRequest = size,
            HeightRequest = size * 1.2,
            VerticalOptions = LayoutOptions.Start,
            BackgroundColor = Night.Stone950,
            Stroke = accent,
            StrokeThickness = 1.5,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            Content = inner,
        };
    }

    /// <summary>Fiche d'un combattant : portrait à gauche, nom et jauges à droite.</summary>
    private View Fighter(Combatant c, string? glyph, Color accent, Color background, Color stroke, double portrait, bool compact)
    {
        var before = Previous(c);
        var name = Txt(c.Name + (c.IsAlive ? "" : " (K.O.)"), compact ? 12 : 15, c.IsAlive ? Night.Stone100 : Night.Stone600, bold: true);
        name.LineBreakMode = LineBreakMode.TailTruncation;
        name.MaxLines = 1;
        var bars = new VerticalStackLayout { Spacing = compact ? 3 : 5, VerticalOptions = LayoutOptions.Center };
        bars.Add(name);
        bars.Add(AnimatedBar(_page.T("hp"), before.Hp, c.Hp, c.Stats.MaxHp, c.IsAlly ? Theme.Green500 : Theme.Red500, compact ? 6 : 9, dark: true));
        if (c.Stats.MaxMana > 0)
            bars.Add(AnimatedBar(_page.T("mp"), before.Mana, c.Mana, c.Stats.MaxMana, Theme.Blue500, compact ? 4 : 6, dark: true));
        if (c.Defending) bars.Add(IconRow(Icon(Ico.Shield, 11, Theme.Gold500), Txt("En garde", 10, Theme.Gold500, bold: true)));
        if (c.StatusText is { Length: > 0 } status)
            bars.Add(IconRow(Icon(Ico.Sparkles, 11, Theme.Purple600), Txt(status, 10, Night.Stone300, bold: true)));

        var row = new Grid
        {
            ColumnSpacing = compact ? 8 : 12,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
        };
        var portraitId = c.Monster?.PortraitId ?? (c.Character is { } pc ? _page.Session.DefOf(pc).PortraitId : null);
        var image = portraitId is not null && _page.Session.Db.Portraits.TryGetValue(portraitId, out var pd) ? pd : null;
        row.Add(Portrait(glyph, c.Name, accent, portrait, image), 0, 0);
        row.Add(bars, 1, 0);

        var card = Card(row, background, stroke, 12);
        card.Padding = compact ? new Thickness(8) : new Thickness(12, 10);
        card.Opacity = c.IsAlive ? 1 : 0.4;
        if (c.Hp < before.Hp) Shake(card);
        return card;
    }

    // ------------------------------------------------------------------ Haut : l'ennemi

    private View BuildEnemies()
    {
        var single = _battle.Enemies.Count == 1;
        var cards = _battle.Enemies.Select(e =>
        {
            var glyph = e.IsAlive ? (e.IsBoss ? Ico.Crown : Ico.Skull) : Ico.X;
            var accent = e.IsAlive ? Theme.Red500 : Night.Stone600;
            return Fighter(e, glyph, accent, Night.Stone900, Color.FromArgb("#7F1D1D"), single ? 64 : 38, compact: !single);
        }).ToList();
        return TileGrid(cards, single ? 1 : 2);
    }

    // ------------------------------------------------------------------ Bas : l'équipe

    private View BuildAllies()
    {
        var cards = _battle.Allies.Select(a =>
        {
            var current = a == _battle.CurrentActor;
            var def = a.Character is { } c ? _page.Session.DefOf(c) : null;
            var accent = current ? Theme.Gold500 : Theme.AvatarColor(def?.Id ?? a.Name);
            return Fighter(a, null, accent, Night.Stone800, current ? Theme.Gold500 : Night.Stone700,
                _battle.Allies.Count == 1 ? 48 : 30, compact: _battle.Allies.Count > 1);
        }).ToList();
        return TileGrid(cards, Math.Clamp(cards.Count, 1, 3));
    }

    // ------------------------------------------------------------------ Milieu : journal du combat (parchemin)

    private View BuildLog()
    {
        var log = new VerticalStackLayout { Spacing = 6 };
        var first = Math.Max(0, _battle.Log.Count - 12);
        for (var i = first; i < _battle.Log.Count; i++)
        {
            var latest = i == _battle.Log.Count - 1;
            if (_battle.SpeechLines.Contains(i))
            {
                // Réplique : en italique serif, avec une icône de parole.
                var speech = new Label
                {
                    Text = _battle.Log[i], FontFamily = "serif", FontSize = latest ? 15 : 13, FontAttributes = FontAttributes.Italic,
                    TextColor = latest ? Theme.Gold700 : Theme.Stone600,
                };
                log.Add(IconRow(Icon(Ico.Speech, 14, Theme.Gold600), speech));
            }
            else log.Add(Txt(_battle.Log[i], latest ? 14 : 12, latest ? Theme.Stone900 : Theme.Stone500, bold: latest));
        }
        if (_resultTitle is not null)
        {
            log.Add(new Label
            {
                Text = _resultTitle, FontFamily = "serif", FontSize = 30, FontAttributes = FontAttributes.Bold, CharacterSpacing = 5,
                TextColor = _victory ? Theme.Gold600 : Theme.Red600, HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 12, 0, 0),
            });
            log.Add(new Label { Text = _resultText, FontSize = 14, TextColor = Theme.Stone800, HorizontalTextAlignment = TextAlignment.Center });
        }
        var scroll = new ScrollView { Content = log };
        scroll.Loaded += async (_, _) => await scroll.ScrollToAsync(0, 100000, false);

        var card = new Border
        {
            BackgroundColor = Theme.Parchment,
            Stroke = Theme.Stone700,
            StrokeThickness = 2,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            Padding = new Thickness(14, 10),
            Content = new Grid
            {
                RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) },
                RowSpacing = 6,
                Children = { IconCaps(Ico.ScrollText, "Journal du combat", Theme.Stone500, 9) },
            },
        };
        ((Grid)card.Content).Add(scroll, 0, 1);
        return card;
    }

    // ------------------------------------------------------------------ Bas : actions

    private static View ActionTile(string glyph, string label, Action onTap, bool enabled = true)
    {
        var tile = Card(new HorizontalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                Icon(glyph, 20, Theme.Gold500),
                new Label
                {
                    Text = label.ToUpperInvariant(), FontSize = 11, FontAttributes = FontAttributes.Bold, CharacterSpacing = 2,
                    TextColor = Night.Stone100, VerticalTextAlignment = TextAlignment.Center,
                },
            },
        }, Night.Stone800, Night.Stone700, 12);
        tile.Padding = new Thickness(6, 14);
        tile.Opacity = enabled ? 1 : 0.35;
        if (enabled) OnTap(tile, onTap);
        return tile;
    }

    private static Button DarkBtn(string text, Action onClick, bool enabled = true)
    {
        var b = Btn(text, onClick, enabled);
        b.BackgroundColor = Night.Stone800;
        b.BorderColor = Night.Stone700;
        b.TextColor = Night.Stone100;
        return b;
    }

    private View BuildActions()
    {
        if (_battle.Outcome != BattleOutcome.Ongoing)
        {
            if (_shatter) return new BoxView { HeightRequest = 44, Color = Colors.Transparent };
            return Primary("Continuer", () =>
            {
                if (_gameOver) _page.GameOver();
                else _onClose();
            });
        }

        var actor = _battle.CurrentActor!;
        switch (_mode)
        {
            case Mode.Skills:
            {
                var buttons = new List<View>();
                // Compétences verrouillées (prologue) : seulement la première, l'attaque de base.
                var skills = _page.Session.IsLocked(UiFeature.BattleSkills) ? actor.Skills.Take(1) : actor.Skills;
                foreach (var skill in skills)
                {
                    var details = new List<string>();
                    if (skill.ManaCost > 0) details.Add($"{skill.ManaCost} {_page.T("mp")}");
                    if (skill.HpCost > 0) details.Add($"{skill.HpCost} {_page.T("hp")}");
                    if (actor.CooldownOf(skill) is var wait && wait > 0) details.Add($"recharge {wait}");
                    var cost = details.Count > 0 ? " · " + string.Join(" · ", details) : "";
                    var s = skill;
                    buttons.Add(DarkBtn(skill.Name + cost, () => PickSkill(s), enabled: _battle.CanUse(skill)));
                }
                var stack = Stack(Caps($"{actor.Name} — {actor.Mana} PM", 9, Night.Stone500));
                if (buttons.Count > 0) stack.Add(TileGrid(buttons));
                stack.Add(ButtonRow(
                    DarkBtn("Passer", () => { _battle.Wait(); SetMode(Mode.Main); }),
                    DarkBtn("◂ Retour", () => SetMode(Mode.Main))));
                return stack;
            }
            case Mode.Items:
            {
                var items = _page.Session.Bag().Where(b => b.Item.IsConsumable).ToList();
                var stack = Stack(Caps("Objets", 9, Night.Stone500));
                if (items.Count == 0) stack.Add(Txt("Aucun objet utilisable.", 13, Night.Stone400));
                else
                {
                    stack.Add(TileGrid(items.Select(b =>
                    {
                        var it = b.Item;
                        return (View)DarkBtn($"{it.Name} x{b.Count}", () => { _item = it; _skill = null; SetMode(Mode.Target); });
                    }).ToList()));
                }
                stack.Add(DarkBtn("◂ Retour", () => SetMode(Mode.Main)));
                return stack;
            }
            case Mode.Target:
            {
                var targets = _skill is not null ? _battle.TargetsFor(_skill) : _battle.Allies.Where(a => a.IsAlive).ToList();
                var stack = Stack(Caps("Choisir la cible", 9, Night.Stone500));
                stack.Add(TileGrid(targets.Select(t =>
                {
                    var target = t;
                    return (View)DarkBtn($"{t.Name} ({t.Hp}/{t.Stats.MaxHp})", () => Act(target));
                }).ToList()));
                stack.Add(DarkBtn("◂ Retour", () => SetMode(_skill is not null ? Mode.Skills : Mode.Items)));
                return stack;
            }
            default:
            {
                var fleeText = _battle.CanFlee ? $"{_page.T("battle.flee")} {_battle.FleeChance():P0}" : _page.T("battle.flee");
                return Stack(
                    new Label
                    {
                        Text = $"AU TOUR DE {actor.Name.ToUpperInvariant()}", FontSize = 10, FontAttributes = FontAttributes.Bold,
                        CharacterSpacing = 3, TextColor = Theme.Gold500, HorizontalTextAlignment = TextAlignment.Center,
                    },
                    TileGrid(MainActions(fleeText), 2));
            }
        }
    }

    /// <summary>Commandes du tour (sans celles verrouillées, ex : pendant le prologue).</summary>
    private List<View> MainActions(string fleeText)
    {
        var s = _page.Session;
        var actions = new List<View> { ActionTile(Ico.Swords, _page.T("battle.attack"), () => SetMode(Mode.Skills)) };
        if (!s.IsLocked(UiFeature.BattleDefend)) actions.Add(ActionTile(Ico.Shield, _page.T("battle.defend"), Defend));
        if (!s.IsLocked(UiFeature.BattleItems)) actions.Add(ActionTile(Ico.FlaskConical, _page.T("battle.item"), () => SetMode(Mode.Items)));
        if (!s.IsLocked(UiFeature.BattleFlee)) actions.Add(ActionTile(Ico.Footprints, fleeText, Flee, _battle.CanFlee));
        return actions;
    }

    private void SetMode(Mode mode)
    {
        _mode = mode;
        Render();
    }

    private void PickSkill(SkillDef skill)
    {
        _skill = skill;
        _item = null;
        if (Battle.NeedsTarget(skill))
        {
            SetMode(Mode.Target);
            return;
        }
        _battle.UseSkill(skill);
        SetMode(Mode.Main);
    }

    private void Act(Combatant target)
    {
        if (_skill is not null) _battle.UseSkill(_skill, target);
        else if (_item is not null) _battle.UseItem(_item, target);
        _skill = null;
        _item = null;
        SetMode(Mode.Main);
    }

    private void Defend()
    {
        _battle.Defend();
        SetMode(Mode.Main);
    }

    private void Flee()
    {
        _battle.TryFlee();
        SetMode(Mode.Main);
    }

    // ------------------------------------------------------------------ Fin du combat

    private void ResolveOutcome()
    {
        var session = _page.Session;
        switch (_battle.Outcome)
        {
            case BattleOutcome.Victory:
            {
                var r = session.ApplyVictory(_battle);
                var lines = new List<string> { $"+{r.Xp} {_page.T("xp")}   ·   +{r.Gold} {_page.T("money")}" };
                if (r.ItemIds.Count > 0)
                    lines.Add("Butin : " + string.Join(", ", r.ItemIds.Select(id => session.Db.Items[id].Name)));
                lines.AddRange(r.LevelUps);
                _victory = true;
                _resultTitle = _page.T("battle.victory").ToUpperInvariant();
                _resultText = string.Join("\n", lines);
                break;
            }
            case BattleOutcome.Defeat:
            {
                var d = session.ApplyDefeat();
                _gameOver = d.IsGameOver;
                var cracks = session.Db.Content.World.Cracks;
                _shatter = cracks.Enabled || _gameOver;
                _resultTitle = _page.T("battle.defeat").ToUpperInvariant();
                _resultText = d.IsGameOver
                    ? "Game over. Retour à la dernière sauvegarde."
                    : $"L'équipe se réveille à {session.Db.Locations[d.ReturnLocationId!].Name} (-{d.GoldLost} {_page.T("money")}).";
                break;
            }
            default:
                session.AfterFlee();
                _resultTitle = "FUITE";
                _resultText = "Vous avez pris la fuite.";
                break;
        }
    }
}
