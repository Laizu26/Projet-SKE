using ProjetSKE.App.Pages;
using ProjetSKE.App.Ui;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.State;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Views;

/// <summary>Campement : trésor (seul endroit où l'or est affiché), équipe, fiches et sac commun.</summary>
public sealed class CampView : ContentView
{
    private readonly GamePage _page;

    public CampView(GamePage page)
    {
        _page = page;
        var s = page.Session;
        var stack = new VerticalStackLayout { Spacing = 14 };

        stack.Add(PageHeader(Ico.Tent, "Campement", $"{s.State.Party.Count} compagnon(s) · {s.Bag().Sum(b => b.Count)} objet(s)"));

        // Trésor : carte sombre, montant en serif doré, pièces en filigrane.
        stack.Add(DarkStat(Ico.Coins, "Trésor de l'équipe", $"{s.State.Gold} or"));

        stack.Add(ButtonRow(
            Btn("Équipe", () => { page.CampShowBag = false; page.SelectedCharacter = null; page.Render(); }, selected: !page.CampShowBag),
            Btn("Sac", () => { page.CampShowBag = true; page.Render(); }, selected: page.CampShowBag)));

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
                            Children = { Txt(def.Name, 16, Theme.Stone900, bold: true), Badge($"Nv {c.Level}", Theme.Gold700) },
                        },
                        Caps(def.Title, 9, Theme.Stone500),
                        Bar("PV", c.CurrentHp, stats.MaxHp, Theme.Hp),
                        Bar("PM", c.CurrentMana, stats.MaxMana, Theme.Mana),
                    },
                };
                var card = Card(IconRow(Avatar(def.Name, Theme.AvatarColor(def.Id), 56), info, Icon(Ico.ChevronRight, 20, Theme.Stone400)));
                if (!c.IsActive) card.Opacity = 0.7;
                stack.Add(OnTap(card, () => { _page.SelectedCharacter = index; _page.Render(); }));
            }
        }
    }

    private void BuildCharacter(VerticalStackLayout stack, CharacterState c)
    {
        var s = _page.Session;
        var def = s.DefOf(c);
        var stats = s.GetStats(c);

        stack.Add(Pill("◂  Équipe", () => { _page.SelectedCharacter = null; _page.Render(); }));

        stack.Add(DarkCard(new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                Avatar(def.Name, Theme.AvatarColor(def.Id), 88),
                new Label
                {
                    Text = def.Name.ToUpperInvariant(), FontFamily = "serif", FontSize = 24, FontAttributes = FontAttributes.Bold,
                    TextColor = Theme.Stone100, CharacterSpacing = 3, HorizontalTextAlignment = TextAlignment.Center,
                },
                new Label
                {
                    Text = $"{def.Title} · Niveau {c.Level}".ToUpperInvariant(), FontSize = 10, FontAttributes = FontAttributes.Bold,
                    TextColor = Theme.Gold500, CharacterSpacing = 3, HorizontalTextAlignment = TextAlignment.Center,
                },
                new Label
                {
                    Text = def.Description, FontSize = 13, FontAttributes = FontAttributes.Italic, TextColor = Theme.Stone400,
                    HorizontalTextAlignment = TextAlignment.Center,
                },
                Bar("PV", c.CurrentHp, stats.MaxHp, Theme.Green500, 10, dark: true),
                Bar("PM", c.CurrentMana, stats.MaxMana, Theme.Blue500, 10, dark: true),
                Bar("XP", c.Xp, s.XpToNextLevel(c.Level), Theme.Gold500, 6, dark: true),
            },
        }, Ico.User, goldLine: true));

        stack.Add(Section("Statistiques"));
        stack.Add(TileGrid(
        [
            StatCell(Ico.Heart, "PV max", stats.MaxHp, Theme.Green600),
            StatCell(Ico.Droplet, "PM max", stats.MaxMana, Theme.Blue600),
            StatCell(Ico.Sword, "Attaque", stats.Attack, Theme.Red600),
            StatCell(Ico.Shield, "Défense", stats.Defense, Theme.Stone600),
            StatCell(Ico.Sparkles, "Magie", stats.Magic, Theme.Purple600),
            StatCell(Ico.Wind, "Vitesse", stats.Speed, Theme.Gold600),
        ], 3));

        var equipment = new VerticalStackLayout { Spacing = 12 };
        foreach (var slot in Enum.GetValues<EquipSlot>())
        {
            var equipped = c.GetEquipped(slot) is { } id && s.Db.Items.TryGetValue(id, out var it) ? it : null;
            var slotCopy = slot;
            var info = new VerticalStackLayout
            {
                Spacing = 2,
                Children =
                {
                    Caps(SlotName(slot), 9, Theme.Stone400),
                    Txt(equipped?.Name ?? "— vide —", 15, equipped is null ? Theme.Stone400 : Theme.Stone900, bold: equipped is not null),
                },
            };
            if (equipped is not null && equipped.Bonus.ToBonusString() is { Length: > 0 } bonus) info.Add(Txt(bonus, 12, Theme.Green600, bold: true));
            var remove = equipped is null ? null : Btn("Retirer", () => { s.Unequip(c, slotCopy); _page.Render(); });
            equipment.Add(IconRow(IconBox(SlotIcon(slot), Theme.Stone700), info, remove));
            foreach (var (item, count) in s.Bag().Where(b => b.Item.Slot == slot))
            {
                var itemId = item.Id;
                equipment.Add(Row(Muted($"↳ {item.Name} x{count} · {item.Bonus.ToBonusString()}", 12),
                    Btn("Équiper", () => { s.Equip(c, itemId); _page.Render(); })));
            }
        }
        stack.Add(TitledCard(Ico.Shield, "Équipement", equipment));

        var skills = new VerticalStackLayout { Spacing = 10 };
        foreach (var unlock in def.Skills)
        {
            if (!s.Db.Skills.TryGetValue(unlock.SkillId, out var skill)) continue;
            var learned = unlock.Level <= c.Level;
            var (glyph, color) = skill.Kind switch
            {
                SkillKind.Heal => (Ico.HeartPulse, Theme.Green600),
                SkillKind.Magical => (Ico.WandSparkles, Theme.Purple600),
                _ => (Ico.Swords, Theme.Red600),
            };
            var info = new VerticalStackLayout
            {
                Spacing = 1,
                Children =
                {
                    Txt(skill.Name, 15, learned ? Theme.Stone900 : Theme.Stone400, bold: true),
                    Muted(learned ? skill.Description : $"Apprise au niveau {unlock.Level}", 12),
                },
            };
            var cost = skill.ManaCost > 0 ? Badge($"{skill.ManaCost} PM", Theme.Blue600) : Badge("Libre", Theme.Stone500);
            var row = IconRow(IconBox(learned ? glyph : Ico.Lock, learned ? color : Theme.Stone400), info, cost);
            if (!learned) row.Opacity = 0.6;
            skills.Add(row);
        }
        stack.Add(TitledCard(Ico.WandSparkles, "Compétences", skills));

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
            var title = group.Key switch
            {
                ItemType.Consumable => "Consommables",
                ItemType.Weapon => "Armes",
                ItemType.Armor => "Armures",
                ItemType.Relic => "Reliques",
                _ => "Objets de quête",
            };
            var list = new VerticalStackLayout { Spacing = 14 };
            foreach (var (item, count) in group)
            {
                var info = new VerticalStackLayout
                {
                    Spacing = 2,
                    Children =
                    {
                        Txt(item.Name, 15, Theme.Stone900, bold: true),
                        Caps(ItemSummary(item), 9, Theme.Stone500),
                        Txt(item.Description, 12, Theme.Stone500),
                    },
                };
                var entry = Stack(IconRow(IconBox(Theme.ItemIcon(item), Theme.Stone700), info, Badge($"x{count}", Theme.Gold700)));
                if (item.IsConsumable)
                {
                    var itemId = item.Id;
                    var targets = s.State.Party.Select(c => (View)Btn(s.DefOf(c).Name, () =>
                    {
                        if (!s.UseItem(itemId, c)) _page.Notify("Aucun effet.");
                        _page.Render();
                    })).ToArray();
                    entry.Add(ButtonRow(targets));
                }
                list.Add(entry);
            }
            stack.Add(TitledCard(Theme.ItemIcon(group.First().Item), title, list));
        }
        stack.Add(Muted("Les armes, armures et reliques s'équipent depuis la fiche d'un personnage.", 12));
    }
}
