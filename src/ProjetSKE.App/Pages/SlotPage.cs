using ProjetSKE.App.Ui;
using ProjetSKE.Core.State;
using ProjetSKE.Core.Systems;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Pages;

/// <summary>Choix de l'emplacement de sauvegarde (3 emplacements).</summary>
public sealed class SlotPage : ContentPage
{
    private readonly bool _newGame;
    private int? _confirmOverwrite;

    public SlotPage(bool newGame)
    {
        _newGame = newGame;
        Background = Theme.PageBackground;
        Render();
    }

    private void Render()
    {
        var stack = new VerticalStackLayout { Padding = new Thickness(18, 28), Spacing = 14 };
        stack.Add(Pill("◂  Retour", () => SkeApp.GoTo(new TitlePage())));
        stack.Add(PageHeader(_newGame ? Ico.Swords : Ico.Save, _newGame ? "Nouvelle partie" : "Charger", "Choisis un emplacement de sauvegarde"));

        for (var slot = 0; slot < SaveService.SlotCount; slot++)
        {
            var state = SkeApp.Saves.Load(slot);
            var s = slot;
            var hero = state is null ? null : Hero(state);

            var info = new VerticalStackLayout
            {
                Spacing = 3,
                Children =
                {
                    Caps($"Emplacement {slot + 1}", 9, Theme.Stone400),
                    Txt(state is null ? "Vide" : hero is null ? "?" : $"{hero.Value.Name} · Nv {hero.Value.Level}", 18,
                        state is null ? Theme.Stone400 : Theme.Stone900, bold: true),
                },
            };
            if (state is not null)
            {
                info.Add(Muted(Place(state) + $" · {state.Party.Count} compagnon(s)", 13));
                info.Add(Muted($"Sauvegardé le {state.SavedAt:dd/MM/yyyy à HH:mm}", 11));
            }

            View icon = hero is { } h ? Avatar(h.Name, Theme.AvatarColor(h.Id), 56) : IconBox(Ico.BookOpen, Theme.Stone400, 56);
            View action = _newGame
                ? Btn(_confirmOverwrite == slot ? "Écraser ?" : "Choisir", () => PickNew(s, state is not null), selected: _confirmOverwrite == slot)
                : Btn("Charger", () => Load(s, state!), enabled: state is not null);

            var card = Card(IconRow(icon, info, action));
            card.MinimumHeightRequest = 110;
            stack.Add(card);
        }

        Content = new ScrollView { Content = stack };
    }

    private static (string Id, string Name, int Level)? Hero(GameState state)
    {
        var hero = state.Party.FirstOrDefault(c => c.DefId == state.HeroId) ?? state.Party.FirstOrDefault();
        if (hero is null) return null;
        var name = SkeApp.Db.Characters.TryGetValue(hero.DefId, out var def) ? def.Name : hero.DefId;
        return (hero.DefId, name, hero.Level);
    }

    private static string Place(GameState state) =>
        SkeApp.Db.Locations.TryGetValue(state.CurrentLocationId, out var loc) ? loc.Name : "?";

    private void PickNew(int slot, bool occupied)
    {
        if (occupied && _confirmOverwrite != slot)
        {
            _confirmOverwrite = slot;
            Render();
            return;
        }
        SkeApp.GoTo(new CharacterSelectPage(slot));
    }

    private static void Load(int slot, GameState state) =>
        SkeApp.GoTo(new GamePage(new GameSession(SkeApp.Db, state), slot, playIntro: false));

    protected override bool OnBackButtonPressed()
    {
        SkeApp.GoTo(new TitlePage());
        return true;
    }
}
