using System.Windows.Forms;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using OpenTK;
using MVCore.GMDL;

public partial class XYZControl : GroupBox
{
    public string groupname = "Default";
    public string fieldName = "";
    public Vector3 Position;
    public NumericUpDown[] valueFields = new NumericUpDown[3];
    public model model = null;

    public XYZControl(string field)
    {
        //Set Fieldname
        fieldName = field;
        //Create main container
        FlowLayoutPanel panel = new FlowLayoutPanel();
        panel.Size = new System.Drawing.Size(100, 50);

        //Create Numeric Elements
        SetupLayout();
        
        Label l_x = new Label();
        l_x.Dock = DockStyle.Fill;
        l_x.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
        l_x.Text = "X";
        l_x.Size = new System.Drawing.Size(20, 40);

        Label l_y = new Label();
        l_y.Dock = DockStyle.Fill;
        l_y.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
        l_y.Text = "Y";
        l_y.Size = new System.Drawing.Size(20, 40);

        Label l_z = new Label();
        l_z.Dock = DockStyle.Fill;
        l_z.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
        l_z.Text = "Z";
        l_z.Size = new System.Drawing.Size(20, 40);
        

        panel.Controls.Add(l_x);
        panel.Controls.Add(valueFields[0]);
        panel.Controls.Add(l_y);
        panel.Controls.Add(valueFields[1]);
        panel.Controls.Add(l_z);
        panel.Controls.Add(valueFields[2]);

        panel.Dock = DockStyle.Fill;
        this.Controls.Add(panel);
        //this.Dock = DockStyle.Fill;

        
    }


    private void SetupLayout()
    {
        for (int i = 0; i < 3; i++)
        {
            NumericUpDown x = new NumericUpDown();
            x.Size = new System.Drawing.Size(60, 40);
            x.Increment = (decimal)0.001f;
            x.Minimum = (decimal) -100000.0f;
            x.Maximum = (decimal) 100000.0f;
            x.DecimalPlaces = 4;
            x.ValueChanged += new System.EventHandler(update_position);

            valueFields[i] = x;
        }
    }


    private void update_position(object sender, System.EventArgs e)
    {
        if (model == null) return;

        //Update fields first
        for (int i = 0; i < 3; i++)
            Position[i] = (float)valueFields[i].Value;

        FieldInfo prop = model.GetType().GetField(fieldName, BindingFlags.FlattenHierarchy |
                          BindingFlags.Instance |
                          BindingFlags.Public);

        if (prop != null)
            prop.SetValue(model, Position);

        return;
        
    }

    public bool bind_model(model m)
    {
        if (m == null) return false;
        model = m;

        FieldInfo prop = m.GetType().GetField(fieldName, BindingFlags.FlattenHierarchy |
                          BindingFlags.Instance |
                          BindingFlags.Public);

        if (prop == null) return false;

        Vector3 inpos = new Vector3(0.0f, 0.0f, 0.0f);
        inpos = (Vector3) prop.GetValue(m);
        
        //Update Control
        for (int i = 0; i < 3; i++)
            valueFields[i].Value = (decimal) inpos[i];

        return true;
    }

}

