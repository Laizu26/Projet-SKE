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
        stack.Add(PageHeader(Ico.User, slot < 0 ? "Partie de test" : "Choisis ton héros", "D'autres te rejoindront en chemin"));

        foreach (var def in db.Starters)
        {
            var skills = def.Skills.Where(s => s.Level <= 1 && db.Skills.ContainsKey(s.SkillId)).Select(s => db.Skills[s.SkillId].Name);
            var id = def.Id;
            var st = def.BaseStats;
            var color = Theme.AvatarColor(def.Id);
            stack.Add(DarkCard(new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    Avatar(def.Name, color, 96),
                    new Label { Text = def.Name.ToUpperInvariant(), FontFamily = "serif", FontSize = 28, FontAttributes = FontAttributes.Bold, TextColor = Theme.Stone100, CharacterSpacing = 4, HorizontalTextAlignment = TextAlignment.Center },
                    new Label { Text = def.Title.ToUpperInvariant(), FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = Theme.Gold500, CharacterSpacing = 4, HorizontalTextAlignment = TextAlignment.Center },
                    new Label { Text = def.Description, FontSize = 14, TextColor = Theme.Stone400, FontAttributes = FontAttributes.Italic, HorizontalTextAlignment = TextAlignment.Center },
                    TileGrid(
                    [
                        StatCell(Ico.Heart, "PV", st.MaxHp, Theme.Hp),
                        StatCell(Ico.Droplet, "PM", st.MaxMana, Theme.Mana),
                        StatCell(Ico.Sword, "ATQ", st.Attack, Theme.Danger),
                        StatCell(Ico.Shield, "DEF", st.Defense, Theme.Muted),
                        StatCell(Ico.Sparkles, "MAG", st.Magic, Theme.Xp),
                        StatCell(Ico.Wind, "VIT", st.Speed, Theme.Good),
                    ], 3),
                    Txt("Compétences : " + string.Join(", ", skills), 13, Theme.Stone400),
                    StartButton("Commencer avec " + def.Name, () =>
                    {
                        var session = GameSession.NewGame(db, id);
                        if (slot >= 0) SkeApp.Saves.Save(slot, session.State);
                        SkeApp.GoTo(new GamePage(session, slot, playIntro: true));
                    }),
                },
            }, Ico.User, goldLine: true));
        }

        Content = new ScrollView { Content = stack };
    }

    /// <summary>Bouton or sur fond pierre.</summary>
    private static Button StartButton(string text, Action onClick)
    {
        var b = Primary(text, onClick);
        b.BackgroundColor = Theme.Gold500;
        b.BorderColor = Theme.Gold400;
        b.TextColor = Theme.Stone900;
        return b;
    }

    protected override bool OnBackButtonPressed()
    {
        SkeApp.GoTo(_slot < 0 ? new Dev.DevHomePage() : (Page)new SlotPage(newGame: true));
        return true;
    }
}
