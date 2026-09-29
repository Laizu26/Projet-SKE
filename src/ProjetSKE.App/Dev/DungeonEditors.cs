using ProjetSKE.App.Ui;
using ProjetSKE.Core.Models;
using static ProjetSKE.App.Ui.UiKit;

namespace ProjetSKE.App.Dev;

/// <summary>Donjons : suites d'épreuves (combats, dialogues, effets) sans carte, entrées par une porte posée dans un lieu.</summary>
public static class DungeonLists
{
    public static Page DungeonList() => new EntityListPage<DungeonDef>(
        "Donjons", DevState.Draft.Dungeons, x => x.Id, x => x.Name,
        (id, name) => new DungeonDef { Id = id, Name = name },
        x => new DungeonEditor(x),
        subtitle: x => $"{x.Steps.Count} épreuve(s) · portes : " + (DoorsOf(x) is { Length: > 0 } doors ? doors : "aucune"),
        help: "Un donjon = une suite d'épreuves jouées dans l'ordre, sans carte : combats, dialogues, effets (trésor, repos...). "
            + "On y entre par une porte posée dans un lieu ou un sous-lieu (fiche du lieu → « Portes de donjon »).");

    /// <summary>Lieux qui ont une porte vers ce donjon.</summary>
    public static string DoorsOf(DungeonDef d) =>
        string.Join(", ", DevState.Draft.Locations.Where(l => l.DungeonIds.Contains(d.Id)).Select(l => l.Name));

    /// <summary>Crée un donjon et pose sa porte dans un lieu.</summary>
    public static DungeonDef CreateIn(LocationDef location)
    {
        var d = new DungeonDef
        {
            Id = DevState.NewId("donjon", DevState.Draft.Dungeons.Select(x => x.Id)),
            Name = "Nouveau donjon",
            Steps = [new DungeonStep { Name = "Première salle", Type = DungeonStepType.Battle }],
        };
        DevState.Draft.Dungeons.Add(d);
        location.DungeonIds.Add(d.Id);
        DevState.Touch();
        return d;
    }
}

public sealed class DungeonEditor : EditorPage
{
    private readonly DungeonDef _x;
    public DungeonEditor(DungeonDef x) { _x = x; Render(); }
    protected override string PageTitle => "Donjon : " + _x.Name;
    protected override void GoBack() => SkeApp.GoTo(DungeonLists.DungeonList());
    protected override Action Delete => () =>
    {
        DevState.Draft.Dungeons.Remove(_x);
        foreach (var l in DevState.Draft.Locations) l.DungeonIds.Remove(_x.Id);
    };

    protected override void Build(Form f)
    {
        f.Note($"Identifiant : {_x.Id} (condition « Donjon terminé », ou {{donjon_fini {_x.Id}}} en mode texte)");
        f.TextField("Nom", _x.Name, v => _x.Name = v);
        f.TextField("Description (sur la porte et dans le donjon)", _x.Description, v => _x.Description = v, multiline: true);

        f.Header("Porte");
        var doors = DevState.Draft.Locations.Where(l => l.DungeonIds.Contains(_x.Id)).ToList();
        f.Note(doors.Count == 0 ? "Aucune porte : pose-la dans un lieu ci-dessous (ou depuis la fiche d'un lieu)."
            : "Portes : " + string.Join(", ", doors.Select(l => l.Name)));
        foreach (var door in doors)
        {
            var location = door;
            f.Add(Row(Txt("🚪 " + string.Join(" › ", DevState.PathOf(location).Select(l => l.Name)), 14, Theme.Text),
                Form.SmallButton("✕", () => { location.DungeonIds.Remove(_x.Id); DevState.Touch(); Render(); })));
        }
        f.RefField("Poser une porte dans (lieu ou sous-lieu)", null, DevState.Locations.Where(l => doors.All(d => d.Id != l.Id)), v =>
        {
            if (DevState.Draft.Locations.FirstOrDefault(l => l.Id == v) is { } location && !location.DungeonIds.Contains(_x.Id))
                location.DungeonIds.Add(_x.Id);
        }, rerender: true);
        f.Conditions("Porte ouverte seulement si", _x.Conditions);
        if (_x.Conditions.Count > 0) f.TextField("Message si fermée", _x.LockedMessage, v => _x.LockedMessage = v);
        f.BoolField("On peut le refaire une fois terminé", _x.Repeatable, v => _x.Repeatable = v);

        f.Header("Épreuves, dans l'ordre");
        f.Note("Combat : les adversaires ; Dialogue : une scène ; Effets : trésor, repos, flag... Une épreuve peut n'avoir lieu que sous conditions. "
            + "Perdre un combat ou fuir fait sortir du donjon (progression perdue).");
        f.ObjectList("Épreuves", _x.Steps, () => new DungeonStep { Name = $"Salle {_x.Steps.Count + 1}" }, (sf, st, i) =>
        {
            sf.TextField($"Épreuve {i + 1} : nom", st.Name, v => st.Name = v);
            sf.EnumField("Type", st.Type, v => st.Type = v, DevState.Name, rerender: true);
            switch (st.Type)
            {
                case DungeonStepType.Battle:
                    sf.IdList("Adversaires", st.MonsterIds, DevState.Monsters);
                    break;
                case DungeonStepType.Dialogue:
                    sf.RefField("Dialogue", st.DialogueId, DevState.Dialogues, v => st.DialogueId = v, allowNone: false,
                        emptyHint: "Aucun dialogue : crée-en un dans « Histoire — dialogues ».");
                    break;
            }
            sf.Actions(st.Type == DungeonStepType.Effects ? "Effets" : "Effets une fois réussie", st.Actions);
            sf.Conditions("A lieu seulement si (sinon sautée)", st.Conditions);
        }, "+ Épreuve");

        f.Header("Fin du donjon");
        f.Actions("Effets à la fin (récompense, flag, quête...)", _x.CompleteActions);
    }
}
