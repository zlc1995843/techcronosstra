using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace TechcronossTranslationLauncher;

internal sealed class MainForm : Form
{
    private readonly LauncherSettings _settings = LauncherSettings.Load();
    private readonly Label _status;

    internal MainForm()
    {
        ModInstaller.ConfigureGameRoot(_settings.GameRoot);
        Text = "テクロノスX 离线启动器 by 托马兹";
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(920, 540);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = Theme.Paper;
        Font = new Font("Microsoft YaHei UI", 10F);
        DoubleBuffered = true;
        TryLoadGameIcon();

        var brand = new Label
        {
            Text = "テクロノスX",
            Font = new Font("Microsoft YaHei UI", 31F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(56, 58),
        };
        var subtitle = new Label
        {
            Text = "离线启动器",
            Font = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold),
            ForeColor = Theme.Mint,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(61, 126),
        };
        var credit = new Label
        {
            Text = "by 托马兹",
            Font = new Font("Microsoft YaHei UI", 11F),
            ForeColor = Color.FromArgb(215, 238, 249),
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(63, 176),
        };

        var settingsButton = new GameButton
        {
            Text = "设定",
            Accent = Theme.Pink,
            Location = new Point(476, 407),
        };
        settingsButton.Click += SettingsClicked;

        var launchButton = new GameButton
        {
            Text = "打开游戏",
            Accent = Theme.Mint,
            BackColor = Color.FromArgb(236, 255, 249),
            Location = new Point(686, 407),
        };
        launchButton.Click += LaunchClicked;

        _status = new Label
        {
            Text = StatusText(),
            ForeColor = Color.FromArgb(93, 115, 137),
            BackColor = Color.Transparent,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
            Location = new Point(460, 477),
            Size = new Size(422, 30),
        };

        Controls.AddRange([brand, subtitle, credit, settingsButton, launchButton, _status]);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var sky = new SolidBrush(Theme.Navy);
        e.Graphics.FillRectangle(sky, new Rectangle(0, 0, Width, 330));

        using var sea = new SolidBrush(Color.FromArgb(31, 126, 166));
        e.Graphics.FillRectangle(sea, new Rectangle(0, 244, Width, 86));
        using var horizon = new Pen(Theme.Cyan, 3);
        e.Graphics.DrawLine(horizon, 0, 244, Width, 244);

        using var wavePen = new Pen(Color.FromArgb(120, 219, 238), 2);
        for (var x = -20; x < Width; x += 64)
            e.Graphics.DrawArc(wavePen, x, 267, 74, 19, 190, 155);

        using var star = new SolidBrush(Color.FromArgb(215, 244, 255));
        foreach (var point in new[]
        {
            new Point(470, 52), new Point(548, 92), new Point(632, 55),
            new Point(717, 121), new Point(806, 67), new Point(862, 153),
        })
            e.Graphics.FillEllipse(star, point.X, point.Y, 4, 4);

        using var accent = new SolidBrush(Theme.Pink);
        e.Graphics.FillRectangle(accent, 60, 218, 160, 5);
        using var mint = new SolidBrush(Theme.Mint);
        e.Graphics.FillRectangle(mint, 225, 218, 72, 5);
    }

    private void SettingsClicked(object? sender, EventArgs e)
    {
        using var dialog = new SettingsForm(_settings);
        dialog.ShowDialog(this);
        try
        {
            if (!EnsureGameRoot())
                return;
            ModInstaller.Apply(_settings.ChineseEnabled);
            _status.Text = StatusText() + "，重启游戏后生效";
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "设定未应用", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void LaunchClicked(object? sender, EventArgs e)
    {
        try
        {
            if (Process.GetProcessesByName("techcronoss").Length > 0)
            {
                _status.Text = "游戏已经在运行";
                return;
            }
            if (!EnsureGameRoot())
                return;
            ModInstaller.Apply(_settings.ChineseEnabled);
            ModInstaller.LaunchGame();
            _status.Text = "正在打开游戏…";
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "无法打开游戏", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private string StatusText() => _settings.ChineseEnabled ? "中文翻译：已开启" : "中文翻译：已关闭";

    private bool EnsureGameRoot()
    {
        if (ModInstaller.HasValidGameRoot)
            return true;
        using var picker = new FolderBrowserDialog
        {
            Description = "请选择包含 techcronoss.exe 的游戏根目录",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };
        if (picker.ShowDialog(this) != DialogResult.OK)
            return false;
        ModInstaller.ConfigureGameRoot(picker.SelectedPath);
        if (!ModInstaller.HasValidGameRoot)
        {
            MessageBox.Show(this, "所选目录不是完整的游戏根目录。", "目录无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        _settings.GameRoot = ModInstaller.GameRoot;
        _settings.Save();
        TryLoadGameIcon();
        return true;
    }

    private void TryLoadGameIcon()
    {
        try
        {
            var path = Path.Combine(ModInstaller.GameRoot, "techcronoss.exe");
            if (File.Exists(path))
                Icon = Icon.ExtractAssociatedIcon(path);
        }
        catch
        {
            // The launcher remains usable when Windows cannot read the game icon.
        }
    }
}
