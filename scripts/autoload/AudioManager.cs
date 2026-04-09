using Godot;
using System.Collections.Generic;

/// <summary>
/// 全局音效（单播放器顺序播放）。资源放在 res://assets/audio/sfx/&lt;key&gt;.wav，缺失时仅警告一次。
/// </summary>
public partial class AudioManager : Node
{
	public static AudioManager Instance { get; private set; }

	public const string SfxPaper = "paper";
	public const string SfxStamp = "stamp";
	public const string SfxUiSoft = "ui_soft";
	public const string SfxDeathAmbient = "death_ambient";

	private AudioStreamPlayer _player;
	private readonly HashSet<string> _warnedMissing = new();

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _Ready()
	{
		_player = new AudioStreamPlayer { Bus = "Master" };
		AddChild(_player);
	}

	/// <summary>播放短音效；同键文件缺失时只告警一次。</summary>
	public void PlaySfx(string key)
	{
		if (string.IsNullOrWhiteSpace(key)) return;
		key = key.Trim();
		var path = $"res://assets/audio/sfx/{key}.wav";
		if (!ResourceLoader.Exists(path))
		{
			if (_warnedMissing.Add(key))
				GD.PushWarning($"[AudioManager] 缺少音效文件: {path}");
			return;
		}
		var stream = GD.Load<AudioStream>(path);
		if (stream == null) return;
		_player.Stream = stream;
		_player.Play();
	}
}
