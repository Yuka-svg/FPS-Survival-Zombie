namespace cowsins
{
    public interface ISpecialEnemy
    {
        /// <summary>
        /// True once the special enemy (boss) has died. Used by wave/quest
        /// logic to gate completion on the boss actually being killed, not
        /// just on reaching a kill count (which can be satisfied by the
        /// regular zombies that spawn alongside the boss).
        /// </summary>
        bool IsDead { get; }
    }
}
