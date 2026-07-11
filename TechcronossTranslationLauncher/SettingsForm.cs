namespace TechcronossTranslationLauncher;

internal sealed class SettingsForm : Form
{
    private readonly LauncherSettings _settings;
    private readonly CheckBox _translationToggle;

    internal SettingsForm(LauncherSettings settings)
    {
        _settings = settings;
        Text = "翻译设定";
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(460, 230);
        BackColor = Theme.Paper;
        ForeColor = Theme.Navy;
        Font = new Font("Microsoft YaHei UI", 10F);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;

        var title = new Label
        {
            Text = "设定",
            Font = new Font("Microsoft YaHei UI", 21F, FontStyle.Bold),
            ForeColor = Theme.Navy,
            AutoSize = true,
            Location = new Point(34, 27),
        };
        var separator = new Panel
        {
            BackColor = Theme.Mint,
            Location = new Point(36, 75),
            Size = new Size(388, 3),
        };
        var label = new Label
        {
            Text = "激活中文",
            Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(38, 112),
        };
        var hint = new Label
        {
            Text = "没有中文词条时自动显示日文原文",
            ForeColor = Color.FromArgb(90, 112, 135),
            AutoSize = true,
            Location = new Point(39, 151),
        };
        _translationToggle = new CheckBox
        {
            Appearance = Appearance.Button,
            AutoCheck = true,
            Checked = settings.ChineseEnabled,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(320, 105),
            Size = new Size(104, 42),
            Cursor = Cursors.Hand,
        };
        _translationToggle.CheckedChanged += (_, _) => RefreshToggle();
        RefreshToggle();
        Controls.AddRange([title, separator, label, hint, _translationToggle]);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _settings.ChineseEnabled = _translationToggle.Checked;
        _settings.Save();
        base.OnFormClosed(e);
    }

    private void RefreshToggle()
    {
        _translationToggle.Text = _translationToggle.Checked ? "已开启" : "已关闭";
        _translationToggle.BackColor = _translationToggle.Checked ? Theme.Mint : Color.LightGray;
        _translationToggle.ForeColor = _translationToggle.Checked ? Theme.Navy : Color.DimGray;
    }
}
