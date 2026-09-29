using ProjetSKE.App.Pages;
using ProjetSKE.App.Ui;
using ProjetSKE.Core.Models;
using ProjetSKE.Core.State;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Views;

/// <summary>Parties du campement, accessibles depuis le cercle autour du feu.</summary>
public enum CampSection { Hub, Team, People, Management, Resources, Bag, Places }

/// <summary>
/// Campement : un cercle autour du feu. Au centre le feu, autour les six parties (Gestion, Ressources, Persos,
/// Équipe, Sac, Lieux). L'équipe s'assoit autour du feu ; les persos se répartissent en cercles, par grade.
/// </summary>
public sealed class CampView : ContentView
{
    private readonly GamePage _page;

    /// <summary>Au-delà, un cercle de grade est trop chargé : les persos s'affichent en liste.</summary>
    private const int MaxPerRing = 9;

    /// <summary>Partie de l'interface qui correspond à un menu du camp (pour le désactiver).</summary>
    private static UiFeature? SectionFeature(CampSection section) => section switch
    {
        CampSection.Management => UiFeature.CampManagement,
        CampSection.Resources => UiFeature.CampResources,
        CampSection.People => UiFeature.CampPeople,
        CampSection.Team => UiFeature.CampTeam,
        CampSection.Bag => UiFeature.CampBag,
        CampSection.Places => UiFeature.CampPlaces,
        _ => null,
    };

    public CampView(GamePage page)
    {
        _page = page;
        var s = page.Session;
        var stack = new VerticalStackLayout { Spacing = 14 };
        var campOn = s.CampRules.Enabled;
        var section = page.CampSection;
        if (!campOn && section is CampSection.People or CampSection.Management or CampSection.Places) section = CampSection.Hub;
        if (SectionFeature(section) is { } locked && s.IsLocked(locked)) section = CampSection.Hub; // menu désactivé (prologue...)

        stack.Add(PageHeader(Ico.Tent, s.Db.T("title.camp"), section switch
        {
            CampSection.Team => s.Db.T("party"),
            CampSection.People => s.Db.T("camp.people"),
            CampSection.Management => s.Db.T("camp.manage"),
            CampSection.Resources => s.Db.T("camp.resources"),
            CampSection.Bag => s.Db.T("bag"),
            CampSection.Places => s.Db.T("camp.places"),
            _ => campOn ? $"{s.State.Party.Count} compagnon(s) · {s.CampMembers.Count} au camp" : $"{s.State.Party.Count} compagnon(s)",
        }));

        if (section != CampSection.Hub && page.SelectedCharacter is null && page.SelectedCampMember is null)
            stack.Add(Pill("◂  Autour du feu", () => { page.CampSection = CampSection.Hub; page.Render(); }));

        var party = s.State.Party;
        switch (section)
        {
            case CampSection.Team when page.SelectedCharacter is { } index && index < party.Count:
                BuildCharacter(stack, party[index]);
                break;
            case CampSection.Team: BuildTeam(stack); break;
            case CampSection.People when page.SelectedCampMember is not null: CampPeople.Build(stack, page); break;
            case CampSection.People: BuildPeople(stack); break;
            case CampSection.Management: CampPeople.Build(stack, page); break;
            case CampSection.Resources: BuildResources(stack); break;
            case CampSection.Bag: BuildBag(stack); break;
            case CampSection.Places: BuildPlaces(stack); break;
            default: BuildHub(stack); break;
        }

        Content = stack;
    }

    private void Go(CampSection section)
    {
        _page.CampSection = section;
        _page.SelectedCharacter = null;
        _page.SelectedCampMember = null;
        _page.Render();
    }

    // ------------------------------------------------------------------ Autour du feu

