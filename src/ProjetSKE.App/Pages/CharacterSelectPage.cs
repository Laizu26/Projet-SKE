using ProjetSKE.App.Ui;
using ProjetSKE.Core.Systems;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Pages;

/// <summary>Sélection du personnage de départ (pas de création).</summary>
public sealed class CharacterSelectPage : ContentPage
{
    public CharacterSelectPage(int slot)
    {
        BackgroundColor = Theme.Bg;
        var db = SkeApp.Db;
        var stack = new VerticalStackLayout { Padding = new Thickness(16), Spacing = 10 };
        stack.Add(Heading("Choisis ton personnage"));

        foreach (var def in db.Starters)
        {
            var skills = def.Skills.Where(s => s.Level <= 1).Select(s => db.Skills[s.SkillId].Name);
            var id = def.Id;
            stack.Add(Panel(Stack(
                Txt(def.Name, 18, Theme.Accent, bold: true),
                Muted(def.Title, 13),
                Txt(def.Description),
                Muted(StatsLine(def.BaseStats)),
                Muted("Compétences : " + string.Join(", ", skills)),
                Btn("Choisir " + def.Name, () =>
                {
                    var session = GameSession.NewGame(db, id);
                    SkeApp.Saves.Save(slot, session.State);
                    SkeApp.GoTo(new GamePage(session, slot, playIntro: true));
                }))));
        }

        stack.Add(Btn("◂ Retour", () => SkeApp.GoTo(new SlotPage(newGame: true))));
        Content = new ScrollView { Content = stack };
    }

    protected override bool OnBackButtonPressed()
    {
        SkeApp.GoTo(new SlotPage(newGame: true));
        return true;
    }
}
