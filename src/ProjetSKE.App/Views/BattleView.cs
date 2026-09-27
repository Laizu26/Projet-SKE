using ProjetSKE.App.Pages;
using ProjetSKE.App.Ui;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.Systems;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Views;

/// <summary>
/// Écran de combat, par-dessus tout le reste :
/// en haut les barres de vie, au milieu le déroulé du combat, en bas Attaque / Objet / Fuite.
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
    private string? _result;
    private bool _gameOver;

    public BattleView(GamePage page, Battle battle, Action onClose)
    {
        _page = page;
        _battle = battle;
        _onClose = onClose;
        BackgroundColor = Theme.Bg;
        Render();
    }

    private void Render()
    {
        if (_battle.Outcome != BattleOutcome.Ongoing && _result is null) ResolveOutcome();

        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
            },
            Padding = new Thickness(10),
            RowSpacing = 8,
        };
        grid.Add(BuildBars(), 0, 0);
        grid.Add(BuildMiddle(), 0, 1);
        grid.Add(BuildActions(), 0, 2);
        Content = grid;
    }

    // ------------------------------------------------------------------ Haut : barres de vie

    private View BuildBars()
    {
        var stack = Stack(Section("Ennemis"));
        foreach (var e in _battle.Enemies)
        {
            stack.Add(Txt(e.IsAlive ? e.Name : e.Name + " (vaincu)", 13, e.IsAlive ? Theme.Danger : Theme.Muted, bold: true));
            stack.Add(Bar("PV", e.Hp, e.Stats.MaxHp, Theme.Danger));
        }
        stack.Add(Section("Équipe"));
        foreach (var a in _battle.Allies)
        {
            var current = a == _battle.CurrentActor;
            stack.Add(Txt((current ? "▶ " : "") + a.Name + (a.IsAlive ? "" : " (K.O.)"), 13,
                current ? Theme.Accent : a.IsAlive ? Theme.Text : Theme.Muted, bold: true));
            stack.Add(Bar("PV", a.Hp, a.Stats.MaxHp, Theme.Hp));
            stack.Add(Bar("PM", a.Mana, a.Stats.MaxMana, Theme.Mana));
        }
        return stack;
    }

    // ------------------------------------------------------------------ Milieu : le combat

    private View BuildMiddle()
    {
        var log = Stack();
        log.Add(Muted($"Tour {_battle.Round}"));
        foreach (var line in _battle.Log.TakeLast(8)) log.Add(Txt(line, 13));
        if (_result is not null) log.Add(Txt(_result, 14, Theme.Accent, bold: true));
        return Panel(new ScrollView { Content = log });
    }

    // ------------------------------------------------------------------ Bas : actions

    private View BuildActions()
    {
        if (_battle.Outcome != BattleOutcome.Ongoing)
        {
            return Btn("Continuer", () =>
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
                var stack = Stack(Muted($"{actor.Name} — compétences ({actor.Mana} PM)"));
                foreach (var skill in actor.Skills)
                {
                    var cost = skill.ManaCost > 0 ? $" ({skill.ManaCost} PM)" : "";
                    var s = skill;
                    stack.Add(Btn(skill.Name + cost, () => PickSkill(s), enabled: _battle.CanUse(skill)));
                }
                stack.Add(Btn("Passer son tour", () => { _battle.Wait(); SetMode(Mode.Main); }));
                stack.Add(Btn("◂ Retour", () => SetMode(Mode.Main)));
                return stack;
            }
            case Mode.Items:
            {
                var stack = Stack(Muted("Objets"));
                var items = _page.Session.Bag().Where(b => b.Item.IsConsumable).ToList();
                if (items.Count == 0) stack.Add(Muted("Aucun objet utilisable."));
                foreach (var (item, count) in items)
                {
                    var it = item;
                    stack.Add(Btn($"{item.Name} x{count}", () => { _item = it; _skill = null; SetMode(Mode.Target); }));
                }
                stack.Add(Btn("◂ Retour", () => SetMode(Mode.Main)));
                return stack;
            }
            case Mode.Target:
            {
                var stack = Stack(Muted("Choisir la cible"));
                var targets = _skill is not null ? _battle.TargetsFor(_skill) : _battle.Allies.Where(a => a.IsAlive).ToList();
                foreach (var t in targets)
                {
                    var target = t;
                    stack.Add(Btn($"{t.Name} ({t.Hp}/{t.Stats.MaxHp} PV)", () => Act(target)));
                }
                stack.Add(Btn("◂ Retour", () => SetMode(_skill is not null ? Mode.Skills : Mode.Items)));
                return stack;
            }
            default:
            {
                var fleeText = _battle.CanFlee ? $"Fuite ({_battle.FleeChance():P0})" : "Fuite";
                return Stack(
                    Muted($"Au tour de {actor.Name}"),
                    ButtonRow(
                        Btn("Attaque", () => SetMode(Mode.Skills)),
                        Btn("Objet", () => SetMode(Mode.Items)),
                        Btn(fleeText, Flee, enabled: _battle.CanFlee)));
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
                var lines = new List<string> { $"Victoire ! +{r.Xp} XP, +{r.Gold} or" };
                if (r.ItemIds.Count > 0)
                    lines.Add("Butin : " + string.Join(", ", r.ItemIds.Select(id => session.Db.Items[id].Name)));
                lines.AddRange(r.LevelUps);
                _result = string.Join("\n", lines);
                break;
            }
            case BattleOutcome.Defeat:
            {
                var d = session.ApplyDefeat();
                _gameOver = d.IsGameOver;
                _result = d.IsGameOver
                    ? "Défaite... Game over. Retour à la dernière sauvegarde."
                    : $"Défaite... L'équipe se réveille à {session.Db.Locations[d.ReturnLocationId!].Name} (-{d.GoldLost} or).";
                break;
            }
            default:
                session.AfterFlee();
                _result = "Vous avez fui le combat.";
                break;
        }
    }
}
