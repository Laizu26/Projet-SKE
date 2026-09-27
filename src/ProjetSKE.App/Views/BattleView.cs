using ProjetSKE.App.Pages;
using ProjetSKE.App.Ui;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Views;

/// <summary>
/// Écran de combat, par-dessus tout le reste :
/// en haut les barres de vie (ennemis puis équipe), au milieu le journal du combat, en bas Attaque / Objet / Fuite.
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

    // Dernières valeurs affichées, pour animer les jauges d'une valeur à l'autre.
    private readonly Dictionary<Combatant, (int Hp, int Mana)> _shown = [];

    private (int Hp, int Mana) Previous(Combatant c) => _shown.TryGetValue(c, out var v) ? v : (c.Hp, c.Mana);

    public BattleView(GamePage page, Battle battle, Action onClose)
    {
        _page = page;
        _battle = battle;
        _onClose = onClose;
        Background = Theme.Vertical(Theme.Stone900, Theme.Stone950);
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
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
            },
            RowSpacing = 10,
        };

        var header = new Grid
        {
            Padding = new Thickness(16, 14, 16, 4),
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
        };
        header.Add(new HorizontalStackLayout
        {
            Spacing = 8,
            Children =
            {
                Icon(Ico.Swords, 18, Theme.Gold500),
                new Label { Text = "COMBAT", FontFamily = "serif", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Theme.Stone100, CharacterSpacing = 4 },
            },
        }, 0, 0);
        header.Add(Caps($"Tour {_battle.Round}", 10, Theme.Stone500), 1, 0);

        grid.Add(GoldLine(3), 0, 0);
        grid.Add(header, 0, 1);
        grid.Add(Padded(BuildEnemies()), 0, 2);
        grid.Add(Padded(BuildAllies()), 0, 3);
        grid.Add(Padded(BuildLog()), 0, 4);
        grid.Add(new ContentView { Content = BuildActions(), Padding = new Thickness(14, 0, 14, 16) }, 0, 5);
        Content = grid;

        foreach (var c in _battle.Allies.Concat(_battle.Enemies)) _shown[c] = (c.Hp, c.Mana);
    }

    private static View Padded(View v) => new ContentView { Content = v, Padding = new Thickness(14, 0) };

    // ------------------------------------------------------------------ Haut : barres de vie

    private View BuildEnemies()
    {
        var cards = _battle.Enemies.Select(e =>
        {
            var title = new HorizontalStackLayout { Spacing = 6 };
            title.Add(Icon(e.IsAlive ? (e.IsBoss ? Ico.Crown : Ico.Skull) : Ico.X, 13, e.IsAlive ? Theme.Red500 : Theme.Stone600));
            title.Add(Txt(e.Name, 13, e.IsAlive ? Theme.Stone100 : Theme.Stone600, bold: true));
            if (e.IsBoss) title.Add(Badge("Boss", Theme.Red500));
            var before = Previous(e);
            var card = Card(Stack(title, AnimatedBar("PV", before.Hp, e.Hp, e.Stats.MaxHp, Theme.Red500, 8, dark: true)),
                Theme.Stone900, Color.FromArgb("#7F1D1D"), 12);
            card.Padding = new Thickness(12, 10);
            card.Opacity = e.IsAlive ? 1 : 0.4;
            if (e.Hp < before.Hp) Shake(card);
            return (View)card;
        }).ToList();
        return TileGrid(cards, cards.Count == 1 ? 1 : 2);
    }

    private View BuildAllies()
    {
        var cards = _battle.Allies.Select(a =>
        {
            var current = a == _battle.CurrentActor;
            var def = a.Character is { } c ? _page.Session.DefOf(c) : null;
            var info = new VerticalStackLayout
            {
                Spacing = 3,
                Children =
                {
                    Txt(a.Name + (a.IsAlive ? "" : " (K.O.)"), 13, current ? Theme.Gold500 : Theme.Stone100, bold: true),
                    AnimatedBar("PV", Previous(a).Hp, a.Hp, a.Stats.MaxHp, Theme.Green500, 7, dark: true),
                    AnimatedBar("PM", Previous(a).Mana, a.Mana, a.Stats.MaxMana, Theme.Blue500, 5, dark: true),
                },
            };
            var card = Card(IconRow(Avatar(a.Name, current ? Theme.Gold500 : Theme.AvatarColor(def?.Id ?? a.Name), 34), info),
                Theme.Stone800, current ? Theme.Gold500 : Theme.Stone700, 12);
            card.Padding = new Thickness(10, 8);
            card.Opacity = a.IsAlive ? 1 : 0.4;
            if (a.Hp < Previous(a).Hp) Shake(card);
            return (View)card;
        }).ToList();
        return TileGrid(cards, cards.Count == 1 ? 1 : 2);
    }

    // ------------------------------------------------------------------ Milieu : journal du combat (parchemin)

    private View BuildLog()
    {
        var log = new VerticalStackLayout { Spacing = 6 };
        var lines = _battle.Log.TakeLast(10).ToList();
        for (var i = 0; i < lines.Count; i++)
        {
            var latest = i == lines.Count - 1;
            log.Add(Txt(lines[i], latest ? 14 : 12, latest ? Theme.Stone900 : Theme.Stone500, bold: latest));
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
        var tile = Card(new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                Icon(glyph, 24, Theme.Gold500),
                new Label
                {
                    Text = label.ToUpperInvariant(), FontSize = 10, FontAttributes = FontAttributes.Bold, CharacterSpacing = 2,
                    TextColor = Theme.Stone100, HorizontalTextAlignment = TextAlignment.Center,
                },
            },
        }, Theme.Stone800, Theme.Stone700, 12);
        tile.Padding = new Thickness(6, 12);
        tile.Opacity = enabled ? 1 : 0.35;
        if (enabled) OnTap(tile, onTap);
        return tile;
    }

    private static Button DarkBtn(string text, Action onClick, bool enabled = true)
    {
        var b = Btn(text, onClick, enabled);
        b.BackgroundColor = Theme.Stone800;
        b.BorderColor = Theme.Stone700;
        b.TextColor = Theme.Stone100;
        return b;
    }

    private View BuildActions()
    {
        if (_battle.Outcome != BattleOutcome.Ongoing)
        {
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
                foreach (var skill in actor.Skills)
                {
                    var cost = skill.ManaCost > 0 ? $" · {skill.ManaCost} PM" : "";
                    var s = skill;
                    buttons.Add(DarkBtn(skill.Name + cost, () => PickSkill(s), enabled: _battle.CanUse(skill)));
                }
                var stack = Stack(Caps($"{actor.Name} — {actor.Mana} PM", 9, Theme.Stone500));
                if (buttons.Count > 0) stack.Add(TileGrid(buttons));
                stack.Add(ButtonRow(
                    DarkBtn("Passer", () => { _battle.Wait(); SetMode(Mode.Main); }),
                    DarkBtn("◂ Retour", () => SetMode(Mode.Main))));
                return stack;
            }
            case Mode.Items:
            {
                var items = _page.Session.Bag().Where(b => b.Item.IsConsumable).ToList();
                var stack = Stack(Caps("Objets", 9, Theme.Stone500));
                if (items.Count == 0) stack.Add(Txt("Aucun objet utilisable.", 13, Theme.Stone400));
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
                var stack = Stack(Caps("Choisir la cible", 9, Theme.Stone500));
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
                var fleeText = _battle.CanFlee ? $"Fuite {_battle.FleeChance():P0}" : "Fuite";
                return Stack(
                    new Label
                    {
                        Text = $"AU TOUR DE {actor.Name.ToUpperInvariant()}", FontSize = 10, FontAttributes = FontAttributes.Bold,
                        CharacterSpacing = 3, TextColor = Theme.Gold500, HorizontalTextAlignment = TextAlignment.Center,
                    },
                    TileGrid(
                    [
                        ActionTile(Ico.Swords, "Attaque", () => SetMode(Mode.Skills)),
                        ActionTile(Ico.FlaskConical, "Objet", () => SetMode(Mode.Items)),
                        ActionTile(Ico.Footprints, fleeText, Flee, _battle.CanFlee),
                    ], 3));
            }
        }
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
                var lines = new List<string> { $"+{r.Xp} XP   ·   +{r.Gold} or" };
                if (r.ItemIds.Count > 0)
                    lines.Add("Butin : " + string.Join(", ", r.ItemIds.Select(id => session.Db.Items[id].Name)));
                lines.AddRange(r.LevelUps);
                _victory = true;
                _resultTitle = "VICTOIRE";
                _resultText = string.Join("\n", lines);
                break;
            }
            case BattleOutcome.Defeat:
            {
                var d = session.ApplyDefeat();
                _gameOver = d.IsGameOver;
                _resultTitle = "DÉFAITE";
                _resultText = d.IsGameOver
                    ? "Game over. Retour à la dernière sauvegarde."
                    : $"L'équipe se réveille à {session.Db.Locations[d.ReturnLocationId!].Name} (-{d.GoldLost} or).";
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
