using ProjetSKE.App.Pages;
using ProjetSKE.App.Ui;
using ProjetSKE.Core.Models;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Views;

/// <summary>Encyclopédie : seules les entrées rencontrées sont visibles.</summary>
public sealed class EncyclopediaView : ContentView
{
    public EncyclopediaView(GamePage page)
    {
        var s = page.Session;
        (EncyclopediaCategory Category, string Label)[] categories =
        [
            (EncyclopediaCategory.Characters, "Persos"),
            (EncyclopediaCategory.Monsters, "Monstres"),
            (EncyclopediaCategory.Locations, "Lieux"),
            (EncyclopediaCategory.Weapons, "Armes"),
            (EncyclopediaCategory.Relics, "Reliques"),
        ];
        var buttons = categories.Select(c => (View)Btn(c.Label, () =>
        {
            page.EncyclopediaCategory = c.Category;
            page.Render();
        }, selected: page.EncyclopediaCategory == c.Category)).ToArray();
        foreach (var b in buttons) ((Button)b).FontSize = 11;

        var stack = Stack(ButtonRow(buttons));
        var entries = s.GetEncyclopedia(page.EncyclopediaCategory);
        stack.Add(Section($"{categories.First(c => c.Category == page.EncyclopediaCategory).Label} ({entries.Count})"));
        if (entries.Count == 0) stack.Add(Muted("Rien de rencontré pour l'instant."));
        foreach (var (name, subtitle, description) in entries)
        {
            stack.Add(Panel(Stack(
                Txt(name, 15, Theme.Accent, bold: true),
                Muted(subtitle),
                Txt(description, 13))));
        }
        Content = stack;
    }
}

/// <summary>Boutique de la ville : achat et revente.</summary>
public sealed class ShopView : ContentView
{
    public ShopView(GamePage page)
    {
        var s = page.Session;
        s.BrowseShop();
        var stack = Stack(ButtonRow(
            Btn("Acheter", () => { page.ShopSelling = false; page.Render(); }, selected: !page.ShopSelling),
            Btn("Vendre", () => { page.ShopSelling = true; page.Render(); }, selected: page.ShopSelling)));

        if (!page.ShopSelling)
        {
            stack.Add(Section($"Boutique de {s.CurrentLocation.Name}"));
            foreach (var item in s.ShopStock)
            {
                var id = item.Id;
                var owned = s.CountItem(id);
                stack.Add(Panel(Row(
                    Stack(
                        Txt($"{item.Name} · {item.Price} or", 14, Theme.Accent, bold: true),
                        Muted(ItemSummary(item) + (owned > 0 ? $" · possédé : {owned}" : "")),
                        Txt(item.Description, 12)),
                    Btn("Acheter", () =>
                    {
                        if (s.Buy(id)) page.Notify($"{item.Name} acheté.");
                        page.Render();
                    }, enabled: s.CanBuy(item)))));
            }
        }
        else
        {
            stack.Add(Section("Revendre (moitié prix)"));
            var sellable = s.Bag().Where(b => b.Item.IsSellable).ToList();
            if (sellable.Count == 0) stack.Add(Muted("Rien à vendre. (Les objets équipés doivent d'abord être retirés.)"));
            foreach (var (item, count) in sellable)
            {
                var id = item.Id;
                stack.Add(Panel(Row(
                    Stack(Txt($"{item.Name} x{count}", 14, Theme.Accent, bold: true), Muted(ItemSummary(item))),
                    Btn($"+{item.SellPrice} or", () =>
                    {
                        if (s.Sell(id)) page.Notify($"{item.Name} vendu.");
                        page.Render();
                    }))));
            }
        }
        Content = stack;
    }
}

/// <summary>Journal : une page blanche où le joueur écrit librement.</summary>
public sealed class JournalView : ContentView
{
    public JournalView(GamePage page)
    {
        var editor = new Editor
        {
            Text = page.Session.State.Journal,
            Placeholder = "Page blanche...",
            PlaceholderColor = Theme.Muted,
            TextColor = Theme.Text,
            BackgroundColor = Theme.Panel,
            FontSize = 15,
            AutoSize = EditorAutoSizeOption.TextChanges,
            MinimumHeightRequest = 420,
        };
        editor.TextChanged += (_, e) => page.Session.State.Journal = e.NewTextValue ?? "";
        editor.Unfocused += (_, _) => page.AutoSave();
        Content = Stack(Section("Journal"), editor);
    }
}

/// <summary>Menu : sauvegarde, paramètres de jeu, retour au titre.</summary>
public sealed class MenuView : ContentView
{
    public MenuView(GamePage page)
    {
        var s = page.Session;
        var config = s.Config;
        var stack = Stack(
            Section("Partie"),
            Panel(Stack(
                Row(Txt($"Emplacement {page.Slot + 1}"), Btn("Sauvegarder", () =>
                {
                    page.AutoSave();
                    page.Notify("Partie sauvegardée.");
                    page.Render();
                })),
                Muted("La partie est aussi sauvegardée automatiquement."))),
            Section("Paramètres"),
            Panel(Stack(
                Setting("Combats en voyage", Describe(config.TravelEncounters), () => config.TravelEncounters = Next(config.TravelEncounters)),
                Setting("Défaite", Describe(config.Defeat), () => config.Defeat = Next(config.Defeat)),
                Setting("Fuite", Describe(config.Flee), () => config.Flee = Next(config.Flee)),
                Muted("Touchez une valeur pour la changer. Les boss empêchent toujours la fuite."))),
            Btn("Retour au titre", page.QuitToTitle));

        Content = stack;

        View Setting(string label, string value, Action change) =>
            Row(Txt(label), Btn(value, () => { change(); page.Render(); }));
    }
}