    private void BuildHub(VerticalStackLayout stack)
    {
        var s = _page.Session;
        var ring = new CampRing();
        ring.Circle(0.62, Theme.Gold700);
        ring.Fire(120);

        var shortage = s.CampRules.Resources.Any(r => DaysLeft(r) is < 2);
        var nodes = new List<(string Glyph, string Label, CampSection Section, string? Badge, bool Alert)>();
        if (s.CampRules.Enabled) nodes.Add((Ico.ClipboardList, s.Db.T("camp.manage"), CampSection.Management, null, false));
        nodes.Add((Ico.Package, s.Db.T("camp.resources"), CampSection.Resources, shortage ? "!" : null, shortage));
        if (s.CampRules.Enabled) nodes.Add((Ico.Users, s.Db.T("camp.people"), CampSection.People, (s.CampMembers.Count + s.State.Party.Count(c => c.DefId != s.State.HeroId && s.CampMember(c.DefId) is null)).ToString(), false));
        nodes.Add((Ico.Swords, s.Db.T("party"), CampSection.Team, s.State.Party.Count(c => c.IsActive).ToString(), false));
        nodes.Add((Ico.Backpack, s.Db.T("bag"), CampSection.Bag, null, false));
        if (s.CampRules.Enabled && s.CampRules.Buildings.Count > 0)
        {
            var buildable = s.CampRules.Buildings.Count(b => s.CannotBuild(b) is null);
            nodes.Add((Ico.Hammer, s.Db.T("camp.places"), CampSection.Places, buildable > 0 ? buildable.ToString() : null, false));
        }

        // Menus désactivés (prologue, effet « Menu : désactiver ») : ils disparaissent du cercle.
        nodes.RemoveAll(n => SectionFeature(n.Section) is { } f && s.IsLocked(f));

        // Réparties en cercle, en commençant en haut.
        for (var i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            var angle = -90 + 360.0 * i / nodes.Count;
            ring.Place(CampRing.Node(n.Glyph, n.Label, () => Go(n.Section), n.Badge, n.Alert), angle, 0.78, 84, 86);
        }
        stack.Add(ring);

        // Sous le feu : le moment, et la dernière nouvelle du camp.
        var clock = s.Clock;
        var line = s.Db.Content.Time.Enabled ? $"{clock.DateText} · {clock.TimeText}" : "";
        if (line.Length > 0) stack.Add(Centered(Caps(line, 10, Theme.Stone500)));
        if (s.CampRules.Enabled && s.State.CampLog.Count > 0)
        {
            var news = Card(IconRow(Icon(Ico.Feather, 14, Theme.Gold600), Txt(s.State.CampLog[^1], 12, Theme.Stone700)));
            news.Padding = new Thickness(12, 10);
            stack.Add(OnTap(news, () => Go(CampSection.Management)));
        }
    }

    private static View Centered(Label label)
    {
        label.HorizontalTextAlignment = TextAlignment.Center;
        return label;
    }

    // ------------------------------------------------------------------ Équipe : ceux qui combattent, assis autour du feu

    private void BuildTeam(VerticalStackLayout stack)
    {
        var s = _page.Session;
        var party = s.State.Party;
        var active = party.Where(c => c.IsActive).ToList();

        var ring = new CampRing(320);
        ring.Fire(100);
        for (var i = 0; i < active.Count; i++)
        {
            var c = active[i];
            var def = s.DefOf(c);
            var stats = s.GetStats(c);
            var index = party.IndexOf(c);
            var info = new VerticalStackLayout
            {
                Spacing = 2,
                WidthRequest = 64,
                HorizontalOptions = LayoutOptions.Center,
                Children = { Caps($"Nv {c.Level}", 8, Theme.Gold500), MiniBar(c.CurrentHp, stats.MaxHp, Theme.Green500), MiniBar(c.CurrentMana, stats.MaxMana, Theme.Blue500) },
            };
            ((Label)info.Children[0]).HorizontalTextAlignment = TextAlignment.Center;
            // En bas d'abord (face au joueur), puis tout autour.
            var angle = 90 + 360.0 * i / Math.Max(1, active.Count);
            ring.Place(CampRing.Person(CampPeople.Face(_page, c.DefId, def.Name, 54, Theme.AvatarColor(c.DefId)), def.Name, info,
                () => { _page.SelectedCharacter = index; _page.Render(); }), angle, 0.72, 86, 112);
        }
        stack.Add(ring);
        stack.Add(Centered(Caps($"Titulaires  {active.Count}/{s.Config.MaxActiveParty}", 10, Theme.Stone500)));

        var reserve = party.Where(c => !c.IsActive).ToList();
        if (reserve.Count == 0) return;
        stack.Add(Section("Réserve"));
        foreach (var c in reserve)
        {
            var def = s.DefOf(c);
            var index = party.IndexOf(c);
            var card = Card(IconRow(CampPeople.Face(_page, c.DefId, def.Name, 44, Theme.AvatarColor(c.DefId)),
                new VerticalStackLayout
                {
                    Spacing = 2,
                    Children = { Txt(def.Name, 15, Theme.Stone900, bold: true), Caps($"Nv {c.Level} · {def.ClassAndTitle}".TrimEnd(' ', '·'), 9, Theme.Stone500) },
                }, Icon(Ico.ChevronRight, 18, Theme.Stone400)));
            card.Padding = new Thickness(12, 10);
            card.Opacity = 0.8;
            stack.Add(OnTap(card, () => { _page.SelectedCharacter = index; _page.Render(); }));
        }
    }

