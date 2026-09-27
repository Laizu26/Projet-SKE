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
        Background = Theme.PageBackground;
        db ??= SkeApp.Db;
        var stack = new VerticalStackLayout { Padding = new Thickness(18, 28), Spacing = 16 };
        stack.Add(Pill("◂  Retour", () => SkeApp.GoTo(slot < 0 ? new Dev.DevHomePage() : (Page)new SlotPage(newGame: true))));
        stack.Add(Heading(slot < 0 ? "Partie de test" : "Choisis ton héros"));
        stack.Add(Muted("Ton aventure commence seul. D'autres te rejoindront en chemin.", 14));

        foreach (var def in db.Starters)
        {
            var skills = def.Skills.Where(s => s.Level <= 1 && db.Skills.ContainsKey(s.SkillId)).Select(s => db.Skills[s.SkillId].Name);
            var id = def.Id;
            var st = def.BaseStats;
            var color = Theme.AvatarColor(def.Id);
            stack.Add(GradientCard(new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    Avatar(def.Name, color, 96),
                    new Label { Text = def.Name, FontSize = 28, FontAttributes = FontAttributes.Bold, TextColor = Theme.AccentLight, HorizontalTextAlignment = TextAlignment.Center },
                    new Label { Text = def.Title.ToUpperInvariant(), FontSize = 12, TextColor = Theme.Text, CharacterSpacing = 3, HorizontalTextAlignment = TextAlignment.Center },
                    new Label { Text = def.Description, FontSize = 14, TextColor = Theme.Text, FontAttributes = FontAttributes.Italic, HorizontalTextAlignment = TextAlignment.Center },
                    TileGrid(
                    [
                        StatCell("❤️", "PV", st.MaxHp, Theme.Hp),
                        StatCell("🔷", "PM", st.MaxMana, Theme.Mana),
                        StatCell("⚔️", "ATQ", st.Attack, Theme.Danger),
                        StatCell("🛡️", "DEF", st.Defense, Theme.Muted),
                        StatCell("✨", "MAG", st.Magic, Theme.Xp),
                        StatCell("💨", "VIT", st.Speed, Theme.Good),
                    ], 3),
                    Muted("Compétences : " + string.Join(", ", skills), 13),
                    Primary("Commencer avec " + def.Name, () =>
                    {
                        var session = GameSession.NewGame(db, id);
                        if (slot >= 0) SkeApp.Saves.Save(slot, session.State);
                        SkeApp.GoTo(new GamePage(session, slot, playIntro: true));
                    }),
                },
            }, Theme.Darker(color, 0.55f), Theme.Bg));
        }

        Content = new ScrollView { Content = stack };
    }

    protected override bool OnBackButtonPressed()
    {
        SkeApp.GoTo(_slot < 0 ? new Dev.DevHomePage() : (Page)new SlotPage(newGame: true));
        return true;
    }
}
