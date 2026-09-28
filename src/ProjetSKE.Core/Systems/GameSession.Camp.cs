using ProjetSKE.Core.Models;
using ProjetSKE.Core.State;

namespace ProjetSKE.Core.Systems;

/// <summary>
/// Campement : des PNJ et PJ y vivent, rangés dans une hiérarchie (grades), et sont affectés à des tâches
/// (rondes, chasse...). Chaque tâche se répète : à la fin de chaque cycle de temps de jeu, un résultat est tiré
/// au sort et ses effets appliqués ; le journal du camp raconte ce qui s'est passé.
/// </summary>
public sealed partial class GameSession
{
    private const int CampLogSize = 40;

    /// <summary>Membre du camp pour qui un résultat de tâche est appliqué (« @membre »).</summary>
    private string? _campContext;

    public CampSettings CampRules => Db.Content.Camp;

    public IReadOnlyList<CampMemberState> CampMembers => State.Camp;

    public CampMemberState? CampMember(string id) => State.Camp.FirstOrDefault(m => m.Id == id);

    public CampRankDef? RankOf(CampMemberState m) => CampRules.Ranks.FirstOrDefault(r => r.Id == m.RankId);

    public int RankLevel(string id) => CampMember(id) is { } m && RankOf(m) is { } r ? r.Level : int.MinValue;

    private CampRankDef? LowestRank => CampRules.Ranks.OrderBy(r => r.Level).FirstOrDefault();

    /// <summary>Un PNJ ou PJ rejoint le camp (au grade le plus bas, ou celui donné).</summary>
    public bool JoinCamp(string id, string? rankId = null)
    {
        if (string.IsNullOrEmpty(id) || CampMember(id) is not null) return false;
        if (!Db.Npcs.ContainsKey(id) && !Db.Characters.ContainsKey(id)) return false;
        var member = new CampMemberState { Id = id, RankId = LowestRank?.Id ?? "" };
        State.Camp.Add(member);
        if (rankId is not null) SetCampRank(id, rankId);
        return true;
    }

    public bool LeaveCamp(string id) => State.Camp.RemoveAll(m => m.Id == id) > 0;

    /// <summary>Places restantes pour un grade (null = illimité).</summary>
    public int? FreeSlots(CampRankDef rank) =>
        rank.Max <= 0 ? null : Math.Max(0, rank.Max - State.Camp.Count(m => m.RankId == rank.Id));

    /// <summary>Change le grade d'un membre (refusé si le grade est complet).</summary>
    public bool SetCampRank(string id, string rankId)
    {
        if (CampMember(id) is not { } m) return false;
        if (CampRules.Ranks.FirstOrDefault(r => r.Id == rankId) is not { } rank) return false;
        if (m.RankId == rankId) return true;
        if (FreeSlots(rank) == 0) return false;
        m.RankId = rankId;
        // Un grade trop bas pour sa tâche : le membre est remis au repos.
        if (m.TaskId is { } taskId && CampRules.Tasks.FirstOrDefault(t => t.Id == taskId) is { } task && rank.Level < task.MinRankLevel)
            m.TaskId = null;
        return true;
    }

    /// <summary>Pourquoi un membre ne peut pas prendre une tâche (null = il peut).</summary>
    public string? CannotTake(CampMemberState m, CampTaskDef task)
    {
        if (!CheckAll(task.Conditions)) return "Pas disponible pour l'instant";
        if ((RankOf(m)?.Level ?? int.MinValue) < task.MinRankLevel)
        {
            var needed = CampRules.Ranks.Where(r => r.Level >= task.MinRankLevel).OrderBy(r => r.Level).FirstOrDefault();
            return needed is null ? "Grade insuffisant" : $"Grade {needed.Name} minimum";
        }
        if (task.MaxWorkers > 0 && m.TaskId != task.Id && State.Camp.Count(o => o.TaskId == task.Id) >= task.MaxWorkers)
            return "Plus de place";
        return null;
    }

    /// <summary>Affecte un membre à une tâche (vide = repos). Le premier cycle commence maintenant.</summary>
    public bool SetCampTask(string id, string? taskId)
    {
        if (CampMember(id) is not { } m) return false;
        if (string.IsNullOrEmpty(taskId))
        {
            m.TaskId = null;
            return true;
        }
        if (CampRules.Tasks.FirstOrDefault(t => t.Id == taskId) is not { } task || CannotTake(m, task) is not null) return false;
        m.TaskId = task.Id;
        m.NextAt = State.Minutes + Math.Max(1, task.DurationMinutes);
        return true;
    }

    /// <summary>Fin des cycles écoulés : tirage d'un résultat pour chaque cycle terminé.</summary>
    private void UpdateCamp()
    {
        if (!CampRules.Enabled) return;
        foreach (var m in State.Camp.ToList())
        {
            if (m.TaskId is not { } taskId || CampRules.Tasks.FirstOrDefault(t => t.Id == taskId) is not { } task) continue;
            var duration = Math.Max(1, task.DurationMinutes);
            // Au plus 50 cycles d'un coup (un long voyage ne doit pas tout bloquer).
            for (var guard = 0; guard < 50 && m.NextAt <= State.Minutes; guard++)
            {
                Resolve(m, task);
                m.NextAt += duration;
            }
            if (m.NextAt <= State.Minutes) m.NextAt = State.Minutes + duration;
        }
    }

    private void Resolve(CampMemberState m, CampTaskDef task)
    {
        _campContext = m.Id;
        try
        {
            var outcomes = task.Outcomes.Where(o => o.Weight > 0 && CheckAll(o.Conditions)).ToList();
            var total = outcomes.Sum(o => o.Weight);
            if (total <= 0) return;
            var roll = Rng.Next(total);
            var outcome = outcomes.First(o => (roll -= o.Weight) < 0);
            var before = Notifications.Count;
            foreach (var action in outcome.Actions.Where(a => a.Type != ActionType.StartBattle)) Execute(action);
            // Les effets d'une tâche vont dans le journal du camp, pas dans les notifications du moment.
            var effects = Notifications.Skip(before).ToList();
            Notifications.RemoveRange(before, Notifications.Count - before);
            var line = $"{new GameClock(State.Minutes, Db.Content.Time).TimeText} · {task.Name} — {FormatText(outcome.Text)}";
            if (effects.Count > 0) line += $" ({string.Join(", ", effects)})";
            State.CampLog.Add(line);
            if (State.CampLog.Count > CampLogSize) State.CampLog.RemoveRange(0, State.CampLog.Count - CampLogSize);
        }
        finally
        {
            _campContext = null;
        }
    }
}
