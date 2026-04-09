using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>死神花钱：列表选择 → 扣款与叙事后果。</summary>
public partial class DeathSpendUI : CanvasLayer
{
	private Control _root;
	private VBoxContainer _listHost;
	private RichTextLabel _result;
	private readonly List<SpendOption> _options = new();

	public override void _Ready()
	{
		_root = GetNodeOrNull<Control>("%SpendRoot");
		if (_root != null)
			AppTheme.ApplyTo(_root);
		_listHost = GetNodeOrNull<VBoxContainer>("%SpendList");
		_result = GetNodeOrNull<RichTextLabel>("%SpendResult");

		var close = GetNodeOrNull<Button>("%SpendClose");
		if (close != null) close.Pressed += () => Visible = false;

		_options.AddRange(SpendOptionsStore.Load());
		BuildList();
		Visible = false;
	}

	private void BuildList()
	{
		if (_listHost == null) return;
		foreach (var c in _listHost.GetChildren())
			c.QueueFree();
		foreach (var opt in _options)
		{
			var b = new Button
			{
				Text = $"{opt.Name}（{opt.Amount} 元）",
				CustomMinimumSize = new Vector2(0, AppTheme.MinButtonHeight)
			};
			b.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			var o = opt;
			b.Pressed += () => _ = OnPickAsync(o);
			_listHost.AddChild(b);
		}
	}

	public void Open()
	{
		Visible = true;
		if (_result != null) _result.Text = "";
	}

	private async Task OnPickAsync(SpendOption opt)
	{
		if (_result != null) _result.Text = "……";
		var r = await DeathSpendService.ExecuteAsync(opt);
		if (_result != null)
			_result.Text = $"{r.EffectLine}\n\n{r.Narration}";
		GameManager.Instance?.Session.ActivityLog.AppendNote("LastDay", $"花钱：{opt.Name}，{r.EffectLine}");
	}
}
