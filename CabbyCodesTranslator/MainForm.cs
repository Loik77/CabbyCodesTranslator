using System;
using System.IO;
using System.Windows.Forms;

namespace CabbyCodesTranslator;

public sealed class MainForm : Form
{
    readonly TextBox input = new() { Dock = DockStyle.Fill };
    readonly TextBox output = new() { Dock = DockStyle.Fill, ReadOnly = true };
    readonly Label status = new() { Text = "请选择 CabbyCodes.dll", Dock = DockStyle.Fill, AutoSize = true };

    public MainForm()
    {
        Text = "CabbyCodes 汉化器";
        Width = 760; Height = 260; StartPosition = FormStartPosition.CenterScreen;
        var p = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 4 };
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 78)); p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
        var browse = new Button { Text = "选择 DLL…", Dock = DockStyle.Fill };
        var run = new Button { Text = "开始汉化", Dock = DockStyle.Fill };
        p.Controls.Add(input, 0, 0); p.Controls.Add(browse, 1, 0);
        p.Controls.Add(output, 0, 1); p.Controls.Add(run, 1, 1);
        p.Controls.Add(status, 0, 2); p.SetColumnSpan(status, 2); Controls.Add(p);
        browse.Click += (_, _) => Choose(); run.Click += (_, _) => Translate();
    }

    void Choose()
    {
        using var d = new OpenFileDialog { Filter = ".NET DLL|*.dll|所有文件|*.*", Title = "选择 CabbyCodes.dll" };
        if (d.ShowDialog(this) != DialogResult.OK) return;
        input.Text = d.FileName;
        output.Text = Path.Combine(Path.GetDirectoryName(d.FileName)!, Path.GetFileNameWithoutExtension(d.FileName) + "_CN.dll");
        status.Text = "已选择文件。";
    }

    void Translate()
    {
        if (!File.Exists(input.Text)) { MessageBox.Show(this, "请先选择 DLL。", "提示"); return; }
        try
        {
            var r = Translator.Translate(input.Text, output.Text);
            status.Text = $"完成：扫描 {r.Scanned}，替换 {r.Replaced}。";
            MessageBox.Show(this, $"汉化完成！\n\n{output.Text}\n\n扫描：{r.Scanned}\n替换：{r.Replaced}", "完成");
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "失败", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}
