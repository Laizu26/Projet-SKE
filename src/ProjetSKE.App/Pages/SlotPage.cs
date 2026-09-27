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
        BackgroundColor = Theme.Bg;
        Render();
    }

    private void Render()
    {
        var stack = new VerticalStackLayout { Padding = new Thickness(16), Spacing = 10 };
        stack.Add(Heading(_newGame ? "Nouvelle partie" : "Charger une partie"));
        stack.Add(Muted("Choisis un emplacement de sauvegarde."));

        for (var slot = 0; slot < SaveService.SlotCount; slot++)
        {
            var state = SkeApp.Saves.Load(slot);
            var s = slot;
            var info = Stack(
                Txt($"Emplacement {slot + 1}", 16, Theme.Accent, bold: true),
                Txt(state is null ? "Vide" : Summary(state)),
                state is null ? Muted("") : Muted($"Sauvegardé le {state.SavedAt:dd/MM/yyyy HH:mm}"));

            View action;
            if (_newGame)
            {
                var confirming = _confirmOverwrite == slot;
                action = Btn(confirming ? "Écraser ?" : "Choisir", () => PickNew(s, state is not null));
            }
            else
            {
                action = Btn("Charger", () => Load(s, state!), enabled: state is not null);
            }
            stack.Add(Panel(Row(info, action)));
        }

        stack.Add(Btn("◂ Retour", () => SkeApp.GoTo(new TitlePage())));
        Content = new ScrollView { Content = stack };
    }

    private static string Summary(GameState state)
    {
        var db = SkeApp.Db;
        var hero = state.Party.FirstOrDefault(c => c.DefId == state.HeroId) ?? state.Party.FirstOrDefault();
        var heroText = hero is not null && db.Characters.TryGetValue(hero.DefId, out var def) ? $"{def.Name} Nv {hero.Level}" : "?";
        var place = db.Locations.TryGetValue(state.CurrentLocationId, out var loc) ? loc.Name : "?";
        return $"{heroText} · {place} · {state.Gold} or · {state.Party.Count} perso.";
    }

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
