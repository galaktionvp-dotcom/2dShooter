using InspectorLocalization;
using UnityEngine;

/// <summary>
/// Identifies which side a character belongs to in the ArenaTest movement simulation.
/// This component does not implement combat.
/// </summary>
[DisallowMultipleComponent]
public sealed class TeamMember : MonoBehaviour
{
    public enum Team
    {
        Player,
        Enemy
    }

    [InspectorLabel("Команда", "Выбери сторону персонажа. Персонажи с противоположной командой будут искать друг друга.")]
    [SerializeField] private Team team = Team.Player;

    public Team CurrentTeam => team;
}
