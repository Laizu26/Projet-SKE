using ProjetSKE.App.Pages;
using ProjetSKE.App.Ui;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Views;

/// <summary>
/// Écran de combat, par-dessus tout le reste :
/// en haut les barres de vie (ennemis puis équipe), au milieu le déroulé du combat, en bas Attaque / Objet / Fuite.
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

    public BattleView(GamePage page, Battle battle, Action onClose)
    {
        _page = page;
        _battle = battle;
        _onClose = onClose;
        Background = Theme.Vertical(Color.FromArgb("#2A1016"), Theme.Bg);
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
            },
            Padding = new Thickness(14, 16, 14, 14),
            RowSpacing = 10,
        };

        var header = new Label
        {
            Text = $"⚔️  COMBAT  ·  TOUR {_battle.Round}",
            FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Theme.Accent,
            HorizontalTextAlignment = TextAlignment.Center, CharacterSpacing = 3,
        };
        grid.Add(header, 0, 0);
        grid.Add(BuildEnemies(), 0, 1);
        grid.Add(BuildAllies(), 0, 2);
        grid.Add(BuildLog(), 0, 3);
        grid.Add(BuildActions(), 0, 4);
        Content = grid;
    }

    // ------------------------------------------------------------------ Haut : barres de vie

    private View BuildEnemies()
    {
        var cards = _battle.Enemies.Select(e =>
        {
            var title = new HorizontalStackLayout { Spacing = 6 };
            title.Add(Txt((e.IsAlive ? "" : "💀 ") + e.Name, 14, e.IsAlive ? Theme.Text : Theme.Muted, bold: true));
            if (e.IsBoss) title.Add(Badge("BOSS", Theme.Danger));
            var card = Card(Stack(title, Bar("PV", e.Hp, e.Stats.MaxHp, Theme.Danger, 12)),
                Color.FromArgb("#2A1A22"), Theme.Danger.WithAlpha(0.5f), 14);
            card.Padding = new Thickness(12, 10);
            card.Opacity = e.IsAlive ? 1 : 0.35;
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
                    Txt((current ? "▶ " : "") + a.Name + (a.IsAlive ? "" : " (K.O.)"), 13, current ? Theme.AccentLight : Theme.Text, bold: true),
                    Bar("PV", a.Hp, a.Stats.MaxHp, Theme.Hp, 8),
                    Bar("PM", a.Mana, a.Stats.MaxMana, Theme.Mana, 6),
                },
            };
            var card = Card(IconRow(Avatar(a.Name, Theme.AvatarColor(def?.Id ?? a.Name), 36), info),
                current ? Theme.Surface2 : Theme.Surface, current ? Theme.Accent : Theme.Stroke, 14);
            card.Padding = new Thickness(10, 8);
            card.Opacity = a.IsAlive ? 1 : 0.4;
            return (View)card;
        }).ToList();
        return TileGrid(cards, cards.Count == 1 ? 1 : 2);
    }

    // ------------------------------------------------------------------ Milieu : le combat

    private View BuildLog()
    {
        var log = new VerticalStackLayout { Spacing = 6 };
        var lines = _battle.Log.TakeLast(10).ToList();
        for (var i = 0; i < lines.Count; i++)
        {
            var latest = i == lines.Count - 1;
            log.Add(Txt(lines[i], latest ? 15 : 13, latest ? Theme.Text : Theme.Muted, bold: latest));
        }
        if (_resultTitle is not null)
        {
            log.Add(new Label
            {
                Text = _resultTitle, FontSize = 30, FontAttributes = FontAttributes.Bold, CharacterSpacing = 3,
                TextColor = _victory ? Theme.Accent : Theme.Danger, HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 12, 0, 0),
            });
            log.Add(new Label { Text = _resultText, FontSize = 15, TextColor = Theme.Text, HorizontalTextAlignment = TextAlignment.Center });
        }
        var scroll = new ScrollView { Content = log };
        scroll.Loaded += async (_, _) => await scroll.ScrollToAsync(0, 100000, false);
        var card = Card(scroll, Color.FromArgb("#141A2A"), radius: 18);
        return card;
    }

    // ------------------------------------------------------------------ Bas : actions

    private static View ActionTile(string icon, string label, Action onTap, bool enabled = true)
    {
        var tile = Card(new VerticalStackLayout
        {
            Spacing = 2,
            Children =
            {
                Icon(icon, 26),
                new Label { Text = label, FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Theme.Text, HorizontalTextAlignment = TextAlignment.Center },
            },
        }, Theme.Surface2, Theme.Accent.WithAlpha(0.5f), 16);
        tile.Padding = new Thickness(6, 10);
        tile.Opacity = enabled ? 1 : 0.35;
        if (enabled) OnTap(tile, onTap);
        return tile;
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
                    var cost = skill.ManaCost > 0 ? $"  ·  {skill.ManaCost} PM" : "";
                    var s = skill;
                    buttons.Add(Btn(skill.Name + cost, () => PickSkill(s), enabled: _battle.CanUse(skill)));
                }
                var stack = Stack(Muted($"{actor.Name} — {actor.Mana} PM", 12));
                if (buttons.Count > 0) stack.Add(TileGrid(buttons));
                stack.Add(ButtonRow(
                    Btn("⏭  Passer", () => { _battle.Wait(); SetMode(Mode.Main); }),
                    Btn("◂  Retour", () => SetMode(Mode.Main))));
                return stack;
            }
            case Mode.Items:
            {
                var items = _page.Session.Bag().Where(b => b.Item.IsConsumable).ToList();
                var stack = Stack(Muted("Objets", 12));
                if (items.Count == 0) stack.Add(Muted("Aucun objet utilisable.", 13));
                else
                {
                    stack.Add(TileGrid(items.Select(b =>
                    {
                        var it = b.Item;
                        return (View)Btn($"{Theme.ItemIcon(it)} {it.Name} x{b.Count}", () => { _item = it; _skill = null; SetMode(Mode.Target); });
                    }).ToList()));
                }
                stack.Add(Btn("◂  Retour", () => SetMode(Mode.Main)));
                return stack;
            }
            case Mode.Target:
            {
                var targets = _skill is not null ? _battle.TargetsFor(_skill) : _battle.Allies.Where(a => a.IsAlive).ToList();
                var stack = Stack(Muted("Choisir la cible", 12));
                stack.Add(TileGrid(targets.Select(t =>
                {
                    var target = t;
                    return (View)Btn($"{t.Name}  ({t.Hp}/{t.Stats.MaxHp})", () => Act(target));
                }).ToList()));
                stack.Add(Btn("◂  Retour", () => SetMode(_skill is not null ? Mode.Skills : Mode.Items)));
                return stack;
            }
            default:
            {
                var fleeText = _battle.CanFlee ? $"Fuite {_battle.FleeChance():P0}" : "Fuite";
                return Stack(
                    new Label
                    {
                        Text = $"Au tour de {actor.Name}", FontSize = 13, TextColor = Theme.AccentLight,
                        HorizontalTextAlignment = TextAlignment.Center,
                    },
                    TileGrid(
                    [
                        ActionTile("⚔️", "Attaque", () => SetMode(Mode.Skills)),
                        ActionTile("🧪", "Objet", () => SetMode(Mode.Items)),
                        ActionTile("🏃", fleeText, Flee, _battle.CanFlee),
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
                lines.AddRange(r.LevelUps.Select(l => "⭐ " + l));
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
