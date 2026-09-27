using ProjetSKE.App.Pages;
using ProjetSKE.App.Ui;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.State;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Views;

/// <summary>Campement : trésor, équipe (titulaires / réserve, fiches, équipement) et sac commun.</summary>
public sealed class CampView : ContentView
{
    private readonly GamePage _page;

    public CampView(GamePage page)
    {
        _page = page;
        var s = page.Session;
        var stack = new VerticalStackLayout { Spacing = 12 };

        // Le trésor : l'or n'est affiché qu'ici.
        stack.Add(GradientCard(IconRow(
            Icon("💰", 40),
            new VerticalStackLayout
            {
                Spacing = 0,
                Children =
                {
                    Txt($"{s.State.Gold} or", 26, Theme.AccentLight, bold: true),
                    Muted($"{s.State.Party.Count} compagnon(s) · {s.Bag().Sum(b => b.Count)} objet(s) dans le sac", 12),
                },
            }), Color.FromArgb("#5A4418"), Color.FromArgb("#1E1709")));

        stack.Add(ButtonRow(
            Btn("👥  Équipe", () => { page.CampShowBag = false; page.SelectedCharacter = null; page.Render(); }, selected: !page.CampShowBag),
            Btn("🎒  Sac", () => { page.CampShowBag = true; page.Render(); }, selected: page.CampShowBag)));

        var party = s.State.Party;
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
            stack.Add(Section(group ? $"Titulaires  {members.Count}/{s.Config.MaxActiveParty}" : "Réserve"));
            foreach (var c in members)
            {
                var def = s.DefOf(c);
                var stats = s.GetStats(c);
                var index = party.IndexOf(c);
                var info = new VerticalStackLayout
                {
                    Spacing = 4,
                    Children =
                    {
                        new HorizontalStackLayout
                        {
                            Spacing = 8,
                            Children = { Txt(def.Name, 17, Theme.Text, bold: true), Badge($"Nv {c.Level}", Theme.Accent) },
                        },
                        Muted(def.Title, 12),
                        Bar("PV", c.CurrentHp, stats.MaxHp, Theme.Hp),
                        Bar("PM", c.CurrentMana, stats.MaxMana, Theme.Mana),
                    },
                };
                var card = Card(IconRow(Avatar(def.Name, Theme.AvatarColor(def.Id), 58), info, Txt("›", 28, Theme.Muted)));
                if (!c.IsActive) card.Opacity = 0.75;
                stack.Add(OnTap(card, () => { _page.SelectedCharacter = index; _page.Render(); }));
            }
        }
    }

    private void BuildCharacter(VerticalStackLayout stack, CharacterState c)
    {
        var s = _page.Session;
        var def = s.DefOf(c);
        var stats = s.GetStats(c);
        var color = Theme.AvatarColor(def.Id);

        stack.Add(Pill("◂  Équipe", () => { _page.SelectedCharacter = null; _page.Render(); }));

        stack.Add(GradientCard(new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                Avatar(def.Name, color, 88),
                new Label { Text = def.Name, FontSize = 26, FontAttributes = FontAttributes.Bold, TextColor = Theme.AccentLight, HorizontalTextAlignment = TextAlignment.Center },
                new Label { Text = $"{def.Title} · Niveau {c.Level}", FontSize = 14, TextColor = Theme.Text, HorizontalTextAlignment = TextAlignment.Center },
                new Label { Text = def.Description, FontSize = 13, TextColor = Theme.Muted, FontAttributes = FontAttributes.Italic, HorizontalTextAlignment = TextAlignment.Center },
                Bar("PV", c.CurrentHp, stats.MaxHp, Theme.Hp, 12),
                Bar("PM", c.CurrentMana, stats.MaxMana, Theme.Mana, 12),
                Bar("XP", c.Xp, s.XpToNextLevel(c.Level), Theme.Xp, 8),
            },
        }, Theme.Darker(color, 0.6f), Theme.Bg));

        stack.Add(Section("Statistiques"));
        stack.Add(TileGrid(
        [
            StatCell("❤️", "PV MAX", stats.MaxHp, Theme.Hp),
            StatCell("🔷", "PM MAX", stats.MaxMana, Theme.Mana),
            StatCell("⚔️", "ATTAQUE", stats.Attack, Theme.Danger),
            StatCell("🛡️", "DÉFENSE", stats.Defense, Theme.Muted),
            StatCell("✨", "MAGIE", stats.Magic, Theme.Xp),
            StatCell("💨", "VITESSE", stats.Speed, Theme.Good),
        ], 3));

        stack.Add(Section("Équipement"));
        foreach (var slot in Enum.GetValues<EquipSlot>())
        {
            var equipped = c.GetEquipped(slot) is { } id && s.Db.Items.TryGetValue(id, out var it) ? it : null;
            var slotCopy = slot;
            var info = new VerticalStackLayout
            {
                Spacing = 2,
                Children =
                {
                    Muted(SlotName(slot).ToUpperInvariant(), 11),
                    Txt(equipped?.Name ?? "— vide —", 15, equipped is null ? Theme.Muted : Theme.Text, bold: equipped is not null),
                },
            };
            if (equipped is not null && equipped.Bonus.ToBonusString() is { Length: > 0 } bonus) info.Add(Txt(bonus, 12, Theme.Good));
            var remove = equipped is null ? null : Btn("Retirer", () => { s.Unequip(c, slotCopy); _page.Render(); });
            var panel = Stack(IconRow(Icon(SlotIcon(slot), 30), info, remove));
            foreach (var (item, count) in s.Bag().Where(b => b.Item.Slot == slot))
            {
                var itemId = item.Id;
                panel.Add(Row(Muted($"↳ {item.Name} x{count} · {item.Bonus.ToBonusString()}", 12),
                    Btn("Équiper", () => { s.Equip(c, itemId); _page.Render(); })));
            }
            stack.Add(Card(panel));
        }

        stack.Add(Section("Compétences"));
        foreach (var unlock in def.Skills)
        {
            if (!s.Db.Skills.TryGetValue(unlock.SkillId, out var skill)) continue;
            var learned = unlock.Level <= c.Level;
            var icon = skill.Kind switch { SkillKind.Heal => "💚", SkillKind.Magical => "🔮", _ => "🗡️" };
            var info = new VerticalStackLayout
            {
                Spacing = 2,
                Children =
                {
                    Txt(skill.Name, 15, learned ? Theme.Text : Theme.Muted, bold: true),
                    Muted(learned ? skill.Description : $"Apprise au niveau {unlock.Level}", 12),
                },
            };
            var cost = skill.ManaCost > 0 ? Badge($"{skill.ManaCost} PM", Theme.Mana) : Badge("Gratuit", Theme.Muted);
            var card = Card(IconRow(Icon(learned ? icon : "🔒", 24), info, cost));
            if (!learned) card.Opacity = 0.6;
            stack.Add(card);
        }

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
        if (bag.Count == 0)
        {
            stack.Add(Card(Muted("Le sac est vide.", 14)));
            return;
        }
        foreach (var group in bag.GroupBy(b => b.Item.Type))
        {
            stack.Add(Section(group.Key switch
            {
                ItemType.Consumable => "Consommables",
                ItemType.Weapon => "Armes",
                ItemType.Armor => "Armures",
                ItemType.Relic => "Reliques",
                _ => "Objets de quête",
            }));
            foreach (var (item, count) in group)
            {
                var info = new VerticalStackLayout
                {
                    Spacing = 2,
                    Children =
                    {
                        Txt(item.Name, 16, Theme.Text, bold: true),
                        Muted(ItemSummary(item), 12),
                        Txt(item.Description, 13, Theme.Muted),
                    },
                };
                var panel = Stack(IconRow(Icon(Theme.ItemIcon(item), 32), info, Badge($"x{count}", Theme.Accent)));
                if (item.IsConsumable)
                {
                    var itemId = item.Id;
                    var targets = s.State.Party.Select(c => (View)Btn(s.DefOf(c).Name, () =>
                    {
                        if (!s.UseItem(itemId, c)) _page.Notify("Aucun effet.");
                        _page.Render();
                    })).ToArray();
                    panel.Add(Muted("Utiliser sur :", 11));
                    panel.Add(ButtonRow(targets));
                }
                else if (item.IsEquipable)
                {
                    panel.Add(Muted("S'équipe depuis la fiche d'un personnage.", 11));
                }
                stack.Add(Card(panel));
            }
        }
    }
}
