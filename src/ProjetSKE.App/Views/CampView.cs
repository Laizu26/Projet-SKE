using ProjetSKE.App.Pages;
using ProjetSKE.App.Ui;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.State;
using ProjetSKE.Core.Systems;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Views;

/// <summary>Campement : l'équipe (titulaires / réserve, fiches, équipement) et le sac commun.</summary>
public sealed class CampView : ContentView
{
    private readonly GamePage _page;

    public CampView(GamePage page)
    {
        _page = page;
        var stack = Stack(ButtonRow(
            Btn("Équipe", () => { page.CampShowBag = false; page.SelectedCharacter = null; page.Render(); }, selected: !page.CampShowBag),
            Btn("Sac", () => { page.CampShowBag = true; page.Render(); }, selected: page.CampShowBag)));

        var party = page.Session.State.Party;
        if (page.CampShowBag) BuildBag(stack);
        else if (page.SelectedCharacter is { } index && index < party.Count) BuildCharacter(stack, party[index]);
        else BuildParty(stack);

        Content = stack;
    }

    private void BuildParty(VerticalStackLayout stack)
    {
        var s = _page.Session;
        var party = s.State.Party;
        foreach (var group in new[] { true, false })
        {
            var members = party.Where(c => c.IsActive == group).ToList();
            if (members.Count == 0) continue;
            stack.Add(Section(group ? $"Titulaires ({members.Count}/{s.Config.MaxActiveParty})" : "Réserve"));
            foreach (var c in members)
            {
                var def = s.DefOf(c);
                var stats = s.GetStats(c);
                var index = party.IndexOf(c);
                stack.Add(Panel(Stack(
                    Row(Txt($"{def.Name} · Nv {c.Level}", 15, Theme.Accent, bold: true),
                        Btn("Fiche", () => { _page.SelectedCharacter = index; _page.Render(); })),
                    Muted(def.Title),
                    Bar("PV", c.CurrentHp, stats.MaxHp, Theme.Hp),
                    Bar("PM", c.CurrentMana, stats.MaxMana, Theme.Mana))));
            }
        }
    }

    private void BuildCharacter(VerticalStackLayout stack, CharacterState c)
    {
        var s = _page.Session;
        var def = s.DefOf(c);
        var stats = s.GetStats(c);

        stack.Add(Btn("◂ Retour à l'équipe", () => { _page.SelectedCharacter = null; _page.Render(); }));
        stack.Add(Panel(Stack(
            Txt(def.Name, 18, Theme.Accent, bold: true),
            Muted($"{def.Title} · Niveau {c.Level} · XP {c.Xp}/{GameSession.XpToNextLevel(c.Level)}"),
            Txt(def.Description, 13),
            Bar("PV", c.CurrentHp, stats.MaxHp, Theme.Hp),
            Bar("PM", c.CurrentMana, stats.MaxMana, Theme.Mana))));

        stack.Add(Section("Statistiques"));
        stack.Add(Panel(Stack(
            Txt($"ATQ {stats.Attack}   DEF {stats.Defense}   MAG {stats.Magic}   VIT {stats.Speed}"))));

        stack.Add(Section("Équipement"));
        foreach (var slot in Enum.GetValues<EquipSlot>())
        {
            var equipped = c.GetEquipped(slot) is { } id ? s.Db.Items[id] : null;
            var line = equipped is null ? $"{SlotName(slot)} : —" : $"{SlotName(slot)} : {equipped.Name} ({equipped.Bonus.ToBonusString()})";
            var slotCopy = slot;
            var panel = Stack(Row(Txt(line), Btn("Retirer", () => { s.Unequip(c, slotCopy); _page.Render(); }, enabled: equipped is not null)));
            foreach (var (item, count) in s.Bag().Where(b => b.Item.Slot == slot))
            {
                var itemId = item.Id;
                panel.Add(Row(Muted($"{item.Name} x{count} · {item.Bonus.ToBonusString()}"),
                    Btn("Équiper", () => { s.Equip(c, itemId); _page.Render(); })));
            }
            stack.Add(Panel(panel));
        }

        stack.Add(Section("Compétences"));
        var skillPanel = Stack();
        foreach (var unlock in def.Skills)
        {
            var skill = s.Db.Skills[unlock.SkillId];
            var cost = skill.ManaCost > 0 ? $" · {skill.ManaCost} PM" : "";
            skillPanel.Add(unlock.Level <= c.Level
                ? Txt($"{skill.Name}{cost} — {skill.Description}", 13)
                : Muted($"{skill.Name} (niveau {unlock.Level})"));
        }
        stack.Add(Panel(skillPanel));

        stack.Add(Btn(c.IsActive ? "Mettre en réserve" : "Passer titulaire", () =>
        {
            if (!s.ToggleActive(c))
                _page.Notify(c.IsActive ? "Il faut au moins un titulaire." : "Plus de place chez les titulaires.");
            _page.Render();
        }));
    }

    private void BuildBag(VerticalStackLayout stack)
    {
        var s = _page.Session;
        var bag = s.Bag();
        stack.Add(Section($"Sac commun · {s.State.Gold} or"));
        if (bag.Count == 0)
        {
            stack.Add(Muted("Le sac est vide."));
            return;
        }
        foreach (var (item, count) in bag)
        {
            var panel = Stack(
                Txt($"{item.Name} x{count}", 15, Theme.Accent, bold: true),
                Muted(ItemSummary(item)),
                Txt(item.Description, 13));
            if (item.IsConsumable)
            {
                var itemId = item.Id;
                var targets = s.State.Party.Select(c => (View)Btn("→ " + s.DefOf(c).Name, () =>
                {
                    if (!s.UseItem(itemId, c)) _page.Notify("Aucun effet.");
                    _page.Render();
                })).ToArray();
                panel.Add(ButtonRow(targets));
            }
            else if (item.IsEquipable)
            {
                panel.Add(Muted("S'équipe depuis la fiche d'un personnage."));
            }
            stack.Add(Panel(panel));
        }
    }
}
