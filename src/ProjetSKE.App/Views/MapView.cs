using ProjetSKE.App.Pages;
using ProjetSKE.App.Ui;
using ProjetSKE.Core.Systems;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Views;

/// <summary>Carte : vue du lieu actuel (ville ou nature), puis vue du pays pour voyager.</summary>
public sealed class MapView : ContentView
{
    public MapView(GamePage page)
    {
        var s = page.Session;
        var loc = s.CurrentLocation;
        var stack = Stack(ButtonRow(
            Btn(s.InCity ? "Ville" : "Lieu", () => { page.MapShowCountry = false; page.Render(); }, selected: !page.MapShowCountry),
            Btn("Pays", () => { page.MapShowCountry = true; page.Render(); }, selected: page.MapShowCountry)));

        stack.Add(Panel(Stack(
            Txt(loc.Name, 18, Theme.Accent, bold: true),
            Muted(GameSession.LocationTypeName(loc.Type)),
            Txt(loc.Description, 13))));

        if (page.MapShowCountry) BuildCountry(page, stack);
        else BuildLocal(page, stack);

        Content = stack;
    }

    private static void BuildLocal(GamePage page, VerticalStackLayout stack)
    {
        var s = page.Session;
        var loc = s.CurrentLocation;

        if (s.InCity)
        {
            stack.Add(Section("Bâtiments"));
            stack.Add(Panel(Stack(
                Row(Txt($"Auberge — repos complet ({loc.InnPrice} or)"), Btn("Dormir", () =>
                {
                    page.Notify(s.Rest() ? "L'équipe est reposée." : "Pas assez d'or.");
                    page.AutoSave();
                    page.Render();
                })),
                Row(Txt("Boutique"), Btn("Entrer", () => page.SwitchTab(GameTab.Shop))))));
        }

        var npcs = s.VisibleNpcs.ToList();
        if (npcs.Count > 0)
        {
            stack.Add(Section("Habitants"));
            var panel = Stack();
            foreach (var npc in npcs)
            {
                var dialogueId = npc.DialogueId;
                panel.Add(Row(Txt(npc.Name), Btn("Parler", () => page.ShowDialogue(dialogueId))));
            }
            stack.Add(Panel(panel));
        }

        if (!s.InCity)
        {
            stack.Add(Section("Actions"));
            var panel = Stack();
            if (s.PendingFixedBattle is { } fb)
            {
                var names = string.Join(", ", fb.MonsterIds.Distinct().Select(id => s.Db.Monsters[id].Name));
                panel.Add(Row(Txt($"Affronter : {names}", 14, Theme.Danger), Btn("Combattre", () => page.StartFixedBattle(fb))));
            }
            if (loc.RandomEncounters.Count > 0)
            {
                panel.Add(Row(Txt("Explorer la zone (chercher le combat)"), Btn("Explorer", () =>
                {
                    if (s.Explore() is { } monsters) page.StartBattle(monsters);
                })));
            }
            if (panel.Children.Count == 0) panel.Add(Muted("Rien à faire ici pour l'instant."));
            stack.Add(Panel(panel));
        }
    }

    private static void BuildCountry(GamePage page, VerticalStackLayout stack)
    {
        var s = page.Session;
        stack.Add(Section("Destinations"));
        var panel = Stack();
        foreach (var dest in s.Destinations)
        {
            var visited = s.State.SeenLocations.Contains(dest.Id);
            var id = dest.Id;
            panel.Add(Row(
                Stack(Txt(visited ? dest.Name : dest.Name + " (inconnu)"), Muted(GameSession.LocationTypeName(dest.Type))),
                Btn("Aller", () => page.Travel(id))));
        }
        stack.Add(Panel(panel));

        stack.Add(Section("Lieux connus"));
        var knownPanel = Stack();
        foreach (var id in s.State.SeenLocations)
        {
            var l = s.Db.Locations[id];
            var here = id == s.State.CurrentLocationId ? "  ◂ ici" : "";
            knownPanel.Add(Txt($"{l.Name} · {GameSession.LocationTypeName(l.Type)}{here}", 13, here.Length > 0 ? Theme.Accent : Theme.Text));
        }
        stack.Add(Panel(knownPanel));
    }
}
