using Godot;

/// <summary>现金余额：仅由系统扣款，Demo 起始 5000 元。</summary>
public partial class MoneySystem : Node
{
	public static MoneySystem Instance { get; private set; }

	public const int StartingYuan = 5000;

	public int Yuan { get; private set; } = StartingYuan;

	public override void _EnterTree()
	{
		Instance = this;
	}

	public void ResetForSession()
	{
		Yuan = StartingYuan;
	}

	/// <summary>尝试扣款；余额不足则扣到 0 并返回 false。</summary>
	public bool TrySpend(int amount)
	{
		if (amount <= 0) return true;
		if (Yuan >= amount)
		{
			Yuan -= amount;
			return true;
		}
		Yuan = 0;
		return false;
	}

	public void Add(int amount)
	{
		if (amount <= 0) return;
		Yuan += amount;
	}
}
