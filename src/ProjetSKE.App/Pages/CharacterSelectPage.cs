using ProjetSKE.App.Ui;
using ProjetSKE.Core.Data;
using ProjetSKE.Core.Systems;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Pages;

/// <summary>Sélection du personnage de départ (pas de création).</summary>
public sealed class CharacterSelectPage : ContentPage
{
    private readonly int _slot;

    /// <param name="slot">Emplacement de sauvegarde, ou -1 pour une partie de test (jamais sauvegardée).</param>
    /// <param name="db">Contenu à utiliser (par défaut le contenu actif).</param>
    public CharacterSelectPage(int slot, GameDatabase? db = null)
    {
        _slot = slot;
        BackgroundColor = Theme.Bg;
        db ??= SkeApp.Db;
        var stack = new VerticalStackLayout { Padding = new Thickness(16), Spacing = 10 };
        stack.Add(Heading(slot < 0 ? "Partie de test : choisis ton personnage" : "Choisis ton personnage"));

        foreach (var def in db.Starters)
        {
            var skills = def.Skills.Where(s => s.Level <= 1 && db.Skills.ContainsKey(s.SkillId)).Select(s => db.Skills[s.SkillId].Name);
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
                    if (slot >= 0) SkeApp.Saves.Save(slot, session.State);
                    SkeApp.GoTo(new GamePage(session, slot, playIntro: true));
                }))));
        }

        stack.Add(Btn("◂ Retour", () => SkeApp.GoTo(slot < 0 ? new Dev.DevHomePage() : (Page)new SlotPage(newGame: true))));
        Content = new ScrollView { Content = stack };
    }

    protected override bool OnBackButtonPressed()
    {
        SkeApp.GoTo(_slot < 0 ? new Dev.DevHomePage() : (Page)new SlotPage(newGame: true));
        return true;
    }
}
