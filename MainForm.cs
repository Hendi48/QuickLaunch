namespace QuickLaunch;

public partial class MainForm : Form
{
    private readonly QuickLaunch quickLaunch;

    public MainForm()
    {
        InitializeComponent();

        quickLaunch = new QuickLaunch();

        foreach (var account in quickLaunch.Accounts)
            AddAccount(account);

        if (quickLaunch.GameExecutable != null)
            textBox1.Text = quickLaunch.GameExecutable;
    }

    private void AddAccount(Account account)
    {
        var li = new ListViewItem(account.Email);
        li.SubItems.Add(account.CookieExpiration.ToString());
        li.SubItems.Add("");
        li.Tag = account;
        listView1.Items.Add(li);
    }

    private void button1_Click(object sender, EventArgs e)
    {
        foreach (ListViewItem li in listView1.SelectedItems)
        {
            quickLaunch.RemoveAccount((Account)li.Tag);
            listView1.Items.Remove(li);
        }
    }

    private void button2_Click_1(object sender, EventArgs e)
    {
        string email = "";
        if (InputBox("Add Account", "Email:", ref email) != DialogResult.OK)
            return;

        var acc = new Account(email);
        quickLaunch.AddAccount(acc);
        AddAccount(acc);
    }

    private void button3_Click(object sender, EventArgs e)
    {
        if (openFileDialog1.ShowDialog() == DialogResult.OK)
        {
            textBox1.Text = openFileDialog1.FileName;
            quickLaunch.GameExecutable = openFileDialog1.FileName;
            quickLaunch.Save();
        }
    }

    private async void listView1_DoubleClick(object sender, EventArgs e)
    {
        if (listView1.SelectedItems.Count == 0)
            return;

        if (quickLaunch.GameExecutable == null)
        {
            MessageBox.Show("Please set the game path.");
            return;
        }

        var li = listView1.SelectedItems[0];
        li.SubItems[2].Text = "Launching...";
        var account = (Account)li.Tag;
        try
        {
            if (!await quickLaunch.Launch(account))
            {
                var loginView = new LoginView();
                loginView.LoginEmail = account.Email;
                loginView.ShowDialog(this);
                if (loginView.NxLCookie != null)
                {
                    account.Cookie = loginView.NxLCookie.Value;
                    account.CookieExpiration = loginView.NxLCookie.Expires;
                    quickLaunch.Save();
                    li.SubItems[1].Text = account.CookieExpiration.ToString();

                    if (!await quickLaunch.Launch(account))
                    {
                        li.SubItems[2].Text = "Failed";
                        MessageBox.Show("Launch attempt failed");
                    }
                    else
                    {
                        li.SubItems[2].Text = "Launched";
                    }
                }
                loginView.Dispose();
            }
            else
            {
                li.SubItems[2].Text = "Launched";
            }
        }
        catch (Exception ex)
        {
            li.SubItems[2].Text = $"Error: {ex.Message}";
        }
    }

    private void listView1_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
            listView1_DoubleClick(sender, e);
    }

    public DialogResult InputBox(string title, string promptText, ref string value)
    {
        Form form = new Form();
        Label label = new Label();
        TextBox textBox = new TextBox();
        Button buttonOk = new Button();
        Button buttonCancel = new Button();

        form.Text = title;
        label.Text = promptText;
        textBox.Text = value;

        buttonOk.Text = "OK";
        buttonCancel.Text = "Cancel";
        buttonOk.DialogResult = DialogResult.OK;
        buttonCancel.DialogResult = DialogResult.Cancel;

        label.SetBounds(9, 20, 372, 13);
        textBox.SetBounds(12, 36, 372, 20);
        buttonOk.SetBounds(228, 72, 75, 23);
        buttonCancel.SetBounds(309, 72, 75, 23);

        label.AutoSize = true;
        textBox.Anchor |= AnchorStyles.Right;
        buttonOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

        form.ClientSize = new Size(396, 107);
        form.Controls.AddRange(new Control[] { label, textBox, buttonOk, buttonCancel });
        form.ClientSize = new Size(Math.Max(300, label.Right + 10), form.ClientSize.Height);
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.StartPosition = FormStartPosition.CenterParent;
        form.MinimizeBox = false;
        form.MaximizeBox = false;
        form.AcceptButton = buttonOk;
        form.CancelButton = buttonCancel;

        DialogResult dialogResult = form.ShowDialog(this);
        value = textBox.Text;
        return dialogResult;
    }
}