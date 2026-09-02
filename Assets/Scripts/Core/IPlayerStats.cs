public interface IPlayerStats
{
    void AddSuctionPower(float amount);
    void AddSuctionRange(float amount);
    void AddMoveSpeed(float amount);
    void TakeDamage(int amount);
    int CurrentHP { get; }
    int MaxHP { get; }
}