    private static View MiniBar(int value, int max, Color color)
    {
        var fill = max <= 0 ? 0 : Math.Clamp(value / (double)max, 0, 1);
        return new Grid
        {
            HeightRequest = 4,
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition(new GridLength(Math.Max(0.001, fill), GridUnitType.Star)),
                new ColumnDefinition(new GridLength(Math.Max(0.001, 1 - fill), GridUnitType.Star))),
            BackgroundColor = Color.FromArgb("#44FFFFFF"),
            Children = { new BoxView { Color = color } },
        };
    }

    // ------------------------------------------------------------------ Persos : en cercles, par grade

    private void BuildPeople(VerticalStackLayout stack)
    {
        var s = _page.Session;
        var rules = s.CampRules;
        var party = s.State.Party;
        var hero = party.FirstOrDefault(c => c.DefId == s.State.HeroId);
        // Les compagnons de l'équipe vivent aussi au camp (au plus près du feu).
        var companions = party.Where(c => c.DefId != s.State.HeroId && s.CampMember(c.DefId) is null).ToList();
        // Puis les habitants, du grade le plus haut (près du feu) au plus bas (à l'extérieur).
        var rings = rules.Ranks.OrderByDescending(r => r.Level)
            .Select(r => (Rank: r, Members: s.CampMembers.Where(m => m.RankId == r.Id).ToList()))
            .Where(x => x.Members.Count > 0).ToList();
        var unranked = s.CampMembers.Where(m => !rules.Ranks.Any(r => r.Id == m.RankId)).ToList();
        var ringCount = rings.Count + (companions.Count > 0 ? 1 : 0);
        var tooMany = rings.Any(x => x.Members.Count > MaxPerRing) || companions.Count > MaxPerRing || ringCount > 3 || unranked.Count > 0;
        var empty = ringCount == 0;
        var asList = !empty && (_page.CampPeopleAsList || tooMany);

        if (!tooMany && !empty)
            stack.Add(ButtonRow(
                Btn("En cercle", () => { _page.CampPeopleAsList = false; _page.Render(); }, selected: !asList),
                Btn("En liste", () => { _page.CampPeopleAsList = true; _page.Render(); }, selected: asList)));
        if (asList)
        {
            if (companions.Count > 0)
            {
                stack.Add(Section(s.Db.T("party")));
                foreach (var c in companions) stack.Add(CompanionCard(c));
            }
            CampPeople.BuildList(stack, _page);
            return;
        }

        var ring = new CampRing(empty ? 250 : 340);
        ring.Fire(empty ? 90 : 84);
        // Le chef, assis juste devant le feu.
        if (hero is not null)
        {
            var heroName = s.DefOf(hero).Name;
            var crown = Icon(Ico.Crown, 12, Theme.Gold400);
            crown.HorizontalOptions = LayoutOptions.Center;
            var leader = new VerticalStackLayout
            {
                Spacing = 1,
                Children = { crown, CampPeople.Face(_page, hero.DefId, heroName, 44, Theme.Gold500), Centered(Caps(heroName, 8, Theme.Gold400)) },
            };
            var index = party.IndexOf(hero);
            OnTap(leader, () => { _page.CampSection = CampSection.Team; _page.SelectedCharacter = index; _page.Render(); });
            ring.Place(leader, 90, empty ? 0.62 : 0.4, 74, 80);
        }

        var radii = ringCount switch { 1 => new[] { 0.76 }, 2 => new[] { 0.62, 0.9 }, _ => new[] { 0.58, 0.76, 0.94 } };
        var ringIndex = 0;
        if (companions.Count > 0)
        {
            var k = radii[ringIndex];
            ring.Circle(k * 0.84, Theme.Gold600);
            for (var j = 0; j < companions.Count; j++)
            {
                var c = companions[j];
                var def = s.DefOf(c);
                var index = party.IndexOf(c);
                var sword = Icon(c.IsActive ? Ico.Swords : Ico.Moon, 10, c.IsActive ? Theme.Gold500 : Theme.Stone500);
                sword.HorizontalOptions = LayoutOptions.Center;
                // Le bas du cercle est pris par le chef : les compagnons se placent autour, en commençant en haut.
                var angle = -90 + 360.0 * (j + 0.5) / (companions.Count + 1);
                ring.Place(CampRing.Person(CampPeople.Face(_page, c.DefId, def.Name, 40, Theme.AvatarColor(c.DefId)), def.Name, sword,
                    () => { _page.CampSection = CampSection.Team; _page.SelectedCharacter = index; _page.Render(); }), angle, k, 62, 76);
            }
            ringIndex++;
        }
        foreach (var (rank, members) in rings)
        {
            var k = radii[ringIndex];
            ring.Circle(k * 0.84, Theme.Gold700);
            // Un cercle sur deux est décalé, pour que les visages ne s'alignent pas.
            var offset = ringIndex % 2 == 1 ? 180.0 / members.Count : 0;
            for (var j = 0; j < members.Count; j++)
            {
                var m = members[j];
                var id = m.Id;
                var name = s.CharacterName(id);
                var task = m.TaskId is { } t ? rules.Tasks.FirstOrDefault(x => x.Id == t) : null;
                var taskIcon = Icon(task is null ? Ico.Moon : CampIcons.Get(task.Icon), 10, task is null ? Theme.Stone500 : Theme.Gold500);
                taskIcon.HorizontalOptions = LayoutOptions.Center;
                var angle = -90 + offset + 360.0 * j / members.Count;
                ring.Place(CampRing.Person(CampPeople.Face(_page, id, name, 40, Theme.AvatarColor(id)), name, taskIcon,
                    () => { _page.SelectedCampMember = id; _page.Render(); }), angle, k, 62, 76);
            }
            ringIndex++;
        }
        stack.Add(ring);

        // Légende : du centre vers l'extérieur.
        var legend = new VerticalStackLayout { Spacing = 4 };
        if (empty)
        {
            legend.Add(Txt("Personne d'autre au camp pour l'instant.", 14, Theme.Stone900, bold: true));
            legend.Add(Muted("Les compagnons recrutés et les PNJ qui rejoignent le camp (effet « Camp : rejoindre », "
                + "ou « Vit au campement » sur un PNJ) s'installent ici, autour du feu.", 12));
        }
        else
        {
            legend.Add(Caps("Du feu vers l'extérieur", 9, Theme.Stone500));
            if (hero is not null) legend.Add(IconRow(Icon(Ico.Crown, 12, Theme.Gold600), Txt($"{rules.LeaderTitle} : {s.DefOf(hero).Name}", 12, Theme.Stone700)));
            if (companions.Count > 0) legend.Add(IconRow(Icon(Ico.Swords, 12, Theme.Stone500), Txt($"{s.Db.T("party")} · {companions.Count}", 12, Theme.Stone700)));
            foreach (var (rank, members) in rings)
                legend.Add(IconRow(Icon(Ico.Users, 12, Theme.Stone500), Txt($"{rank.Name} · {members.Count}{(rank.Max > 0 ? $"/{rank.Max}" : "")}", 12, Theme.Stone700)));
        }
        var card = Card(legend);
        card.Padding = new Thickness(12, 10);
        stack.Add(card);
        if (!empty) stack.Add(Muted("Touchez quelqu'un pour voir sa fiche.", 11));
    }

    private View CompanionCard(CharacterState c)
    {
        var s = _page.Session;
        var def = s.DefOf(c);
        var index = s.State.Party.IndexOf(c);
        var card = Card(IconRow(CampPeople.Face(_page, c.DefId, def.Name, 44, Theme.AvatarColor(c.DefId)),
            new VerticalStackLayout
            {
                Spacing = 2,
                Children = { Txt(def.Name, 15, Theme.Stone900, bold: true), Caps(c.IsActive ? "Titulaire" : "Réserve", 9, Theme.Stone500) },
            }, Icon(Ico.ChevronRight, 18, Theme.Stone400)));
        card.Padding = new Thickness(12, 10);
        return OnTap(card, () => { _page.CampSection = CampSection.Team; _page.SelectedCharacter = index; _page.Render(); });
    }

    // ------------------------------------------------------------------ Ressources

    /// <summary>Jours de réserve d'une ressource consommée (null = pas de consommation).</summary>
    private int? DaysLeft(CampResourceDef r)
    {
        var need = _page.Session.DailyNeed(r);
        return need <= 0 ? null : _page.Session.GetCampResource(r.Id) / need;
    }

    private void BuildResources(VerticalStackLayout stack)
    {
        var s = _page.Session;
        // Trésor : carte sombre, montant en serif doré, pièces en filigrane.
        stack.Add(DarkStat(Ico.Coins, s.Db.T("camp.treasure"), $"{s.State.Gold} {s.Db.T("money")}"));

        var resources = s.CampRules.Resources;
        if (resources.Count > 0)
        {
            stack.Add(Section("Stocks"));
            foreach (var r in resources)
            {
                var amount = s.GetCampResource(r.Id);
                var need = s.DailyNeed(r);
                var days = DaysLeft(r);
                var info = new VerticalStackLayout
                {
                    Spacing = 3,
                    Children = { Row(Txt(r.Name, 15, Theme.Stone900, bold: true), Txt(r.Max > 0 ? $"{amount} / {r.Max}" : amount.ToString(), 15, Theme.Stone900, bold: true)) },
                };
                if (r.Max > 0) info.Add(Bar("", amount, r.Max, days is < 2 ? Theme.Red500 : Theme.Gold500, 5));
                if (need > 0)
                {
                    var warn = days is < 2;
                    var text = amount == 0 ? $"Épuisé ! −{need} par jour : le moral baisse" : $"−{need} par jour · {days} jour{(days > 1 ? "s" : "")} de réserve";
                    info.Add(Txt(text, 11, warn ? Theme.Red600 : Theme.Stone500, bold: warn));
                }
                var card = Card(IconRow(IconBox(CampIcons.Get(r.Icon), days is < 2 ? Theme.Red600 : Theme.Stone700), info));
                card.Padding = new Thickness(12, 10);
                stack.Add(card);
            }
            stack.Add(Muted("Les tâches du camp remplissent les stocks ; chaque habitant consomme chaque jour.", 11));
        }

        // Valeurs du scénario affichées au joueur (réputation, dette...).
        var shown = s.Db.Content.Variables.Where(v => v.Visible).ToList();
        if (shown.Count > 0)
        {
            stack.Add(Section("Le camp"));
            stack.Add(TileGrid(shown.Select(v =>
            {
                var cell = Card(new VerticalStackLayout
                {
                    Spacing = 2,
                    Children = { Caps(v.Name, 9, Theme.Stone500), Txt(s.GetVariable(v.Id).ToString(), 20, Theme.Stone900, bold: true) },
                });
                cell.Padding = new Thickness(12, 10);
                return (View)cell;
            }).ToList(), Math.Min(3, shown.Count)));
        }
    }

    // ------------------------------------------------------------------ Lieux à construire

    private void BuildPlaces(VerticalStackLayout stack)
    {
        var s = _page.Session;
        var buildings = s.CampRules.Buildings.Where(b => s.IsBuilt(b.Id) || s.CheckAll(b.Conditions)).ToList();
        if (buildings.Count == 0)
        {
            stack.Add(Card(Muted("Rien à construire pour l'instant.", 14)));
            return;
        }
        foreach (var built in new[] { false, true })
        {
            var group = buildings.Where(b => s.IsBuilt(b.Id) == built).ToList();
            if (group.Count == 0) continue;
            stack.Add(Section(built ? "Construits" : "À construire"));
            foreach (var b in group)
            {
                var info = new VerticalStackLayout { Spacing = 3 };
                info.Add(Txt(b.Name, 15, Theme.Stone900, bold: true));
                if (b.Description.Length > 0) info.Add(Txt(b.Description, 12, Theme.Stone600));
                var entry = Stack();
                if (built)
                {
                    entry.Add(IconRow(IconBox(CampIcons.Get(b.Icon), Theme.Green600), info, Badge("Construit", Theme.Green600)));
                }
                else
                {
                    var costs = b.Costs.Select(c => $"{c.Amount} {s.CampResource(c.ResourceId)?.Name.ToLowerInvariant() ?? c.ResourceId}").ToList();
                    if (b.GoldCost > 0) costs.Add($"{b.GoldCost} {s.Db.T("money")}");
                    info.Add(Caps(costs.Count > 0 ? "Coût : " + string.Join(" · ", costs) : "Gratuit", 9, Theme.Gold700));
                    var reason = s.CannotBuild(b);
                    if (reason is not null) info.Add(IconRow(Icon(Ico.Lock, 11, Theme.Red600), Txt(reason, 11, Theme.Red600)));
                    entry.Add(IconRow(IconBox(CampIcons.Get(b.Icon), Theme.Stone700), info));
                    var id = b.Id;
                    entry.Add(Primary("Construire", () =>
                    {
                        if (!s.Build(id)) _page.Notify("Impossible de construire.");
                        _page.AutoSave();
                        _page.Render();
                    }, enabled: reason is null));
                }
                var card = Card(entry);
                card.Padding = new Thickness(12, 10);
                stack.Add(card);
            }
        }
    }

    private void BuildCharacter(VerticalStackLayout stack, CharacterState c)
    {
        var s = _page.Session;
        var def = s.DefOf(c);
        var stats = s.GetStats(c);

        stack.Add(Pill("◂  " + s.Db.T("party"), () => { _page.SelectedCharacter = null; _page.Render(); }));

        stack.Add(DarkCard(new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                Avatar(def.Name, Theme.AvatarColor(def.Id), 88),
                new Label
                {
                    Text = def.Name.ToUpperInvariant(), FontFamily = "serif", FontSize = 24, FontAttributes = FontAttributes.Bold,
                    TextColor = Night.Stone100, CharacterSpacing = 3, HorizontalTextAlignment = TextAlignment.Center,
                },
                new Label
                {
                    Text = string.Join(" · ", new[] { def.Class, def.Title, $"Niveau {c.Level}" }.Where(x => x.Length > 0)).ToUpperInvariant(), FontSize = 10, FontAttributes = FontAttributes.Bold,
                    TextColor = Theme.Gold500, CharacterSpacing = 3, HorizontalTextAlignment = TextAlignment.Center,
                },
                new Label
                {
                    Text = def.Description, FontSize = 13, FontAttributes = FontAttributes.Italic, TextColor = Night.Stone400,
                    HorizontalTextAlignment = TextAlignment.Center,
                },
                Bar(_page.T("hp"), c.CurrentHp, stats.MaxHp, Theme.Green500, 10, dark: true),
                Bar(_page.T("mp"), c.CurrentMana, stats.MaxMana, Theme.Blue500, 10, dark: true),
                Bar("XP", c.Xp, s.XpToNextLevel(c.Level), Theme.Gold500, 6, dark: true),
            },
        }, Ico.User, goldLine: true));

        var karma = s.Db.Content.Karma;
        if (karma.Enabled && karma.Visible)
        {
            var tier = karma.TierName(c.Karma);
            stack.Add(DarkStat(Ico.Scale, karma.Name, tier.Length > 0 ? $"{c.Karma} · {tier}" : c.Karma.ToString()));
        }
        // Jauges propres au personnage (folie...), comme le karma.
        foreach (var gauge in s.Db.Content.Gauges.Where(g => g.Visible))
        {
            var value = s.GaugeOf(c, gauge);
            var tier = gauge.TierName(value);
            stack.Add(DarkStat(Ico.Sparkles, gauge.Name, tier.Length > 0 ? $"{value} · {tier}" : value.ToString()));
        }

        // Pouvoirs du personnage (familles de compétences).
        if (s.PowersOf(c) is { Count: > 0 } powers)
            stack.Add(DarkStat(Ico.Flame, "Pouvoirs", string.Join(", ", powers.Select(p => p.Name))));

        // Passifs : ceux qui agissent en or, ceux en sommeil (conditions non remplies) grisés.
        var passives = s.PassivesOf(c);
        if (passives.Count > 0)
        {
            stack.Add(Section("Passifs"));
            foreach (var p in passives)
            {
                var active = s.IsPassiveActive(c, p);
                var row = IconRow(Icon(Ico.Sparkles, 16, active ? Theme.Gold500 : Theme.Stone400), new VerticalStackLayout
                {
                    Spacing = 1,
                    Children =
                    {
                        Txt(p.Name + (active ? "" : " · en sommeil"), 14, active ? Theme.Stone900 : Theme.Stone500, bold: true),
                        Muted(p.Description, 12),
                    },
                });
                row.Opacity = active ? 1 : 0.7;
                stack.Add(Card(row));
            }
        }

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

        stack.Add(Section("Équipement"));
        stack.Add(EquipmentDoll.Build(_page, c));

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
