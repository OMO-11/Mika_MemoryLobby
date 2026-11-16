using System;
using System.Windows.Forms;
class ControlledTarget {
    [STAThread] static void Main() {
        var form = new Form { Text = "GiftCompanion Phase5 controlled target", Width = 340, Height = 130 };
        form.Controls.Add(new Label { Text = "Owned validation window. Closes automatically.", Dock = DockStyle.Fill });
        var timer = new Timer { Interval = 45000 }; timer.Tick += (s,e) => form.Close(); timer.Start();
        Application.Run(form);
    }
}